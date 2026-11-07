"""Corbie's independent ViZDoom game instance; input never reaches other apps."""
import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path
import re
import struct
import zlib
from pilot import Pilot, BUTTONS, CONTROL_TICS, MAX_TURN, WEAPON_BUTTONS, visible_enemies
from backseat import BackseatChat, snapshot

ROOT = Path(__file__).resolve().parent
VARIABLES = ('HEALTH', 'ARMOR', 'SELECTED_WEAPON_AMMO', 'KILLCOUNT', 'ITEMCOUNT',
             'POSITION_X', 'POSITION_Y', 'SELECTED_WEAPON', 'ATTACK_READY', 'ANGLE') + tuple(
                 name + str(slot) for name in ('WEAPON', 'AMMO') for slot in range(1, 8))

def wad(path, kind):
    path = Path(path)
    if str(path).startswith(('\\\\', '//')) or path.suffix.lower() != '.wad':
        raise ValueError('Choose a local WAD file.')
    path = path.resolve(strict=True)
    size = path.stat().st_size
    if not 12 <= size <= 512*1024*1024:
        raise ValueError('Invalid WAD size.')
    with path.open('rb') as stream:
        magic, count, offset = struct.unpack('<4sii', stream.read(12))
        if magic != kind or not 1 <= count <= 300000 or offset < 12 or offset+16*count > size:
            raise ValueError('Invalid WAD directory: ' + path.name)
        stream.seek(offset)
        names = set()
        for _ in range(count):
            start, length, name = struct.unpack('<ii8s', stream.read(16))
            if start < 0 or length < 0 or start+length > size:
                raise ValueError('Invalid WAD lump: ' + path.name)
            names.add(name.rstrip(b'\0').decode('ascii'))
    return path, names

def profile(name, root=ROOT):
    base = root/'games'/'doom2.wad'
    if not base.exists():
        base = root/'games'/'freedoom2.wad'
    base, names = wad(base, b'IWAD')
    if 'MAP01' not in names:
        raise ValueError('This player needs a Doom II / Freedoom Phase 2 base.')
    mods = []
    maps = names
    if name == 'avian':
        main, maps = wad(root/'games'/'AUAVIAN.wad', b'PWAD')
        # The author's readme identifies these as single-player maps. Exclude
        # deathmatch maps 10-12 and placeholder map markers 17-30.
        maps = maps & {f'MAP{i:02}' for i in (*range(1, 10), *range(13, 17), 31, 32)}
        mods.append(main)
        wide = root/'games'/'AAA_WS.wad'
        if wide.exists():
            mods.append(wad(wide, b'PWAD')[0])
    elif name != 'freedoom':
        raise ValueError('Unknown game profile.')
    identity = hashlib.sha256(b'corbie-vizdoom-v1')
    for file in [base, *mods]:
        with file.open('rb') as stream:
            for block in iter(lambda: stream.read(1024*1024), b''):
                identity.update(block)
    return base, mods, sorted(n.lower() for n in maps if re.fullmatch('MAP[0-9]{2}', n)), identity.hexdigest()[:16]

def png(path, image):
    """Save an RGB evidence frame without a display or an image-library dependency."""
    height, width, _ = image.shape
    raw = b''.join(b'\0' + row.tobytes() for row in image)
    def chunk(kind, data):
        return struct.pack('!I', len(data)) + kind + data + struct.pack('!I', zlib.crc32(kind+data))
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('!2I5B', width, height, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(raw)) + chunk(b'IEND', b''))

