"""Fast, local Doom reflexes. No desktop input, LLM, training, or hidden map access."""
from dataclasses import dataclass
import numpy as np
from navigation import RouteMemory
from guidance import Guidance
from sight import scan

CONTROL_TICS = 4
MAX_TURN = 2.5  # degrees per game tic, not per decision

WEAPON_BUTTONS = tuple('SELECT_WEAPON' + str(slot) for slot in range(1, 8))
BUTTONS = ('MOVE_FORWARD', 'MOVE_BACKWARD', 'MOVE_LEFT', 'MOVE_RIGHT',
           'TURN_LEFT_RIGHT_DELTA', 'ATTACK', 'USE', 'SELECT_NEXT_WEAPON') + WEAPON_BUTTONS
HOSTILES = frozenset(('ZombieMan', 'ShotgunGuy', 'ChaingunGuy', 'DoomImp',
    'Demon', 'Spectre', 'Cacodemon', 'HellKnight', 'BaronOfHell', 'Arachnotron',
    'PainElemental', 'Revenant', 'Fatso', 'Archvile', 'SpiderMastermind',
    'Cyberdemon', 'LostSoul', 'WolfensteinSS'))
HOSTILE_NAMES = frozenset(name.casefold() for name in HOSTILES)
HEALTH = frozenset(('Medikit', 'Medkit', 'Stimpack', 'HealthBonus', 'Soulsphere', 'Megasphere'))
PICKUPS = HEALTH | frozenset(('Clip', 'ClipBox', 'Shell', 'ShellBox', 'RocketAmmo',
    'RocketBox', 'Cell', 'CellPack', 'Shotgun', 'SuperShotgun', 'Chaingun',
    'Chainsaw', 'RocketLauncher', 'PlasmaRifle', 'BFG9000', 'GreenArmor',
    'BlueArmor', 'ArmorBonus', 'BlueCard', 'YellowCard', 'RedCard',
    'BlueSkull', 'YellowSkull', 'RedSkull', 'Backpack'))

@dataclass
class Move:
    values: list
    reason: str

def action(reason, **buttons):
    return Move([float(buttons.get(name, 0)) for name in BUTTONS], reason)

def visible_enemies(labels):
    """Use engine monster categories and case-insensitive classic names."""
    enemies = []
    for label in labels:
        name = label.get('name', '').strip().casefold()
        category = label.get('category', '').strip().casefold()
        if name.startswith('dead') or category in ('gore', 'self', 'player'):
            continue
        if category != 'monster' and name not in HOSTILE_NAMES:
            continue
        # A partially visible/distant actor may be only one or two pixels wide.
        geometry = (label.get('cx', np.nan), label.get('width', 0), label.get('height', 0))
        if not all(np.isfinite(value) for value in geometry) or min(geometry[1:]) <= 0:
            continue
        enemies.append(label)
    return enemies

