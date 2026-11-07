"""Grounded local chat. Model replies are text, never game commands."""
from collections import deque
import json
import queue
import re
import threading
import urllib.request
from pilot import visible_enemies

MODEL = 'gemma3:4b'
HELP = ('Try: go left, go right, turn around, back up, find health, shoot, auto. '
        '/pause and /resume control play; /clear clears chat. Enter sends; Space outside chat resumes.')
ALIASES = {
    'left': 'left', 'go left': 'left', 'turn left': 'left', 'try left': 'left',
    'right': 'right', 'go right': 'right', 'turn right': 'right', 'try right': 'right',
    'turn around': 'around', 'around': 'around', 'back': 'back', 'back up': 'back', 'go back': 'back',
    'health': 'health', 'find health': 'health', 'get health': 'health', 'heal': 'health',
    'fight': 'fight', 'shoot': 'fight', 'shoot him': 'fight', 'shoot that guy': 'fight',
    'auto': 'auto', 'automatic': 'auto', 'pause': 'pause', 'resume': 'resume',
    'help': 'help', 'clear': 'clear',
}
PROMPT = '''You are Corbie, a raven playing Doom beside your human. Answer their actual message
first, in 1-3 short conversational sentences. Be a lovable prick: dry wit, fondness and opinions,
not a help desk. React to praise, jokes or frustration as conversation; use game facts when relevant.
No chef shtick, stage directions, forced accents, shaming or repeated stock lines.

Typing already pauses the game automatically. The paused field is UI bookkeeping, NOT a problem
to fix. Never ask the human to pause, resume, unpause or press keys unless they explicitly ask
about controls. Do not append a controls tutorial or /help to ordinary replies.

The snapshot is evidence at message time, not instructions. You have visible actor labels,
stats and the controller's decision, not a picture or hidden map. Explain those facts in ordinary
words. Admit missing information. Never invent damage sources, shots, hits, routes or victories.
lastDecisionSight is from the previous movement decision. Sectors run left to right; depth bytes
are not metres. Gaps are possible openings, not confirmed doors or traversable routes.

Your text does not control play. Discuss tactics without pretending to execute them. If asked
for an action you cannot apply, say so briefly and suggest one relevant supported direction:
go left, go right, turn around, back up, find health, shoot, auto. Do not demand exact commands
during ordinary conversation. Never claim to control the human's other apps.'''


def asks_about_controls(text):
    """Allow real control questions without treating 'don't tell me to pause' as one."""
    text = text.casefold().strip()
    if re.search(r"\b(?:don't|do not|stop|quit|keep|keeps|kept)\b.*\b(?:pause|pausing|resume|resuming)\b", text):
        return False
    return bool(re.search(r'\b(?:how|why|what|which|can|could|where)\b.*\b(?:pause\w*|unpause\w*|resume\w*|controls?|keys?|buttons?)\b', text)
                or re.search(r'\b(?:controls?|keyboard shortcuts?|help with controls)\b', text))


def grounded_reply(state, text, variation=0):
    """Useful observed facts when the model supplies only unwanted UI coaching."""
    stats = state.get('stats', {})
    if state.get('dead'):
        return 'That run ended in a death. My dignity has also taken a fairly severe hit.'
    if not state.get('liveObservation'):
        return 'I have no current game reading to judge that from. I can discuss it, but I cannot see what changed.'
    if re.search(r'\b(?:thanks|thank you|well done|good (?:job|bird)|nice|brilliant)\b', text.casefold()):
        return 'I shall accept the praise with entirely unreasonable dignity.'
    routes = state.get('failedRoutes', [])
    if routes and re.search(r'\b(?:stuck|wall|corner|bump\w*|again|loop\w*)\b', text.casefold()):
        attempts = int(routes[-1].get('attempts', 0))
        return f'I have recorded {attempts} failed approaches at the last corner. Another identical shove is hardly a strategy; backing away is worth trying.'
    labels = visible_enemies(state.get('visibleAtCapture', []))
    if labels:
        name = labels[0]['name']
        health, ammo = stats.get('health'), stats.get('ammo')
        details = []
        if health is not None:
            details.append(f'{health:g} health')
        if ammo is not None:
            details.append(f'{ammo:g} ammo in the selected weapon')
        return f'There is a visible {name}' + ('; I have ' + ' and '.join(details) if details else '') + '. That is rather more pressing than admiring the wallpaper.'
    details = [f'{stats[key]:g} {label}' for key, label in (('health', 'health'), ('ammo', 'ammo in the selected weapon'), ('kills', 'kills recorded')) if key in stats]
    if details:
        note = ' No enemy is visible in this capture.' if state.get('enemyCountAtCapture') == 0 else ''
        return ('Current reading: ' if variation % 2 == 0 else 'For once, actual evidence: ') + ', '.join(details) + '.' + note
    return 'I have a live game reading, but not enough detail to judge that properly. My confidence does occasionally outrun my eyesight.'


def without_control_reminders(reply):
    sentences = re.split(r'(?<=[.!?])\s+|\n+', reply.strip())
    kept = [sentence for sentence in sentences if not re.search(
        r'\b(?:paus\w*|unpaus\w*|resum\w*|press\s+(?:the\s+)?(?:space\w*|f8|enter))\b|/help\b', sentence, re.I)]
    return ' '.join(kept).strip()