class Session:
    def __init__(self, name, map_name='map01', headless=False, root=ROOT):
        import vizdoom as vzd
        self.vzd = vzd
        base, mods, maps, key = profile(name, root)
        if map_name not in maps:
            raise ValueError('Map not present. Available maps: ' + ', '.join(maps))
        self.name, self.map, self.maps = name, map_name, maps
        self.directory = root/'player-state'/key/map_name
        self.directory.mkdir(parents=True, exist_ok=True)
        self.pilot = Pilot()
        self.steps = 0
        self.attack_input_ticks = 0
        self.seen_labels = {}
        self.enemy_frames = 0
        self.frames_left = 0
        self.held_action = None
        self.last_observation = None
        self.backseat = BackseatChat()
        self.reason = 'Ready. I have eyes. Questionable judgement, but eyes.'
        self.game = vzd.DoomGame()
        game = self.game
        game.set_doom_game_path(str(base))
        if mods:
            for extra in mods:
                if any(c in str(extra) for c in ('"', '\n', '\r')):
                    raise ValueError('Invalid add-on path.')
            game.add_game_args('-file ' + ' '.join('"'+str(extra)+'"' for extra in mods))
        if any(c in str(self.directory) for c in ('"', '\n', '\r')):
            raise ValueError('Invalid player-state path.')
        game.add_game_args('-noautoload -config "' + str(self.directory/'vizdoom.ini') + '" +vid_fullscreen 0 +sv_cheats 0')
        game.set_doom_map(map_name)
        game.set_doom_skill(2)
        game.set_mode(vzd.Mode.PLAYER)
        game.set_screen_resolution(vzd.ScreenResolution.RES_640X480)
        game.set_screen_format(vzd.ScreenFormat.RGB24)
        game.set_window_visible(False)  # Our spectator window renders the actual screen buffer.
        game.set_sound_enabled(not headless)
        game.set_depth_buffer_enabled(True)
        game.set_labels_buffer_enabled(True)
        game.set_render_hud(True)
        game.set_episode_timeout(35*60*20)
        game.set_available_buttons([getattr(vzd.Button, n) for n in BUTTONS])
        game.set_button_max_value(vzd.Button.TURN_LEFT_RIGHT_DELTA, MAX_TURN)
        self.variables = VARIABLES
        game.set_available_game_variables([getattr(vzd.GameVariable, n) for n in self.variables])
        try:
            game.init()
        except BaseException:
            game.close()
            raise

    def observation(self):
        state = self.game.get_state()
        if state is None or self.game.is_player_dead():
            return None
        values = dict(zip(self.variables, map(float, state.game_variables)))
        observation = {'state': state, 'depth': state.depth_buffer, 'health': values['HEALTH'],
                'armor': values['ARMOR'], 'tick': self.game.get_episode_time(),
                'angle': values['ANGLE'],
                'weapon': int(values['SELECTED_WEAPON']), 'attackReady': bool(values['ATTACK_READY']),
                'weapons': {slot: int(values['WEAPON' + str(slot)]) for slot in range(1, 8)},
                'ammoBySlot': {slot: values['AMMO' + str(slot)] for slot in range(1, 8)},
                'ammo': values['SELECTED_WEAPON_AMMO'], 'kills': values['KILLCOUNT'],
                'items': values['ITEMCOUNT'], 'x': values['POSITION_X'], 'y': values['POSITION_Y'],
                'labels': [{'name': l.object_name, 'category': l.object_category, 'cx': l.x+l.width/2,
                            'width': l.width, 'height': l.height} for l in state.labels]}
        self.last_observation = observation
        return observation

    def reset_control(self):
        self.frames_left = 0
        self.held_action = None

    def frame(self, observation=None):
        """Advance one visible tic; hits interrupt the usual four-tic plan."""
        if observation is None:
            observation = self.observation()
        if observation is None:
            return False
        enemies = visible_enemies(observation.get('labels', []))
        self.enemy_frames += bool(enemies)
        for label in observation.get('labels', []):
            key = (label['name'], label.get('category', ''))
            if key in self.seen_labels or len(self.seen_labels) < 128:
                self.seen_labels[key] = {**label, 'lastSeenTick': observation.get('tick'),
                    'hostile': label in enemies}
        if self.pilot.sense_damage(observation):
            self.reset_control()
        if self.frames_left == 0:
            move = self.pilot.choose(observation)
            self.reason = move.reason
            self.held_action = move.values
            self.frames_left = CONTROL_TICS
            self.steps += 1
        values = list(self.held_action)
        if self.frames_left != CONTROL_TICS:
            # Weapon cycling is a command, not a continuously held direction.
            values[BUTTONS.index('SELECT_NEXT_WEAPON')] = 0
            for button in WEAPON_BUTTONS:
                values[BUTTONS.index(button)] = 0
            values[BUTTONS.index('USE')] = 0
        self.game.make_action(values, 1)
        self.attack_input_ticks += bool(values[BUTTONS.index('ATTACK')])
        self.frames_left -= 1
        return not self.game.is_episode_finished()

    def step(self):
        """One fresh decision for console/headless use, stopping on episode end."""
        self.reset_control()
        for _ in range(CONTROL_TICS):
            if not self.frame():
                return False
        return True

    def report(self):
        observation = self.observation()
        data = {'game': self.name, 'map': self.map, 'decisions': self.steps,
                'controller': 'reflex-v10', 'displayTargetFps': 35, 'decisionIntervalTicks': CONTROL_TICS,
                'ticks': self.game.get_episode_time(), 'reason': self.reason,
                'dead': self.game.is_player_dead(), 'episodeEnded': self.game.is_episode_finished(),
                'timeout': self.game.is_episode_timeout_reached(),
                'campaignCompleted': False,
                'damageEvents': self.pilot.damage_events, 'healthLost': self.pilot.health_lost,
                'armorLost': self.pilot.armor_lost, 'lastHit': self.pilot.last_hit,
                'combatStatus': self.pilot.combat_status, 'attackInputTicks': self.attack_input_ticks,
                'enemyVisibleTicks': self.enemy_frames, 'observedLabels': list(self.seen_labels.values()),
                'failedRoutes': self.pilot.route.summary(),
                'backseatHint': self.pilot.guidance.mode,
                'lastDecisionSight': self.pilot.sight,
                'liveObservation': observation is not None}
        if observation is None and self.last_observation is not None:
            data['lastObservedStats'] = {key: self.last_observation[key]
                for key in ('tick', 'health', 'armor', 'ammo', 'kills', 'items', 'x', 'y', 'weapon', 'attackReady', 'weapons', 'ammoBySlot')
                if key in self.last_observation}
            data['lastObservedLabels'] = self.last_observation['labels']
        if observation:
            data.update({key: observation[key] for key in ('health', 'armor', 'ammo', 'kills', 'items', 'x', 'y', 'weapon', 'attackReady', 'weapons', 'ammoBySlot')})
            data['visibleObjects'] = sorted(set(l['name'] for l in observation['labels']))
            data['visibleLabels'] = observation['labels']
            # Keep the most recent depth evidence for tuning real room geometry;
            # this never becomes a growing per-frame recording.
            import numpy as np
            depth = np.asarray(observation['depth'])
            np.save(self.directory/'last-depth.npy', depth, allow_pickle=False)
            data['depthPercentiles'] = np.percentile(depth, [10, 50, 90]).tolist()
            png(self.directory/'last-frame.png', observation['state'].screen_buffer)
        (self.directory/'report.json').write_text(json.dumps(data, indent=2), encoding='utf8')
        return data

    def save(self):
        if self.game.is_episode_finished() or self.game.is_player_dead():
            raise ValueError('Cannot save a finished/dead episode. Use restart for a new attempt.')
        self.game.save(str(self.directory/'checkpoint.zds'))

    def load(self):
        path = self.directory/'checkpoint.zds'
        if not path.exists():
            raise ValueError('No checkpoint saved for this game and map.')
        self.game.load(str(path))
        self.pilot = Pilot()
        self.last_observation = None
        self.seen_labels = {}
        self.enemy_frames = 0
        self.reset_control()

    def close(self):
        self.backseat.close()
        self.game.close()

    def instruct(self, command):
        observation = self.observation()
        receipt = self.pilot.guidance.set(command, observation)
        if observation and command in ('left', 'right', 'around', 'back'):
            self.pilot.escape = 0
            self.pilot.blocked_moves = 0
            self.pilot.turn_without_progress = 0
        self.reset_control()
        return receipt

