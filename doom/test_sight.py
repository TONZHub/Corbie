import unittest
import numpy as np
from sight import scan
from pilot import Pilot, BUTTONS


class SightTests(unittest.TestCase):
    def scene(self, depth, **changes):
        return dict(x=100, y=100, health=100, armor=0, ammo=20, weapon=2,
                    labels=[], depth=depth, **changes)

    def test_distinguishes_doorway_inside_sector_from_broad_wall(self):
        depth = np.full((60, 90), 3)
        depth[:, 10:25] = 40
        sight = scan(depth)
        self.assertEqual(sight['front'], 3)
        self.assertAlmostEqual(sight['preferred']['center'], 17.5 / 90)
        self.assertEqual(len(sight['sectors']), 9)
        move = Pilot().choose(self.scene(depth))
        self.assertEqual(move.reason, 'Turning toward a visible opening')
        self.assertLess(move.values[BUTTONS.index('TURN_LEFT_RIGHT_DELTA')], 0)
        self.assertEqual(move.values[BUTTONS.index('MOVE_FORWARD')], 0)

    def test_right_doorway_turns_right(self):
        depth = np.full((60, 90), 3)
        depth[:, 65:82] = 40
        move = Pilot().choose(self.scene(depth))
        self.assertGreater(move.values[BUTTONS.index('TURN_LEFT_RIGHT_DELTA')], 0)

    def test_slits_are_not_mistaken_for_passable_openings(self):
        depth = np.full((60, 90), 3)
        depth[:, 15:19] = 150
        self.assertIsNone(scan(depth)['preferred'])

    def test_thin_center_obstacle_is_not_averaged_away(self):
        depth = np.full((60, 90), 40)
        depth[:, 42:48] = 3
        self.assertEqual(scan(depth)['front'], 3)
        move = Pilot().choose(self.scene(depth))
        self.assertEqual(move.values[BUTTONS.index('MOVE_FORWARD')], 0)

    def test_floor_ceiling_do_not_hide_openings(self):
        depth = np.full((60, 90), 1)
        depth[22:32, :] = 40
        self.assertEqual(scan(depth)['front'], 40)
        self.assertEqual(len(scan(depth)['openings']), 1)

    def test_clear_room_keeps_straight_exploration(self):
        move = Pilot().choose(self.scene(np.full((60, 90), 40)))
        self.assertEqual(move.values[BUTTONS.index('MOVE_FORWARD')], 1)
        self.assertEqual(move.values[BUTTONS.index('TURN_LEFT_RIGHT_DELTA')], 0)

    def test_combat_still_takes_priority_over_opening(self):
        depth = np.full((60, 90), 3)
        depth[:, 10:25] = 40
        scene = self.scene(depth)
        scene['labels'] = [dict(name='Zombieman', cx=45, width=20, height=30)]
        move = Pilot().choose(scene)
        self.assertEqual(move.values[BUTTONS.index('ATTACK')], 1)

    def test_invalid_depth_is_rejected(self):
        for depth in (np.zeros((1, 90)), np.full((60, 90), np.nan), np.zeros((60, 2))):
            with self.assertRaises(ValueError):
                scan(depth)


if __name__ == '__main__':
    unittest.main()
