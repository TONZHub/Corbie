"""Explicit, short-lived backseat instructions inside Corbie's game only."""
from navigation import angle_error, steer


class Guidance:
    def __init__(self):
        self.mode = None
        self.until = 0
        self.heading = 0
        self.walk_until = None

    def set(self, command, observation):
        if command == 'auto':
            self.mode = None
            return 'Back to my own questionable judgement.'
        if observation is None:
            return 'There is no live player to steer. Start or restart a game first.'
        if command == 'health' and observation.get('health', 0) >= 100:
            return 'Health is already full. No health hunt queued.'
        self.mode = command
        tick = observation.get('tick', 0)
        self.until = tick + (350 if command in ('health', 'fight') else 105)
        self.heading = (observation.get('angle', 0) + dict(left=90, right=-90, around=180).get(command, 0)) % 360
        self.walk_until = None
        return dict(left='Left it is. I will turn and try that way when you resume.',
                    right='Right it is. I will turn and try that way when you resume.',
                    around='Turning around when you resume. A tactical change of mind.',
                    back='I will back up briefly when you resume. Yes, away from the wall.',
                    health='Health gets priority for the next ten game seconds. I still need to spot it.',
                    fight='I will focus on visible enemies for the next ten game seconds.')[command]

    def update(self, tick):
        if self.mode and tick >= self.until:
            self.mode = None

    def motion(self, observation, front, blocked):
        tick = observation.get('tick', 0)
        self.update(tick)
        if self.mode not in ('left', 'right', 'around', 'back'):
            return None
        if blocked >= 4:
            self.mode = None
            return None
        if self.mode == 'back':
            if self.walk_until is None:
                self.walk_until = tick + 28
            if tick >= self.walk_until:
                self.mode = None
                return None
            return dict(MOVE_BACKWARD=1)
        error = angle_error(self.heading, observation.get('angle', 0))
        if abs(error) >= 10:
            return dict(TURN_LEFT_RIGHT_DELTA=steer(self.heading, observation.get('angle', 0)))
        if self.walk_until is None:
            self.walk_until = tick + 18
        if tick >= self.walk_until or front < 8:
            self.mode = None
            return None
        return dict(MOVE_FORWARD=1)
