"""Short-lived route memory from the player's own positions, never a hidden map."""
from collections import deque
import math


def distance(a, b):
    return math.hypot(a[0] - b[0], a[1] - b[1])


def angle_error(target, angle):
    return (target - angle + 180) % 360 - 180


def steer(target, angle):
    # Doom angles grow anticlockwise; the input delta grows clockwise.
    error = angle_error(target, angle)
    return 0 if abs(error) < 8 else max(-2.5, min(2.5, -error * .12))


class RouteMemory:
    def __init__(self):
        self.trail = deque(maxlen=64)
        self.corners = deque(maxlen=32)
        self.detour_heading = None

    def update(self, x, y, tick):
        point = (x, y)
        if not self.trail or distance(point, self.trail[-1]) >= 48:
            self.trail.append(point)
        # Keep failures through little movements and return visits, but allow
        # another try later if a door/platform or the combat situation changes.
        while self.corners and tick - self.corners[0]['tick'] > 35 * 120:
            self.corners.popleft()

    def nearby(self, point):
        candidates = [site for site in self.corners if distance(point, site['point']) < 64]
        return min(candidates, key=lambda site: distance(point, site['point'])) if candidates else None

    def fail(self, x, y, angle, tick, left, right, failed_heading=None):
        point = (x, y)
        site = self.nearby(point)
        if site is None:
            site = dict(point=point, tick=tick, attempts=0, firstSide=-1 if left > right else 1,
                        baseHeading=angle % 360, headings=[])
            self.corners.append(site)
        site['attempts'] += 1
        site['headings'].append((angle if failed_heading is None else failed_heading) % 360)
        site['headings'] = site['headings'][-12:]
        self.detour_heading = None
        attempt = site['attempts']
        # The second attempt explicitly reverses the first side even if the
        # depth readings are identical. The third retraces a known position.
        side = site['firstSide'] if attempt == 1 else -site['firstSide']
        waypoint = next((p for p in reversed(self.trail)
                         if 64 <= distance(point, p) <= 384 and distance(p, site['point']) >= 64), None)
        if attempt > 3:
            # Exhausted the familiar routes: try bounded probes at different
            # world headings instead of throwing an error and closing the view.
            # Fix the reference to this corner, so changes in camera angle do
            # not turn every new plan into the same relative turn.
            offsets = (135, -135, 90, -90, 180, 45, -45, 0)
            heading = (site['baseHeading'] + offsets[(attempt-4) % len(offsets)]) % 360
            radians = math.radians(heading)
            return dict(attempt=attempt, side=side, plan='unstick', heading=heading,
                        waypoint=(x + math.cos(radians)*128, y + math.sin(radians)*128))
        return dict(attempt=attempt, side=side,
                    plan='backtrack' if attempt == 3 else 'sidestep',
                    waypoint=waypoint, heading=(angle + 180) % 360)

    def detour(self, x, y, angle, front):
        """Avoid walking back into a remembered failed approach."""
        point = (x, y)
        site = self.nearby(point)
        if site is None:
            self.detour_heading = None
            return None
        if self.detour_heading is None:
            if not any(abs(angle_error(heading, angle)) < 30 for heading in site['headings']):
                return None
            # After a failed left escape, favor the right; after that, look
            # back along the trail. Do not treat 40 units of jitter as learning.
            waypoint = next((p for p in reversed(self.trail) if distance(point, p) >= 64), None)
            self.detour_heading = (math.degrees(math.atan2(waypoint[1]-y, waypoint[0]-x)) % 360
                                   if waypoint else (angle + site['firstSide'] * 90) % 360)
        target = self.detour_heading
        error = angle_error(target, angle)
        if abs(error) < 12:
            self.detour_heading = None
            return dict(MOVE_FORWARD=front >= 8, TURN_LEFT_RIGHT_DELTA=0)
        return dict(TURN_LEFT_RIGHT_DELTA=steer(target, angle))

    def backtrack(self, recovery, x, y, angle, front, left, right):
        waypoint = recovery['waypoint']
        if waypoint and distance((x, y), waypoint) < 20:
            return None
        heading = (math.degrees(math.atan2(waypoint[1]-y, waypoint[0]-x)) % 360
                   if waypoint else recovery['heading'])
        error = angle_error(heading, angle)
        controls = dict(TURN_LEFT_RIGHT_DELTA=steer(heading, angle))
        if abs(error) < 25:
            if front >= 8:
                controls['MOVE_FORWARD'] = 1
            elif max(left, right) >= 8:
                controls['MOVE_LEFT' if left > right else 'MOVE_RIGHT'] = 1
            else:
                controls['MOVE_BACKWARD'] = 1
        return controls

    def summary(self):
        return [dict(x=s['point'][0], y=s['point'][1], attempts=s['attempts'],
                     failedHeadings=s['headings'], tick=s['tick']) for s in self.corners]
