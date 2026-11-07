import json
import math
import unittest
from author_poses import CHANNELS, LIMITS, ROOT, STATES, library, pose, validate


class PoseTests(unittest.TestCase):
    def test_shipped_asset_matches_authored_curves(self):
        data = json.loads((ROOT / 'avian-poses.json').read_text(encoding='utf-8'))
        validate(data)
        self.assertEqual(data, library())

    def test_joints_remain_bounded_between_exported_samples(self):
        for state in STATES:
            for i in range(1000):
                frame = pose(state, i/999)
                for key, limit in zip(CHANNELS, LIMITS):
                    self.assertTrue(math.isfinite(frame[key]))
                    self.assertLessEqual(abs(frame[key]), limit)

    def test_curves_close_without_a_loop_seam(self):
        for state in STATES:
            self.assertEqual(pose(state, 0), pose(state, 1))

    def test_wing_flight_followthrough_extends_outward(self):
        tips = [pose('flight', i/100)['wingTip'] for i in range(100)]
        self.assertGreaterEqual(min(tips), 0)
        self.assertGreater(max(tips)-min(tips), 20)

    def test_talking_has_a_quiet_half_instead_of_constant_bobbing(self):
        self.assertEqual(pose('talk', .75)['neckY'], 0)
        self.assertLess(pose('talk', .25)['neckY'], -1)

    def test_sleep_breathing_is_subtle(self):
        values = [pose('sleep', i/100)['neckY'] for i in range(100)]
        self.assertLessEqual(max(values)-min(values), .401)

    def test_unknown_pose_is_rejected(self):
        with self.assertRaises(ValueError):
            pose('unknown', .3)


if __name__ == '__main__':
    unittest.main()
