import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import struct
import os
import numpy as np
from pilot import Pilot, BUTTONS, CONTROL_TICS, MAX_TURN, WEAPON_BUTTONS, action, visible_enemies
from player import profile, wad, Session, view, VARIABLES
from navigation import RouteMemory, steer
from backseat import BackseatChat

def observation(**changes):
    result = dict(x=100, y=200, health=100, armor=0, ammo=20, weapon=2,
                  attackReady=True, weapons={}, ammoBySlot={}, labels=[], depth=np.full((60, 90), 180))
    result.update(changes)
    return result

def buttons(move):
    return dict(zip(BUTTONS, move.values))

def write_wad(path, magic, names):
    with path.open('wb') as f:
        f.write(struct.pack('<4sii', magic, len(names), 12))
        for name in names:
            f.write(struct.pack('<ii8s', 0, 0, name.encode('ascii')))

class ReflexTests(unittest.TestCase):
    def test_centred_enemy_is_attacked_and_dead_actor_is_not(self):
        p = Pilot()
        enemy = dict(name='DoomImp', cx=45, width=15, height=25)
        self.assertEqual(buttons(p.choose(observation(labels=[enemy])))['ATTACK'], 1)
        enemy['name'] = 'DeadDoomImp'
        self.assertEqual(buttons(p.choose(observation(labels=[enemy])))['ATTACK'], 0)

    def test_offcentre_enemy_is_aimed_at_before_firing(self):
        move = buttons(Pilot().choose(observation(labels=[dict(name='ZombieMan', cx=85, width=10, height=20)])))
        self.assertEqual(move['ATTACK'], 0)
        self.assertGreater(move['TURN_LEFT_RIGHT_DELTA'], 0)

    def test_low_health_retreats_while_attacking(self):
        move = buttons(Pilot().choose(observation(health=15, labels=[dict(name='Demon', cx=45, width=20, height=30)])))
        self.assertEqual(move['MOVE_BACKWARD'], 1)
        self.assertEqual(move['ATTACK'], 1)

    def test_health_pickup_is_preferred_when_hurt(self):
        labels = [dict(name='ClipBox', cx=10, width=20, height=20), dict(name='Medikit', cx=45, width=10, height=10)]
        move = Pilot().choose(observation(health=20, labels=labels))
        self.assertIn('Medikit', move.reason)
        self.assertEqual(buttons(move)['MOVE_FORWARD'], 1)

    def test_stuck_position_checks_door_then_escapes(self):
        p = Pilot()
        for _ in range(20):
            move = p.choose(observation())
            if p.escape:
                break
        self.assertEqual(buttons(move)['USE'], 1)
        self.assertEqual(buttons(move)['MOVE_BACKWARD'], 1)
        move = p.choose(observation())
        self.assertEqual(buttons(move)['MOVE_BACKWARD'], 1)
        self.assertEqual(buttons(move)['TURN_LEFT_RIGHT_DELTA'], 0)

    def test_obstacle_turns_toward_open_space(self):
        depth = np.full((60, 90), 3)
        depth[:, :30] = 180
        move = buttons(Pilot().choose(observation(depth=depth)))
        self.assertLess(move['TURN_LEFT_RIGHT_DELTA'], 0)
        self.assertEqual(move['MOVE_FORWARD'], 0)

    def test_ordinary_room_depth_does_not_trigger_wall_spinning(self):
        p = Pilot()
        depth = np.full((60, 90), 20)
        depth[:, :30] = 80
        for i in range(80):
            move = buttons(p.choose(observation(x=100+i*12, depth=depth)))
            self.assertEqual(move['MOVE_FORWARD'], 1)
            self.assertEqual(move['TURN_LEFT_RIGHT_DELTA'], 0)

    def test_narrow_horizon_avoids_floor_and_ceiling_obstacle_false_positive(self):
        depth = np.full((60, 90), 2)
        depth[22:32, :] = 25
        move = buttons(Pilot().choose(observation(depth=depth)))
        self.assertEqual(move['MOVE_FORWARD'], 1)
        self.assertEqual(move['TURN_LEFT_RIGHT_DELTA'], 0)

    def test_trapped_turning_escalates_to_varied_escape_moves_without_stopping(self):
        p = Pilot()
        moves = [p.choose(observation(depth=np.full((60, 90), 3))) for _ in range(300)]
        self.assertGreater(p.route.summary()[0]['attempts'], 3)
        self.assertTrue(any('different escape direction' in move.reason for move in moves))
        self.assertTrue(any(buttons(move)['MOVE_BACKWARD'] for move in moves))

    def test_obstacle_direction_does_not_flip_with_minor_sensor_changes(self):
        p = Pilot()
        depth = np.full((60, 90), 3)
        depth[:, :30] = 20
        depth[:, 60:] = 19
        first = buttons(p.choose(observation(depth=depth)))
        depth[:, :30] = 19
        depth[:, 60:] = 20
        second = buttons(p.choose(observation(depth=depth)))
        self.assertLess(first['TURN_LEFT_RIGHT_DELTA'], 0)
        self.assertLess(second['TURN_LEFT_RIGHT_DELTA'], 0)

    def test_aim_converges_with_small_per_tic_turns(self):
        p = Pilot()
        error = .4
        for _ in range(20):
            move = buttons(p.choose(observation(labels=[dict(name='DoomImp', cx=(.5+error)*90, width=10, height=20)])))
            self.assertLessEqual(abs(move['TURN_LEFT_RIGHT_DELTA']), MAX_TURN)
            error -= move['TURN_LEFT_RIGHT_DELTA'] * CONTROL_TICS / 90
            if abs(error) < .025:
                break
        self.assertLess(abs(error), .03)

    def test_bad_sensor_data_pauses_without_a_move(self):
        for depth in (None, np.array([[np.nan]]), np.zeros((1, 1))):
            with self.assertRaises(ValueError):
                Pilot().choose(observation(depth=depth))