class Pilot:
    def __init__(self):
        self.ticks = 0
        self.escape = 0
        self.direction = 1
        self.previous_health = None
        self.previous_armor = None
        self.danger_until = 0
        self.dodge_direction = 1
        self.damage_events = 0
        self.health_lost = 0
        self.armor_lost = 0
        self.last_hit = None
        self.last_decision_tick = None
        self.next_weapon_tick = 0
        self.combat_status = 'No visible enemy'
        self.anchor = None
        self.turn_without_progress = 0
        self.last_turn = 0
        self.obstacle_direction = None
        self.recoveries = 0
        self.last_position = None
        self.last_translation = False
        self.blocked_moves = 0
        self.route = RouteMemory()
        self.recovery = None
        self.last_angle = 0
        self.attempted_heading = None
        self.guidance = Guidance()
        self.sight = None

    def sense_damage(self, observation):
        """Sample every game tic, including tics between movement decisions."""
        health, armor = observation['health'], observation.get('armor', 0)
        tick = observation.get('tick', self.ticks * CONTROL_TICS)
        health_loss = max(0, self.previous_health - health) if self.previous_health is not None else 0
        armor_loss = max(0, self.previous_armor - armor) if self.previous_armor is not None else 0
        self.previous_health, self.previous_armor = health, armor
        if not (health_loss or armor_loss):
            return False
        repeated = tick < self.danger_until
        self.danger_until = tick + (105 if repeated or health < 40 or health_loss >= 15 else 70)
        self.damage_events += 1
        self.health_lost += health_loss
        self.armor_lost += armor_loss
        self.last_hit = dict(tick=tick, healthLost=health_loss, armorLost=armor_loss)
        # Do not discard an escape on every hit: that can prevent it from ever
        # turning out of the corner. A short forward probe still yields to pain.
        if not self.recovery or self.recovery['plan'] == 'sidestep':
            self.escape = 0
        return True

    def move(self, reason, **buttons):
        self.last_turn = max(-MAX_TURN, min(MAX_TURN, buttons.get('TURN_LEFT_RIGHT_DELTA', 0)))
        buttons['TURN_LEFT_RIGHT_DELTA'] = self.last_turn
        self.last_translation = any(buttons.get(n, 0) for n in ('MOVE_FORWARD', 'MOVE_BACKWARD', 'MOVE_LEFT', 'MOVE_RIGHT'))
        if self.last_translation:
            forward = buttons.get('MOVE_FORWARD', 0) - buttons.get('MOVE_BACKWARD', 0)
            left = buttons.get('MOVE_LEFT', 0) - buttons.get('MOVE_RIGHT', 0)
            self.attempted_heading = (self.last_angle + float(np.degrees(np.arctan2(left, forward)))) % 360
        return action(reason, **buttons)

    def recover(self, left, right, observation, tick):
        self.recovery = self.route.fail(observation['x'], observation['y'],
            observation.get('angle', 0), tick, left, right,
            failed_heading=self.attempted_heading if self.blocked_moves >= 4 and self.last_translation else None)
        self.recoveries = self.recovery['attempt']
        self.escape = dict(backtrack=40, unstick=56, sidestep=12)[self.recovery['plan']]
        self.direction = self.recovery['side']
        self.turn_without_progress = 0
        self.blocked_moves = 0

    def combat(self, observation, target, width, height, tick):
        """Share aim/ammo decisions between fighting and defensive movement."""
        self.combat_status = 'No visible enemy'
        if target is None:
            return {}, False, False
        error = target['cx'] / width - .5
        # A nearby enemy can cover the firing line without its centre being
        # within the old fixed 8% gate. Use its visible bounding box instead.
        aligned = abs(target['cx'] - width / 2) <= target['width'] / 2 + width * .025
        turn = 0 if abs(error) < .025 else max(-MAX_TURN, min(MAX_TURN, error * 12))
        selected = int(observation.get('weapon', 2))
        ammo = observation['ammo']
        melee = selected == 1
        inventory = observation.get('weapons', {})
        ammo_by_slot = observation.get('ammoBySlot', {})
        # Classic Doom's fist/chainsaw use no ammunition. Equip a loaded gun
        # directly rather than cycling repeatedly and interrupting the raise.
        needs_gun = melee or ammo <= 0 or (selected == 7 and ammo < 40)
        loaded = [slot for slot in (4, 3, 2, 6, 7, 5)
                  if inventory.get(slot, 0) > 0 and ammo_by_slot.get(slot, 0) >= (40 if slot == 7 else 1)
                  and (slot != 5 or target['height'] < height * .35)]
        controls = dict(TURN_LEFT_RIGHT_DELTA=turn)
        if needs_gun and loaded and tick >= self.next_weapon_tick:
            controls['SELECT_WEAPON' + str(loaded[0])] = 1
            self.next_weapon_tick = tick + 35
            self.combat_status = 'Selecting loaded weapon ' + str(loaded[0])
            return controls, aligned, False
        if needs_gun and loaded:
            self.combat_status = 'Waiting for loaded weapon to equip'
            return controls, aligned, False
        if not melee and (ammo <= 0 or (selected == 7 and ammo < 40)):
            # Older/custom observations without inventory still get a bounded
            # fallback. Never cycle at the same time as holding attack.
            if tick >= self.next_weapon_tick and (inventory.get(1, 0) > 0 or not inventory):
                controls['SELECT_WEAPON1' if inventory else 'SELECT_NEXT_WEAPON'] = 1
                self.next_weapon_tick = tick + 35
            self.combat_status = 'No usable ammo; looking for a loaded weapon'
            return controls, aligned, melee
        close_enough = not melee or target['height'] >= height * .35
        controls['ATTACK'] = aligned and close_enough
        if controls['ATTACK']:
            self.combat_status = ('Melee attack on ' if melee else 'Firing at ') + target['name']
        elif melee:
            self.combat_status = 'Only melee available; enemy out of reach' if aligned else 'Lining up melee on ' + target['name']
        else:
            self.combat_status = 'Lining up shot on ' + target['name']
        return controls, aligned, melee

    def choose(self, observation):
        self.ticks += 1
        self.combat_status = 'No shot planned'
        tick = observation.get('tick', self.ticks * CONTROL_TICS)
        elapsed = tick - self.last_decision_tick if self.last_decision_tick is not None else 0
        self.last_decision_tick = tick
        x, y = observation['x'], observation['y']
        angle = observation.get('angle', 0)
        self.last_angle = angle
        self.route.update(x, y, tick)
        if self.anchor is None:
            self.anchor = (x, y)
        if np.hypot(x-self.anchor[0], y-self.anchor[1]) >= 40:
            self.anchor = (x, y)
            self.turn_without_progress = 0
            self.recoveries = 0
        else:
            self.turn_without_progress += abs(self.last_turn) * max(0, elapsed)
        if self.last_position is not None and self.last_translation:
            moved = np.hypot(x-self.last_position[0], y-self.last_position[1])
            self.blocked_moves = self.blocked_moves + 1 if moved < 2 else 0
        self.last_position = (x, y)
        depth = np.asarray(observation['depth'])
        if depth.ndim != 2 or not np.isfinite(depth).all():
            raise ValueError('Missing or invalid depth vision; paused.')
        h, w = depth.shape
        if h < 4 or w < 6:
            raise ValueError('Depth vision is too small; paused.')
        # A narrow horizon band sees openings without nearby floor/ceiling pixels.
        # Values are engine depth bytes, not map units: 35 was far too cautious
        # for ordinary rooms. A close wall is around 8; gaps need only about 12.
        self.sight = scan(depth)
        left, front, right = (self.sight[key] for key in ('left', 'front', 'right'))
        health = observation['health']
        self.sense_damage(observation)
        under_fire = tick < self.danger_until
        labels = observation.get('labels', [])
        enemies = visible_enemies(labels)
        target = min(enemies, key=lambda v: max(0, abs(v['cx'] - w / 2) - v['width'] / 2)) if enemies else None
        self.guidance.update(tick)
        guided = self.guidance.motion(observation, front, self.blocked_moves)
        if guided is not None:
            combat, _, _ = self.combat(observation, target, w, h, tick)
            if 'TURN_LEFT_RIGHT_DELTA' in guided:
                combat.pop('TURN_LEFT_RIGHT_DELTA', None)
                if guided['TURN_LEFT_RIGHT_DELTA']:
                    combat['ATTACK'] = 0
            return self.move('Following your backseat direction: ' + self.guidance.mode, **combat, **guided)
        defensive = under_fire or (health < 40 and bool(enemies))
        if self.escape and self.blocked_moves >= 4:
            if self.recovery and self.recovery['plan'] == 'sidestep' and self.escape >= 8:
                # Backing up is blocked too: skip straight to the side step.
                self.escape = 8
                self.blocked_moves = 0
            else:
                self.escape = 0
        if not self.escape and (self.turn_without_progress >= 120 or self.blocked_moves >= 4):
            self.recover(left, right, observation, tick)
        if not self.escape and not enemies and not defensive:
            detour = self.route.detour(x, y, angle, front)
            if detour is not None:
                return self.move('Remembered that blocked route. Going another way', **detour)
        # Only approach supplies already ahead, with a clear path and no enemy
        # directly across it. Labels cannot tell us where an unseen attacker is.
        medicine = [v for v in labels if v['name'] in HEALTH and v['width'] > 2 and v['height'] > 2
                    and abs(v['cx'] / w - .5) < .22
                    and not any(abs(e['cx'] - v['cx']) < (e['width'] + v['width']) / 2 for e in enemies)]
        if (health < 55 or (self.guidance.mode == 'health' and health < 100)) and medicine and front >= 8 and not self.escape and (self.guidance.mode != 'fight' or health < 25):
            pickup = max(medicine, key=lambda v: v['width'] * v['height'])
            pickup_error = pickup['cx'] / w - .5
            self.combat_status = 'Seeking health'
            return self.move('Health low. Getting ' + pickup['name'] + ' before another fight',
                MOVE_FORWARD=1, TURN_LEFT_RIGHT_DELTA=0 if abs(pickup_error) < .025 else pickup_error * 10)
        combat, aligned, melee = self.combat(observation, target, w, h, tick)
        if self.escape and self.recovery and self.recovery['plan'] in ('backtrack', 'unstick'):
            controls = self.route.backtrack(self.recovery, x, y, angle, front, left, right)
            self.escape -= 1
            if controls is not None:
                # Navigation owns steering here; keep fire/weapon controls so
                # escaping a corner does not silently disable shooting again.
                combat.pop('TURN_LEFT_RIGHT_DELTA', None)
                reason = ('Still stuck. Trying a different escape direction' if self.recovery['plan'] == 'unstick'
                          else 'That corner failed. Retracing my route' if self.recovery['waypoint']
                          else 'Both sides failed. Turning away from this corner')
                return self.move(reason, **combat, **controls)
            self.escape = 0
        if self.escape:
            if not target:
                self.combat_status = 'Recovering position'
            self.escape -= 1
            # Back away, then sidestep, then probe forward. The old recovery
            # combined backing up with repeated 84-degree turns.
            if self.escape >= 8:
                return self.move('Backing out of a tight spot', MOVE_BACKWARD=1, USE=self.escape == 11, **combat)
            if self.escape >= 3:
                return self.move('Trying a different line through the room',
                    MOVE_LEFT=self.direction < 0, MOVE_RIGHT=self.direction > 0,
                    **(combat if target else dict(TURN_LEFT_RIGHT_DELTA=self.direction * 1.5)))
            return self.move('Checking whether that cleared the obstruction',
                MOVE_FORWARD=front >= 8 and not defensive, MOVE_BACKWARD=defensive, USE=self.escape == 2, **combat)
        if defensive:
            # A corner escape owns movement above; generic combat dodging must
            # not override the chosen opposite side and repeat the old failure.
            if self.last_hit and self.last_hit['tick'] == tick:
                self.dodge_direction = -1 if left > right else 1
            side_clear = (left if self.dodge_direction < 0 else right) >= 8
            reason = ('Taking damage. ' if under_fire else 'Health low. ')
            reason += ('Breaking away from ' + target['name'] if target else 'Moving and checking for an unseen attacker')
            return self.move(reason, MOVE_BACKWARD=bool(target) or not side_clear,
                MOVE_LEFT=side_clear and (aligned or not target) and self.dodge_direction < 0,
                MOVE_RIGHT=side_clear and (aligned or not target) and self.dodge_direction > 0,
                **(combat if target else dict(TURN_LEFT_RIGHT_DELTA=self.dodge_direction * 2)))
        if enemies:
            # Classic autoaim handles height. Do not fire into empty screen regions.
            return self.move(self.combat_status,
                MOVE_FORWARD=melee and not combat.get('ATTACK') and not any(combat.get(b) for b in WEAPON_BUTTONS) and front >= 8,
                MOVE_RIGHT=(self.ticks // 12) % 2 == 0 and aligned and not melee,
                MOVE_LEFT=(self.ticks // 12) % 2 == 1 and aligned and not melee,
                **combat)
        pickups = [v for v in labels if v['name'] in (HEALTH if health < 55 or self.guidance.mode == 'health' else PICKUPS)]
        if pickups and front >= 8:
            target = max(pickups, key=lambda v: v['width'] * v['height'])
            error = target['cx'] / w - .5
            return self.move('Collecting ' + target['name'], MOVE_FORWARD=abs(error) < .22,
                          TURN_LEFT_RIGHT_DELTA=0 if abs(error) < .025 else error * 10)
        gap = self.sight['preferred']
        if front < 12 and gap and abs(gap['center'] - .5) > .08:
            self.obstacle_direction = None
            return self.move('Turning toward a visible opening',
                             TURN_LEFT_RIGHT_DELTA=(gap['center'] - .5) * 10,
                             USE=self.ticks % 15 == 0)
        if front < 8:
            # Commit to one side until there is room ahead; don't flip sides on
            # small depth changes, and don't arc endlessly through clear rooms.
            if self.obstacle_direction is None:
                self.obstacle_direction = -1 if left > right else 1
            return self.move('Turning gently toward an opening', USE=self.ticks % 4 == 0,
                          TURN_LEFT_RIGHT_DELTA=self.obstacle_direction * 2)
        self.obstacle_direction = None
        return self.move('Exploring; looking for enemies, supplies and doors', MOVE_FORWARD=1,
                         USE=self.ticks % 15 == 0)