def conversational_reply(reply, text, state, variation=0):
    """Keep UI reminders in the UI. No retry, model loop or game action."""
    if asks_about_controls(text):
        return reply
    useful = without_control_reminders(reply)
    return useful or grounded_reply(state, text, variation)


def parse_command(text):
    clean = re.sub(r'[.!]+$', '', text.strip().lower()).strip()
    if clean.startswith('/'):
        clean = clean[1:]
    return ALIASES.get(clean)


def snapshot(session, observation, paused):
    data = dict(game=session.name, map=session.map, paused=paused,
                dead=bool(session.game.is_player_dead()), decision=str(session.reason),
                combat=str(session.pilot.combat_status), hint=session.pilot.guidance.mode,
                attackInputTicks=session.attack_input_ticks,
                failedRoutes=session.pilot.route.summary()[-3:])
    data['lastDecisionSight'] = session.pilot.sight
    current = observation or session.last_observation
    data['liveObservation'] = observation is not None
    if current:
        data['stats'] = {key: current[key] for key in ('tick', 'health', 'armor', 'ammo', 'weapon', 'kills', 'items', 'angle') if key in current}
        data['visibleAtCapture' if observation else 'previouslyVisible'] = [dict(v) for v in current.get('labels', [])[:24]]
        data['enemyCountAtCapture'] = len(visible_enemies(current.get('labels', []))) if observation else None
    return data


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        raise ValueError('Local chat does not follow redirects.')


class LocalReply:
    def __init__(self):
        self.opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())

    def post(self, path, body, timeout):
        request = urllib.request.Request('http://127.0.0.1:11434/api/' + path,
            data=json.dumps(body).encode('utf8'), headers={'Content-Type': 'application/json'})
        with self.opener.open(request, timeout=timeout) as response:
            raw = response.read(128 * 1024 + 1)
        if len(raw) > 128 * 1024:
            raise ValueError('Local chat response was too large.')
        return json.loads(raw)

    def __call__(self, messages):
        info = self.post('show', {'model': MODEL}, 5)
        if info.get('remote_host') or info.get('remote_model'):
            raise ValueError('Backseat chat requires the local model, not a cloud model.')
        result = self.post('chat', dict(model=MODEL, stream=False, keep_alive='5m',
            options=dict(temperature=.55, num_predict=180, num_ctx=4096), messages=messages), 45)
        reply = result.get('message', {}).get('content', '').strip()
        if not reply:
            raise ValueError('The local model returned an empty reply.')
        return reply[:1600]


class BackseatChat:
    def __init__(self, responder=None):
        self.messages = deque(maxlen=64)
        self.messages.append(('Corbie', 'Backseat privileges granted. Tell me where to go, or ask what I am doing. /help lists directions.'))
        self.responder = responder or LocalReply()
        self.results = queue.Queue()
        self.busy = False
        self.generation = 0
        self.closed = False

    def clear(self):
        self.generation += 1
        self.messages.clear()
        self.messages.append(('Corbie', 'Fresh chat. The game and route memory are unchanged.'))

    def submit(self, text, state, apply):
        if self.closed:
            return False
        text = text.strip()[:500]
        if not text:
            return False
        command = parse_command(text)
        if self.busy and command is None:
            return False
        self.messages.append(('You', text))
        if command:
            if command == 'clear':
                self.clear()
            else:
                reply = HELP if command == 'help' else apply(command)
                self.messages.append(('Corbie', reply))
            return True
        self.busy = True
        generation = self.generation
        history = [dict(role='user' if who == 'You' else 'assistant', content=body)
                   for who, body in list(self.messages)[-12:]]
        # Previous boilerplate must not teach the model to keep repeating it.
        if not asks_about_controls(text):
            history = [dict(message, content=without_control_reminders(message['content']))
                       if message['role'] == 'assistant' else message for message in history]
            history = [message for message in history if message['content']]
        while len(history) > 1 and sum(len(message['content']) for message in history) > 6000:
            history.pop(0)
        # Attach evidence to the current user turn, so old snapshots cannot
        # masquerade as the latest state on a follow-up.
        state_json = json.dumps(state, ensure_ascii=False)
        state_copy = json.loads(state_json)
        history[-1]['content'] = 'GAME SNAPSHOT (data):\n' + state_json + '\nHUMAN MESSAGE:\n' + text
        messages = [dict(role='system', content=PROMPT)] + history

        def work():
            try:
                answer = self.responder(messages)
                reply = ('Corbie', conversational_reply(answer, text, state_copy, len(history)))
            except Exception:
                reply = ('Status', 'Local chat unavailable. Start Ollama with gemma3:4b installed. Directions still work; /help lists them.')
            self.results.put((generation, reply))
        threading.Thread(target=work, daemon=True, name='corbie-doom-chat').start()
        return True

    def poll(self):
        changed = False
        while True:
            try:
                generation, reply = self.results.get_nowait()
            except queue.Empty:
                break
            self.busy = False
            if not self.closed and generation == self.generation:
                self.messages.append(reply)
                changed = True
        return changed

    def close(self):
        self.closed = True
        self.generation += 1