class CombatTests(unittest.TestCase):
    def test_actual_freedoom_zombieman_name_is_recognized_over_health_bonus(self):
        # The desktop v4 report at tic 84 listed Zombieman, HealthBonus,
        # DoomPlayer, a loaded slot-2 pistol and zero fire commands.
        zombie = dict(name='Zombieman', cx=45, width=20, height=30)
        bonus = dict(name='HealthBonus', cx=45, width=10, height=10)
        move = Pilot().choose(observation(weapon=2, ammo=50, labels=[zombie, bonus]))
        self.assertEqual(buttons(move)['ATTACK'], 1)
        self.assertIn('Zombieman', move.reason)

    def test_engine_monster_category_recognizes_unknown_mod_class(self):
        enemy = dict(name='AvianCustomEnemy', category='Monster', cx=45, width=20, height=30)
        move = Pilot().choose(observation(labels=[enemy]))
        self.assertEqual(buttons(move)['ATTACK'], 1)

    def test_dead_self_and_player_labels_never_become_targets(self):
        for name, category in (('DeadZombieman', 'Monster'), ('DoomImp', 'Gore'),
                               ('DoomImp', 'Self'), ('DoomImp', 'Player'),
                               ('DoomImpBall', 'Projectile'), ('HealthBonus', 'Health')):
            labels = [dict(name=name, category=category, cx=45, width=20, height=30)]
            self.assertEqual(visible_enemies(labels), [])
            self.assertEqual(buttons(Pilot().choose(observation(labels=labels)))['ATTACK'], 0)

    def test_small_partially_visible_enemy_is_not_rejected_by_old_size_threshold(self):
        enemy = dict(name='Zombieman', cx=45, width=1, height=2)
        self.assertEqual(buttons(Pilot().choose(observation(labels=[enemy])))['ATTACK'], 1)

    def test_invalid_enemy_geometry_cannot_generate_aim_or_fire(self):
        for changes in (dict(width=0), dict(height=-1), dict(cx=np.nan), dict(width=np.inf)):
            enemy = dict(name='Zombieman', cx=45, width=20, height=30)
            enemy.update(changes)
            self.assertEqual(visible_enemies([enemy]), [])

    def test_enemy_crossing_firing_line_is_shot_without_perfect_centre(self):
        enemy = dict(name='Demon', cx=60, width=34, height=40)
        move = Pilot().choose(observation(labels=[enemy]))
        self.assertEqual(buttons(move)['ATTACK'], 1)
        self.assertIn('Firing at', move.reason)

    def test_offscreen_firing_line_still_requires_aiming(self):
        enemy = dict(name='DoomImp', cx=80, width=8, height=20)
        move = Pilot().choose(observation(labels=[enemy]))
        self.assertEqual(buttons(move)['ATTACK'], 0)
        self.assertIn('Lining up', move.reason)

    def test_retreat_keeps_firing_at_visible_enemy(self):
        p = Pilot()
        p.choose(observation(tick=0))
        move = p.choose(observation(tick=1, health=80,
            labels=[dict(name='Demon', cx=60, width=34, height=40)]))
        self.assertEqual(buttons(move)['ATTACK'], 1)
        self.assertEqual(buttons(move)['MOVE_BACKWARD'], 1)

    def test_offcentre_defence_stops_side_running_away_from_aim(self):
        move = Pilot().choose(observation(health=20, labels=[dict(name='DoomImp', cx=80, width=8, height=20)]))
        self.assertEqual(buttons(move)['MOVE_LEFT'], 0)
        self.assertEqual(buttons(move)['MOVE_RIGHT'], 0)
        self.assertEqual(buttons(move)['MOVE_BACKWARD'], 1)
        self.assertGreater(buttons(move)['TURN_LEFT_RIGHT_DELTA'], 0)

    def test_zero_ammo_fists_can_attack_close_enemy(self):
        move = Pilot().choose(observation(weapon=1, ammo=0,
            labels=[dict(name='Demon', cx=45, width=35, height=40)]))
        self.assertEqual(buttons(move)['ATTACK'], 1)
        self.assertEqual(buttons(move)['SELECT_NEXT_WEAPON'], 0)
        self.assertIn('Melee attack', move.reason)

    def test_melee_does_not_swing_at_distant_enemy(self):
        move = Pilot().choose(observation(weapon=1, ammo=0,
            labels=[dict(name='DoomImp', cx=45, width=5, height=8)]))
        self.assertEqual(buttons(move)['ATTACK'], 0)
        self.assertEqual(buttons(move)['MOVE_FORWARD'], 1)

    def test_melee_equips_loaded_gun_and_waits_for_raise_before_shooting(self):
        p = Pilot()
        enemy = dict(name='Demon', cx=45, width=30, height=40)
        obs = observation(weapon=1, ammo=0, weapons={1: 1, 2: 1}, ammoBySlot={2: 50}, labels=[enemy], tick=0)
        first = buttons(p.choose(obs))
        self.assertEqual(first['SELECT_WEAPON2'], 1)
        self.assertEqual(first['ATTACK'], 0)
        self.assertEqual(first['MOVE_FORWARD'], 0)
        for tick in (4, 8, 12):
            waiting = buttons(p.choose({**obs, 'tick': tick}))
            self.assertTrue(all(waiting[name] == 0 for name in WEAPON_BUTTONS))
            self.assertEqual(waiting['ATTACK'], 0)
            self.assertEqual(waiting['MOVE_FORWARD'], 0)
        loaded = buttons(p.choose({**obs, 'tick': 16, 'weapon': 2, 'ammo': 50}))
        self.assertEqual(loaded['ATTACK'], 1)
        self.assertTrue(all(loaded[name] == 0 for name in WEAPON_BUTTONS))

    def test_empty_gun_selects_owned_loaded_alternative(self):
        obs = observation(weapon=3, ammo=0, weapons={1: 1, 2: 1, 3: 1, 4: 1},
            ammoBySlot={2: 10, 3: 0, 4: 10}, labels=[dict(name='Demon', cx=45, width=30, height=40)])
        move = buttons(Pilot().choose(obs))
        self.assertEqual(move['SELECT_WEAPON4'], 1)
        self.assertEqual(move['ATTACK'], 0)
        self.assertEqual(move['SELECT_NEXT_WEAPON'], 0)

    def test_no_ammo_in_owned_guns_falls_back_to_melee(self):
        obs = observation(weapon=2, ammo=0, weapons={1: 1, 2: 1}, ammoBySlot={2: 0},
            labels=[dict(name='Demon', cx=45, width=30, height=40)])
        move = buttons(Pilot().choose(obs))
        self.assertEqual(move['SELECT_WEAPON1'], 1)
        self.assertEqual(move['ATTACK'], 0)

    def test_recovery_executes_weapon_request_instead_of_discarding_it(self):
        p = Pilot()
        p.escape = 8
        move = p.choose(observation(weapon=1, ammo=0, weapons={1: 1, 2: 1}, ammoBySlot={2: 50},
            labels=[dict(name='Demon', cx=45, width=30, height=40)], tick=0))
        self.assertEqual(buttons(move)['SELECT_WEAPON2'], 1)
        self.assertEqual(p.next_weapon_tick, 35)

    def test_all_recovery_phases_can_return_fire(self):
        for phase in (12, 8, 2):
            p = Pilot()
            p.escape = phase
            move = p.choose(observation(labels=[dict(name='DoomImp', cx=45, width=20, height=30)]))
            self.assertEqual(buttons(move)['ATTACK'], 1)