def f8_pressed():
    return os.name == 'nt' and bool(ctypes.windll.user32.GetAsyncKeyState(0x77) & 0x8000)

def view(session, mode='play', chat=None):
    import pygame
    from chat_panel import ChatPanel
    pygame.display.init()
    pygame.font.init()
    screen = pygame.display.set_mode((1120, 760), pygame.RESIZABLE)
    pygame.display.set_caption("Corbie plays " + ('Austrian Avian Association' if session.name == 'avian' else 'Freedoom') + ' | Player v10')
    font = pygame.font.SysFont('Segoe UI', 18)
    clock = pygame.time.Clock()
    paused = mode != 'play'
    session.reset_control()
    reason = session.reason
    observation = session.observation()
    existing = getattr(session, 'backseat', None)
    chat = chat or (existing if isinstance(existing, BackseatChat) else BackseatChat())
    session.backseat = chat
    panel = ChatPanel(chat)
    scaled = None

    def apply_command(command):
        nonlocal paused
        if command == 'pause':
            paused = True
            session.reset_control()
            return 'Paused. I am listening.'
        if command == 'resume':
            if observation is None:
                return 'There is no live game to resume. Use restart in the console.'
            paused = False
            session.reset_control()
            return 'Resuming. Try not to sound surprised when I survive.'
        return session.instruct(command)

    try:
        while True:
            if chat.poll():
                panel.scroll = 0
            for event in pygame.event.get():
                if event.type == pygame.QUIT or (event.type == pygame.KEYDOWN and event.key == pygame.K_F8):
                    return
                if event.type == pygame.WINDOWFOCUSLOST:
                    paused = True
                    session.reset_control()
                handled = panel.event(event, screen.get_size())
                if handled == 'focus':
                    paused = True
                    session.reset_control()
                elif handled == 'send':
                    paused = True
                    session.reset_control()
                    state = snapshot(session, observation, paused)
                    if chat.submit(panel.draft, state, apply_command):
                        panel.draft = ''
                        panel.blur()
                        panel.scroll = 0
                if handled:
                    continue
                if event.type == pygame.KEYDOWN and event.key == pygame.K_ESCAPE:
                    return
                if event.type == pygame.KEYDOWN and event.key == pygame.K_SPACE:
                    paused = not paused
                    session.reset_control()
                if event.type == pygame.KEYDOWN and event.key == pygame.K_s:
                    session.save()
                    reason = 'Checkpoint saved. A rare moment of responsibility.'
            if f8_pressed():
                return
            # Doom simulates at 35 tics/sec. Render every tic rather than
            # advancing seven tics and showing only one picture five times/sec.
            if not paused:
                if not session.frame(observation):
                    reason = 'I died. Rude.' if session.game.is_player_dead() else 'Episode ended. Check status; no campaign win is assumed.'
                    paused = True
                else:
                    reason = session.reason
                if mode == 'step':
                    paused = True
                observation = session.observation()
            screen.fill((20, 24, 35))
            width, height = screen.get_size()
            game_width = panel.layout(width, height)[0].x
            if observation:
                image = observation['state'].screen_buffer
                surface = pygame.image.frombuffer(image, (image.shape[1], image.shape[0]), 'RGB')
                scale = min(game_width/image.shape[1], max(1, height-115)/image.shape[0])
                size = (max(1, int(image.shape[1]*scale)), max(1, int(image.shape[0]*scale)))
                if scaled is None or scaled.get_size() != size:
                    scaled = pygame.transform.scale(surface, size)
                else:
                    pygame.transform.scale(surface, size, scaled)
                screen.blit(scaled, ((game_width-size[0])//2, (height-115-size[1])//2))
            lines = [('PAUSED' if paused else 'CORBIE PLAYING') + f' | {clock.get_fps():.0f} fps | Space: pause | T: chat | F8: console | S: save',
                     reason[:100], (f"Health: {observation.get('health', '?')} | Armor: {observation.get('armor', '?')} | Weapon: {observation.get('weapon', '?')} | Ammo: {observation.get('ammo', '?')}"
                                    if observation else 'No live player state. Use restart for a new attempt.')]
            if observation:
                lines.append(str(session.pilot.combat_status)[:100])
            screen.set_clip(pygame.Rect(0, 0, game_width, height))
            for i, text in enumerate(lines):
                screen.blit(font.render(text, True, (233, 215, 180)), (12, height-107+i*25))
            screen.set_clip(None)
            panel.draw(screen, font)
            pygame.display.flip()
            clock.tick(35)
    finally:
        panel.blur()
        session.reset_control()
        pygame.display.quit()
        session.report()

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--headless', action='store_true')
    parser.add_argument('--steps', type=int, default=150)
    parser.add_argument('--profile', choices=('avian', 'freedoom'), default='avian' if (ROOT/'games'/'AUAVIAN.wad').exists() else 'freedoom')
    args = parser.parse_args()
    if not 1 <= args.steps <= 3000:
        parser.error('--steps must be between 1 and 3000')
    session = None
    name, map_name = args.profile, 'map01'
    try:
        if args.headless:
            session = Session(name, map_name, True)
            for _ in range(args.steps):
                if not session.step():
                    break
            print(json.dumps(session.report(), indent=2))
            return
        print("CORBIE / DOOM DIVISION\nBird violence. Finally, a suitable use of my talents.")
        print('play: watch Corbie play | watch: paused view | step: one decision | status | save | load')
        print('avian / freedoom: change game | map MAP02: new map | restart: new attempt | quit')
        print('F8/Esc returns to this console. Space pauses; switching windows pauses. No desktop keys are sent.')
        while True:
            try:
                command = input('\nDoom > ').strip().lower()
            except (EOFError, KeyboardInterrupt):
                break
            if command in ('quit', 'exit', 'back'):
                break
            try:
                if command in ('avian', 'freedoom', 'restart') or command.startswith('map '):
                    if command in ('avian', 'freedoom'):
                        name, map_name = command, 'map01'
                    elif command.startswith('map '):
                        target = command[4:].strip()
                        if target not in profile(name)[2]:
                            raise ValueError('Unknown map; use status to see available maps.')
                        map_name = target
                    if session:
                        session.close()
                    session = None
                    print('New attempt selected. Unsaved current progress is discarded. Type play when ready.')
                    continue
                if command not in ('play', 'watch', 'step', 'save', 'load', 'status'):
                    print('Use play, watch, step, status, save, load, avian, freedoom, map MAPxx, restart or quit.')
                    continue
                if session is None:
                    session = Session(name, map_name)
                if command in ('play', 'watch', 'step'):
                    if command == 'step':
                        session.step()
                    view(session, 'watch' if command == 'step' else command)
                elif command == 'save':
                    session.save()
                    print('Checkpoint saved locally.')
                elif command == 'load':
                    session.load()
                    print('Checkpoint loaded; reflex memory reset.')
                elif command == 'status':
                    print(json.dumps(session.report(), indent=2))
                    print('Maps:', ', '.join(session.maps))
            except KeyboardInterrupt:
                print('\nPaused. Back at the console.')
            except Exception as error:
                print('Corbie > Paused: ' + str(error))
    finally:
        if session:
            session.close()

if __name__ == '__main__':
    main()