class SurvivalTests(unittest.TestCase):
    def test_hit_interrupts_aiming_even_before_enemy_is_centred(self):
        p = Pilot()
        enemy = dict(name='DoomImp', cx=80, width=10, height=20)
        p.choose(observation(tick=0, labels=[enemy]))
        move = p.choose(observation(tick=1, health=85, labels=[enemy]))
        self.assertIn('Taking damage', move.reason)
        self.assertEqual(buttons(move)['MOVE_BACKWARD'], 1)
        self.assertEqual(buttons(move)['ATTACK'], 0)
        self.assertEqual(p.damage_events, 1)

    def test_damage_response_persists_then_expires_after_quiet_game_tics(self):
        p = Pilot()
        p.choose(observation(tick=0))
        p.choose(observation(tick=1, health=95))
        move = p.choose(observation(tick=60, health=95, x=160))
        self.assertIn('Taking damage', move.reason)
        move = p.choose(observation(tick=72, health=95, x=220))
        self.assertIn('Exploring', move.reason)
        self.assertEqual(buttons(move)['MOVE_FORWARD'], 1)

    def test_repeated_damage_extends_defensive_window(self):
        p = Pilot()
        p.choose(observation(tick=0))
        p.choose(observation(tick=1, health=95))
        p.choose(observation(tick=50, health=90))
        self.assertEqual(p.danger_until, 155)
        self.assertEqual(p.damage_events, 2)
        self.assertEqual(p.health_lost, 10)

    def test_armor_loss_without_health_loss_is_a_hit(self):
        p = Pilot()
        p.choose(observation(tick=0, armor=50))
        move = p.choose(observation(tick=1, armor=45))
        self.assertIn('Taking damage', move.reason)
        self.assertEqual(p.health_lost, 0)
        self.assertEqual(p.armor_lost, 5)

    def test_initial_low_health_and_healing_do_not_invent_hits(self):
        p = Pilot()
        p.choose(observation(tick=0, health=20))
        p.choose(observation(tick=4, health=45, armor=30))
        self.assertEqual(p.damage_events, 0)
        p.choose(observation(tick=5, health=40, armor=30))
        self.assertEqual(p.health_lost, 5)

    def test_health_priority_over_enemy_when_pickup_path_is_clear(self):
        enemy = dict(name='Demon', cx=80, width=10, height=20)
        medicine = dict(name='Medikit', cx=45, width=10, height=10)
        move = Pilot().choose(observation(health=25, labels=[enemy, medicine]))
        self.assertIn('Medikit', move.reason)
        self.assertEqual(buttons(move)['MOVE_FORWARD'], 1)

    def test_does_not_rush_health_behind_enemy_or_close_wall(self):
        enemy = dict(name='Demon', cx=45, width=20, height=30)
        medicine = dict(name='Medikit', cx=45, width=10, height=10)
        move = Pilot().choose(observation(health=25, labels=[enemy, medicine]))
        self.assertEqual(buttons(move)['MOVE_FORWARD'], 0)
        move = Pilot().choose(observation(health=25, labels=[medicine], depth=np.full((60, 90), 3)))
        self.assertEqual(buttons(move)['MOVE_FORWARD'], 0)

    def test_low_health_retreat_does_not_require_centred_enemy(self):
        move = Pilot().choose(observation(health=25, labels=[dict(name='Demon', cx=80, width=10, height=20)]))
        self.assertEqual(buttons(move)['MOVE_BACKWARD'], 1)

    def test_damage_cancels_recovery_forward_probe_without_resetting_failure_budget(self):
        p = Pilot()
        p.choose(observation(tick=0))
        p.escape, p.recoveries = 2, 2
        move = p.choose(observation(tick=1, health=90))
        self.assertIn('Taking damage', move.reason)
        self.assertEqual(buttons(move)['MOVE_FORWARD'], 0)
        self.assertEqual(p.escape, 0)
        self.assertEqual(p.recoveries, 2)

    def test_unseen_attacker_response_moves_instead_of_only_turning(self):
        p = Pilot()
        p.choose(observation(tick=0))
        depth = np.full((60, 90), 3)
        move = p.choose(observation(tick=1, health=90, depth=depth))
        self.assertIn('unseen attacker', move.reason)
        self.assertEqual(buttons(move)['MOVE_BACKWARD'], 1)
        self.assertEqual(buttons(move)['ATTACK'], 0)

    def test_unreachable_medicine_cannot_bypass_stuck_recovery_forever(self):
        p = Pilot()
        medicine = dict(name='Medikit', cx=45, width=10, height=10)
        moves = [p.choose(observation(health=20, labels=[medicine])) for _ in range(300)]
        self.assertGreater(p.route.summary()[0]['attempts'], 3)
        self.assertTrue(any('different escape direction' in move.reason for move in moves))

    def test_urgent_replans_count_actual_turn_time(self):
        p = Pilot()
        p.choose(observation(tick=0))
        p.last_turn = 2
        p.choose(observation(tick=1, health=90))
        self.assertEqual(p.turn_without_progress, 2)

class NavigationTests(unittest.TestCase):
    def test_identical_sensors_do_not_repeat_same_escape_three_times(self):
        route = RouteMemory()
        plans = [route.fail(100, 200, 0, i*100, 180, 100) for i in range(3)]
        self.assertEqual(plans[0]['side'], -1)
        self.assertEqual(plans[1]['side'], 1)
        self.assertEqual(plans[2]['plan'], 'backtrack')
        self.assertEqual(plans[2]['heading'], 180)
        next_plan = route.fail(100, 200, 0, 400, 180, 100)
        self.assertEqual(next_plan['plan'], 'unstick')
        self.assertNotEqual(next_plan['heading'], plans[2]['heading'])

    def test_later_probes_vary_world_headings_and_bound_failure_memory(self):
        route = RouteMemory()
        plans = [route.fail(100, 200, i*17, i*4, 180, 100) for i in range(50)]
        self.assertEqual(len({plan['heading'] for plan in plans[3:11]}), 8)
        self.assertTrue(all(plan['plan'] == 'unstick' for plan in plans[3:]))
        self.assertLessEqual(len(route.summary()[0]['failedHeadings']), 12)
        self.assertEqual(route.summary()[0]['attempts'], 50)

    def test_advanced_escape_can_finish_when_it_reaches_probe_target(self):
        route = RouteMemory()
        plans = [route.fail(100, 200, 0, i*4, 180, 100) for i in range(4)]
        plan = plans[-1]
        x, y = plan['waypoint']
        self.assertIsNone(route.backtrack(plan, x, y, plan['heading'], 100, 100, 100))

    def test_damage_does_not_restart_an_advanced_escape_on_every_hit(self):
        p = Pilot()
        p.choose(observation(tick=0))
        for i in range(4):
            p.recover(180, 100, observation(), 4+i*4)
        p.sense_damage(observation(tick=20, health=90))
        self.assertEqual(p.escape, 56)
        self.assertEqual(p.recovery['plan'], 'unstick')
        move = p.choose(observation(tick=20, health=90,
            labels=[dict(name='Zombieman', cx=45, width=20, height=30)]))
        self.assertIn('different escape direction', move.reason)
        self.assertEqual(buttons(move)['ATTACK'], 1)

    def test_small_movement_does_not_erase_failed_corner(self):
        p = Pilot()
        p.choose(observation(x=100, tick=0))
        p.recover(180, 100, observation(x=100, angle=0), 4)
        p.escape = 0
        p.choose(observation(x=145, tick=8, angle=90))
        p.recover(180, 100, observation(x=105, angle=0), 12)
        self.assertEqual(p.recoveries, 2)
        self.assertEqual(p.direction, 1)

    def test_third_attempt_retraces_actual_previously_visited_point(self):
        route = RouteMemory()
        for i, x in enumerate((0, 48, 96, 144)):
            route.update(x, 0, i*4)
        plans = [route.fail(144, 0, 0, 20+i*100, 40, 20) for i in range(3)]
        self.assertEqual(plans[-1]['waypoint'], (48, 0))
        controls = route.backtrack(plans[-1], 144, 0, 0, 30, 30, 30)
        self.assertNotIn('MOVE_FORWARD', controls)
        self.assertNotEqual(controls['TURN_LEFT_RIGHT_DELTA'], 0)
        controls = route.backtrack(plans[-1], 144, 0, 180, 30, 30, 30)
        self.assertEqual(controls['MOVE_FORWARD'], 1)
        self.assertIsNone(route.backtrack(plans[-1], 49, 0, 180, 30, 30, 30))

    def test_detour_changes_heading_before_walking_into_failed_approach(self):
        route = RouteMemory()
        route.fail(100, 200, 0, 0, 180, 100)
        controls = route.detour(110, 200, 0, 100)
        self.assertEqual(controls.get('MOVE_FORWARD', 0), 0)
        self.assertGreater(controls['TURN_LEFT_RIGHT_DELTA'], 0)
        controls = route.detour(110, 200, 270, 100)
        self.assertEqual(controls['MOVE_FORWARD'], 1)
        self.assertEqual(controls['TURN_LEFT_RIGHT_DELTA'], 0)

    def test_unrelated_heading_and_new_location_are_not_blacklisted(self):
        route = RouteMemory()
        route.fail(100, 200, 0, 0, 180, 100)
        self.assertIsNone(route.detour(100, 200, 90, 100))
        self.assertIsNone(route.detour(300, 200, 0, 100))
        self.assertEqual(route.fail(300, 200, 0, 4, 180, 100)['attempt'], 1)

    def test_route_memory_is_bounded_and_old_failures_can_be_retried(self):
        route = RouteMemory()
        for i in range(150):
            route.update(i*100, 0, i*4)
            route.fail(i*100, 0, 0, i*4, 180, 100)
        self.assertLessEqual(len(route.trail), 64)
        self.assertLessEqual(len(route.corners), 32)
        route.update(14900, 0, 35*121+600)
        self.assertEqual(len(route.corners), 0)
        self.assertEqual(route.fail(14900, 0, 0, 35*121+601, 180, 100)['attempt'], 1)

    def test_backtrack_steering_wraps_angle_in_the_correct_direction(self):
        self.assertLess(steer(10, 350), 0)
        self.assertGreater(steer(350, 10), 0)
        self.assertEqual(steer(2, 358), 0)

    def test_blocked_backup_skips_to_side_step_early(self):
        p = Pilot()
        p.choose(observation())
        p.recover(180, 100, observation(), 4)
        p.blocked_moves = 4
        move = p.choose(observation(tick=8))
        self.assertEqual(buttons(move)['MOVE_BACKWARD'], 0)
        self.assertEqual(buttons(move)['MOVE_LEFT'], 1)

    def test_blocked_side_step_escalates_to_opposite_side_early(self):
        p = Pilot()
        p.choose(observation())
        p.recover(180, 100, observation(), 4)
        p.escape, p.blocked_moves = 5, 4
        p.choose(observation(tick=8))
        self.assertEqual(p.direction, 1)
        self.assertEqual(p.recoveries, 2)

    def test_backtracking_keeps_fire_controls_while_navigation_owns_steering(self):
        p = Pilot()
        for i in range(3):
            p.recover(180, 100, observation(angle=0), i*4)
        move = p.choose(observation(tick=16, angle=0,
            labels=[dict(name='Zombieman', cx=45, width=20, height=30)]))
        self.assertEqual(buttons(move)['ATTACK'], 1)
        self.assertNotEqual(buttons(move)['TURN_LEFT_RIGHT_DELTA'], 0)
        self.assertIn('Both sides failed', move.reason)

    def test_known_corner_detour_takes_priority_over_unreachable_health(self):
        p = Pilot()
        p.route.fail(100, 200, 0, 0, 180, 100)
        move = p.choose(observation(tick=4, angle=0, health=20,
            labels=[dict(name='Medikit', cx=45, width=10, height=10)]))
        self.assertEqual(buttons(move)['MOVE_FORWARD'], 0)
        self.assertIn('Remembered', move.reason)

    def test_known_corner_detour_does_not_suppress_combat(self):
        p = Pilot()
        p.route.fail(100, 200, 0, 0, 180, 100)
        move = p.choose(observation(tick=4, angle=0,
            labels=[dict(name='Zombieman', cx=45, width=20, height=30)]))
        self.assertEqual(buttons(move)['ATTACK'], 1)

    def test_records_attempted_strafe_direction_instead_of_camera_direction(self):
        p = Pilot()
        p.move('blocked side step', MOVE_LEFT=1)
        p.blocked_moves = 4
        p.recover(180, 100, observation(angle=0), 4)
        self.assertEqual(p.route.summary()[0]['failedHeadings'], [90])

    def test_damage_dodging_does_not_override_opposite_side_escape(self):
        p = Pilot()
        p.recover(180, 100, observation(angle=0), 0)
        p.recover(180, 100, observation(angle=0), 4)
        p.escape = 7
        p.danger_until = 100
        p.dodge_direction = -1
        move = p.choose(observation(tick=8, angle=0,
            labels=[dict(name='Zombieman', cx=45, width=20, height=30)]))
        self.assertEqual(buttons(move)['MOVE_RIGHT'], 1)
        self.assertEqual(buttons(move)['MOVE_LEFT'], 0)
        self.assertEqual(buttons(move)['ATTACK'], 1)

class ProfileTests(unittest.TestCase):
    def test_mod_changes_isolate_saves_and_only_singleplayer_maps_are_available(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            games = root/'games'
            games.mkdir()
            write_wad(games/'freedoom2.wad', b'IWAD', ['MAP01'])
            write_wad(games/'AUAVIAN.wad', b'PWAD', ['MAP01', 'MAP10', 'MAP17', 'MAP31'])
            _, mods, maps, first = profile('avian', root)
            self.assertEqual(maps, ['map01', 'map31'])
            self.assertEqual(len(mods), 1)
            write_wad(games/'AAA_WS.wad', b'PWAD', ['TITLEPIC'])
            _, mods, _, second = profile('avian', root)
            self.assertEqual(len(mods), 2)
            self.assertNotEqual(first, second)
            self.assertNotEqual(second, profile('freedoom', root)[3])
            write_wad(games/'AUAVIAN.wad', b'IWAD', ['MAP01'])
            with self.assertRaises(ValueError):
                profile('avian', root)

    def test_missing_and_malformed_files_cannot_reach_game_input(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp)/'broken.wad'
            path.write_bytes(struct.pack('<4sii', b'PWAD', 50, 12))
            with self.assertRaises(ValueError):
                wad(path, b'PWAD')
            with self.assertRaises(ValueError):
                wad('\\\\server\\remote.wad', b'IWAD')

    def test_finished_episode_never_calls_make_action_or_claims_campaign_win(self):
        from unittest.mock import Mock
        session = object.__new__(Session)
        session.game = Mock()
        session.game.get_state.return_value = None
        self.assertFalse(session.step())
        session.game.make_action.assert_not_called()

    def test_init_failure_closes_owned_engine(self):
        from unittest.mock import Mock
        import vizdoom
        fake = Mock()
        fake.init.side_effect = RuntimeError('init failed')
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            games = root/'games'
            games.mkdir()
            write_wad(games/'freedoom2.wad', b'IWAD', ['MAP01'])
            with patch.object(vizdoom, 'DoomGame', return_value=fake):
                with self.assertRaisesRegex(RuntimeError, 'init failed'):
                    Session('freedoom', root=root)
            fake.close.assert_called_once()

class PlaybackTests(unittest.TestCase):
    def session(self):
        from unittest.mock import Mock
        session = object.__new__(Session)
        session.game = Mock()
        session.game.is_episode_finished.return_value = False
        session.pilot = Mock()
        session.pilot.sense_damage.return_value = False
        session.pilot.choose.return_value = action('test', MOVE_FORWARD=1, USE=1, SELECT_NEXT_WEAPON=1)
        session.observation = Mock(return_value=observation())
        session.steps = 0
        session.attack_input_ticks = 0
        session.seen_labels = {}
        session.enemy_frames = 0
        session.reset_control()
        return session

    def test_one_second_presents_35_individual_tics_not_five_seven_tic_batches(self):
        session = self.session()
        for _ in range(35):
            self.assertTrue(session.frame())
        self.assertEqual(session.game.make_action.call_count, 35)
        self.assertTrue(all(call.args[1] == 1 for call in session.game.make_action.call_args_list))
        self.assertEqual(session.pilot.choose.call_count, 9)

    def test_repeated_collisions_do_not_end_frame_loop_or_close_game(self):
        session = self.session()
        session.pilot = Pilot()
        session.observation.return_value = observation(depth=np.full((60, 90), 3))
        for _ in range(1200):
            self.assertTrue(session.frame())
        self.assertGreater(session.pilot.route.summary()[0]['attempts'], 3)
        self.assertEqual(session.game.make_action.call_count, 1200)
        session.game.close.assert_not_called()

    def test_console_step_is_one_decision_with_each_intermediate_tic_available(self):
        session = self.session()
        self.assertTrue(session.step())
        self.assertEqual(session.pilot.choose.call_count, 1)
        self.assertEqual(session.game.make_action.call_count, CONTROL_TICS)

    def test_discrete_commands_pulse_once_but_movement_is_held(self):
        session = self.session()
        session.step()
        moves = [dict(zip(BUTTONS, call.args[0])) for call in session.game.make_action.call_args_list]
        self.assertEqual([m['SELECT_NEXT_WEAPON'] for m in moves], [1, 0, 0, 0])
        self.assertEqual([m['USE'] for m in moves], [1, 0, 0, 0])
        self.assertTrue(all(m['MOVE_FORWARD'] == 1 for m in moves))

    def test_resume_replans_instead_of_reusing_movement_from_before_pause(self):
        session = self.session()
        session.frame()
        session.reset_control()
        session.frame()
        self.assertEqual(session.pilot.choose.call_count, 2)

    def test_episode_end_stops_step_immediately(self):
        session = self.session()
        session.game.is_episode_finished.return_value = True
        self.assertFalse(session.step())
        self.assertEqual(session.game.make_action.call_count, 1)

    def test_attack_is_held_for_each_game_tick_and_counted_in_report_evidence(self):
        session = self.session()
        session.pilot = Pilot()
        session.observation.return_value = observation(labels=[dict(name='DoomImp', cx=45, width=20, height=30)])
        session.step()
        moves = [dict(zip(BUTTONS, call.args[0])) for call in session.game.make_action.call_args_list]
        self.assertTrue(all(m['ATTACK'] == 1 for m in moves))
        self.assertEqual(session.attack_input_ticks, CONTROL_TICS)

    def test_desktop_enemy_name_reaches_attack_command_in_actual_frame_path(self):
        session = self.session()
        session.pilot = Pilot()
        session.observation.return_value = observation(weapon=2, ammo=50, tick=84,
            labels=[dict(name='Zombieman', category='Monster', cx=45, width=20, height=30)])
        session.step()
        sent = [dict(zip(BUTTONS, call.args[0])) for call in session.game.make_action.call_args_list]
        self.assertTrue(all(move['ATTACK'] == 1 for move in sent))
        self.assertEqual(session.enemy_frames, CONTROL_TICS)
        self.assertTrue(session.seen_labels[('Zombieman', 'Monster')]['hostile'])

    def test_direct_weapon_selection_pulses_once_without_attack_during_raise(self):
        session = self.session()
        session.pilot = Pilot()
        session.observation.return_value = observation(weapon=1, ammo=0, weapons={1: 1, 2: 1}, ammoBySlot={2: 50},
            labels=[dict(name='DoomImp', cx=45, width=20, height=30)])
        session.step()
        moves = [dict(zip(BUTTONS, call.args[0])) for call in session.game.make_action.call_args_list]
        self.assertEqual([m['SELECT_WEAPON2'] for m in moves], [1, 0, 0, 0])
        self.assertTrue(all(m['ATTACK'] == 0 for m in moves))

    def test_hit_replans_on_next_tic_instead_of_holding_old_movement(self):
        session = self.session()
        session.pilot = Pilot()
        session.frame(observation(tick=0))
        session.frame(observation(tick=1, health=90))
        moves = [dict(zip(BUTTONS, call.args[0])) for call in session.game.make_action.call_args_list]
        self.assertEqual(moves[0]['MOVE_FORWARD'], 1)
        self.assertEqual(moves[1]['MOVE_FORWARD'], 0)
        self.assertEqual(session.steps, 2)
        self.assertEqual(session.pilot.damage_events, 1)

    def test_damage_after_healing_between_decisions_is_not_hidden_by_net_gain(self):
        session = self.session()
        session.pilot = Pilot()
        session.frame(observation(tick=0, health=50))
        session.frame(observation(tick=1, health=100))
        session.frame(observation(tick=2, health=95))
        self.assertEqual(session.pilot.health_lost, 5)
        self.assertEqual(session.steps, 2)

    def test_load_resets_damage_memory_and_held_action(self):
        session = self.session()
        session.pilot = Pilot()
        session.frame(observation(tick=10))
        session.frame(observation(tick=11, health=80))
        with tempfile.TemporaryDirectory() as temp:
            session.directory = Path(temp)
            (session.directory/'checkpoint.zds').touch()
            session.load()
        session.frame(observation(tick=1, health=20))
        self.assertEqual(session.pilot.damage_events, 0)
        self.assertEqual(session.pilot.danger_until, 0)

class DamageReportTests(unittest.TestCase):
    def test_observation_exposes_live_armor_and_tick(self):
        from unittest.mock import Mock
        session = object.__new__(Session)
        session.game = Mock()
        session.game.is_player_dead.return_value = False
        session.game.get_episode_time.return_value = 12
        session.variables = VARIABLES
        values = dict(HEALTH=80, ARMOR=35, SELECTED_WEAPON_AMMO=10, KILLCOUNT=0,
                      ITEMCOUNT=0, POSITION_X=100, POSITION_Y=200, SELECTED_WEAPON=2,
                      ATTACK_READY=1, WEAPON1=1, WEAPON2=1, AMMO2=10)
        label = Mock(object_name='Zombieman', object_category='Monster', x=35, width=20, height=30)
        session.game.get_state.return_value = Mock(game_variables=[values.get(name, 0) for name in VARIABLES], labels=[label])
        obs = session.observation()
        self.assertEqual(obs['armor'], 35)
        self.assertEqual(obs['tick'], 12)
        self.assertEqual(obs['weapon'], 2)
        self.assertTrue(obs['attackReady'])
        self.assertEqual(obs['weapons'][2], 1)
        self.assertEqual(obs['ammoBySlot'][2], 10)
        self.assertEqual(obs['labels'][0]['category'], 'Monster')
        self.assertEqual(obs['labels'][0]['cx'], 45)
        self.assertIs(session.last_observation, obs)

    def test_dead_report_keeps_last_live_stats_explicitly_marked_as_previous(self):
        from unittest.mock import Mock
        session = object.__new__(Session)
        session.name, session.map, session.steps, session.reason = 'freedoom', 'map01', 5, 'Taking damage'
        session.attack_input_ticks = 4
        session.seen_labels = {('Zombieman', 'Monster'): dict(name='Zombieman', category='Monster', hostile=True)}
        session.enemy_frames = 1
        session.game = Mock()
        session.game.get_episode_time.return_value = 20
        session.game.is_player_dead.return_value = True
        session.game.is_episode_finished.return_value = True
        session.game.is_episode_timeout_reached.return_value = False
        session.game.get_state.return_value = None
        session.last_observation = observation(tick=19, health=5, kills=1, items=0)
        session.pilot = Pilot()
        session.pilot.sense_damage(observation(tick=0))
        session.pilot.sense_damage(observation(tick=19, health=5))
        with tempfile.TemporaryDirectory() as temp:
            session.directory = Path(temp)
            data = session.report()
        self.assertFalse(data['liveObservation'])
        self.assertEqual(data['lastObservedStats']['health'], 5)
        self.assertNotIn('health', data)
        self.assertEqual(data['damageEvents'], 1)
        self.assertEqual(data['healthLost'], 95)
        self.assertEqual(data['attackInputTicks'], 4)
        self.assertEqual(data['observedLabels'][0]['name'], 'Zombieman')
        self.assertEqual(data['enemyVisibleTicks'], 1)
        self.assertFalse(data['campaignCompleted'])

class SpectatorTests(unittest.TestCase):
    def render(self, events, mode='play', chat=None):
        import pygame
        from unittest.mock import Mock
        session = Mock()
        session.name = 'avian'
        session.map = 'map01'
        session.reason = 'Test movement'
        session.pilot = Pilot()
        session.game.is_player_dead.return_value = False
        session.attack_input_ticks = 0
        session.last_observation = None
        session.frame.return_value = True
        session.observation.return_value = observation(state=Mock(screen_buffer=np.zeros((480, 640, 3), dtype=np.uint8)))
        session.instruct.side_effect = lambda command: session.pilot.guidance.set(command, session.observation.return_value)
        with patch.dict(os.environ, {'SDL_VIDEODRIVER': 'dummy', 'SDL_AUDIODRIVER': 'dummy'}), \
             patch.object(pygame.event, 'get', side_effect=events), \
             patch.object(pygame.font, 'SysFont', side_effect=lambda *a: pygame.font.Font(None, 18)), \
             patch('player.f8_pressed', return_value=False):
            view(session, mode, chat=chat)
        session.report.assert_called_once()
        return session

    def test_actual_renderer_displays_each_advanced_frame_without_format_errors(self):
        import pygame
        session = self.render([[] for _ in range(35)]+[[pygame.event.Event(pygame.QUIT)]])
        self.assertEqual(session.frame.call_count, 35)

    def test_focus_loss_stops_advancement_until_explicit_resume(self):
        import pygame
        events = [[], [], [pygame.event.Event(pygame.WINDOWFOCUSLOST)], [], [],
                  [pygame.event.Event(pygame.KEYDOWN, key=pygame.K_SPACE)], [],
                  [pygame.event.Event(pygame.QUIT)]]
        session = self.render(events)
        self.assertEqual(session.frame.call_count, 4)

    def test_watch_stays_paused_and_f8_exits_before_a_move(self):
        import pygame
        session = self.render([[], [], [pygame.event.Event(pygame.KEYDOWN, key=pygame.K_F8)]], 'watch')
        session.frame.assert_not_called()

    def test_typing_chat_captures_save_and_space_keys_and_pauses_play(self):
        import pygame
        chat = BackseatChat(responder=lambda messages: 'Hello')
        events = [[pygame.event.Event(pygame.KEYDOWN, key=pygame.K_t)],
                  [pygame.event.Event(pygame.TEXTINPUT, text='go left')],
                  [pygame.event.Event(pygame.KEYDOWN, key=pygame.K_s),
                   pygame.event.Event(pygame.KEYDOWN, key=pygame.K_SPACE)],
                  [pygame.event.Event(pygame.KEYDOWN, key=pygame.K_RETURN)],
                  [pygame.event.Event(pygame.QUIT)]]
        session = self.render(events, chat=chat)
        session.frame.assert_not_called()
        session.save.assert_not_called()
        session.instruct.assert_called_once_with('left')
        self.assertIn('when you resume', chat.messages[-1][1])

    def test_resume_after_chat_advances_game_explicitly(self):
        import pygame
        events = [[pygame.event.Event(pygame.KEYDOWN, key=pygame.K_t)],
                  [pygame.event.Event(pygame.TEXTINPUT, text='go left')],
                  [pygame.event.Event(pygame.KEYDOWN, key=pygame.K_RETURN)],
                  [pygame.event.Event(pygame.KEYDOWN, key=pygame.K_SPACE)], [],
                  [pygame.event.Event(pygame.QUIT)]]
        session = self.render(events, chat=BackseatChat())
        self.assertEqual(session.frame.call_count, 2)

    def test_escape_cancels_composing_then_second_escape_exits(self):
        import pygame
        events = [[pygame.event.Event(pygame.KEYDOWN, key=pygame.K_t)],
                  [pygame.event.Event(pygame.TEXTINPUT, text='Unsent message')],
                  [pygame.event.Event(pygame.KEYDOWN, key=pygame.K_ESCAPE)],
                  [pygame.event.Event(pygame.KEYDOWN, key=pygame.K_ESCAPE)]]
        chat = BackseatChat()
        session = self.render(events, chat=chat)
        session.frame.assert_not_called()
        self.assertEqual(len(chat.messages), 1)

if __name__ == '__main__':
    unittest.main()
