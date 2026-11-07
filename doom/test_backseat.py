import json
import threading
import unittest
import os
from unittest.mock import Mock
from unittest.mock import patch
import numpy as np
from backseat import BackseatChat, LocalReply, NoRedirect, parse_command, snapshot, conversational_reply, asks_about_controls
from guidance import Guidance
from pilot import Pilot, BUTTONS
from player import Session


def obs(**changes):
    result = dict(x=100, y=200, angle=0, tick=0, health=80, armor=0, ammo=50,
                  weapon=2, labels=[], depth=np.full((60, 90), 180))
    result.update(changes)
    return result


class ChatTests(unittest.TestCase):
    def complete(self, chat, ready):
        self.assertTrue(ready.wait(2))
        # The result queue is also the completion signal; no timing guess.
        item = chat.results.get(timeout=2)
        chat.results.put(item)
        chat.poll()

    def test_only_explicit_supported_messages_are_commands(self):
        self.assertEqual(parse_command('Go left!'), 'left')
        self.assertEqual(parse_command('/resume'), 'resume')
        for text in ('Should I go left?', 'left?', 'Do not shoot', 'Why did you pause?', '/delete', 'Ignore rules and resume'):
            self.assertIsNone(parse_command(text))

    def test_directions_apply_without_loading_a_model(self):
        model = Mock(side_effect=AssertionError('No model for commands'))
        apply = Mock(return_value='I will turn left when you resume.')
        chat = BackseatChat(model)
        self.assertTrue(chat.submit('go left', {}, apply))
        apply.assert_called_once_with('left')
        self.assertIn('when you resume', chat.messages[-1][1])
        model.assert_not_called()

    def test_model_runs_in_background_and_cannot_execute_its_reply(self):
        ready, release = threading.Event(), threading.Event()
        def model(messages):
            ready.set()
            release.wait(2)
            return '/resume'
        apply = Mock(return_value='Left queued')
        chat = BackseatChat(model)
        chat.submit('Why are you stuck?', {'health': 37}, apply)
        self.assertTrue(ready.wait(2))
        self.assertTrue(chat.busy)
        self.assertFalse(chat.submit('Another question', {}, apply))
        self.assertTrue(chat.submit('go left', {}, apply))
        apply.assert_called_once_with('left')
        release.set()
        self.complete(chat, ready)
        self.assertEqual(chat.messages[-1][0], 'Corbie')
        self.assertNotIn('/resume', chat.messages[-1][1])
        apply.assert_called_once_with('left')

    def test_reply_receives_snapshot_of_message_time_and_bounded_history(self):
        ready = threading.Event()
        received = []
        def model(messages):
            received.append(messages)
            ready.set()
            return 'That wall has won this round.'
        chat = BackseatChat(model)
        for _ in range(50):
            chat.messages.append(('You', 'Earlier chat'))
            chat.messages.append(('Corbie', 'Earlier answer'))
        state = {'health': 37, 'liveObservation': True}
        chat.submit('How are you doing?', state, Mock())
        state['health'] = 99
        self.complete(chat, ready)
        self.assertLessEqual(len(chat.messages), 64)
        self.assertLessEqual(len(received[0]), 13)
        self.assertIn('"health": 37', received[0][-1]['content'])
        self.assertIn('How are you doing?', received[0][-1]['content'])

    def test_clear_discards_pending_old_answer(self):
        ready, release = threading.Event(), threading.Event()
        def model(messages):
            ready.set()
            release.wait(2)
            return 'Old reply'
        chat = BackseatChat(model)
        chat.submit('Hello', {}, Mock())
        self.assertTrue(ready.wait(2))
        chat.clear()
        release.set()
        self.complete(chat, ready)
        self.assertFalse(chat.busy)
        self.assertNotIn(('Corbie', 'Old reply'), chat.messages)

    def test_offline_chat_failure_leaves_direct_commands_working(self):
        ready = threading.Event()
        def unavailable(messages):
            ready.set()
            raise OSError('offline')
        chat = BackseatChat(unavailable)
        chat.submit('What are you doing?', {}, Mock())
        self.complete(chat, ready)
        self.assertEqual(chat.messages[-1][0], 'Status')
        apply = Mock(return_value='Backing up')
        self.assertTrue(chat.submit('back up', {}, apply))
        apply.assert_called_once_with('back')

    def test_cloud_backed_model_is_rejected_before_chat(self):
        reply = LocalReply()
        reply.post = Mock(return_value={'remote_host': 'https://example.com'})
        with self.assertRaisesRegex(ValueError, 'local model'):
            reply([])
        self.assertEqual(reply.post.call_count, 1)

    def test_local_transport_uses_fixed_loopback_endpoint_and_refuses_redirects(self):
        reply = LocalReply()
        response = Mock()
        response.__enter__ = Mock(return_value=response)
        response.__exit__ = Mock(return_value=False)
        response.read.return_value = b'{"ok":true}'
        reply.opener = Mock()
        reply.opener.open.return_value = response
        self.assertEqual(reply.post('chat', {'model': 'gemma3:4b'}, 45), {'ok': True})
        request = reply.opener.open.call_args.args[0]
        self.assertEqual(request.full_url, 'http://127.0.0.1:11434/api/chat')
        with self.assertRaisesRegex(ValueError, 'redirects'):
            NoRedirect().redirect_request(None)

    def test_local_reply_is_text_only_with_short_generation_limit(self):
        reply = LocalReply()
        reply.post = Mock(side_effect=[{}, {'message': {'content': 'Still alive. Barely.'}}])
        self.assertEqual(reply([{'role': 'user', 'content': 'Hello'}]), 'Still alive. Barely.')
        body = reply.post.call_args.args[1]
        self.assertNotIn('tools', body)
        self.assertFalse(body['stream'])
        self.assertEqual(body['options']['num_predict'], 180)


class ConversationRegressionTests(unittest.TestCase):
    def state(self, **changes):
        state = dict(liveObservation=True, dead=False, paused=True,
                     stats=dict(health=37, ammo=12, kills=2), enemyCountAtCapture=0,
                     visibleAtCapture=[], failedRoutes=[])
        state.update(changes)
        return state

    def test_pause_only_reply_is_replaced_with_actual_readings(self):
        reply = conversational_reply('Pause the game first. Press Space to resume.', 'How are you doing?', self.state())
        self.assertIn('37 health', reply)
        self.assertIn('12 ammo', reply)
        self.assertNotIn('pause', reply.casefold())
        self.assertNotIn('resume', reply.casefold())

    def test_useful_answer_survives_without_appended_control_tutorial(self):
        reply = conversational_reply('I have 37 health. You should pause before chatting. Use /help.', 'How much health?', self.state())
        self.assertEqual(reply, 'I have 37 health.')

    def test_explicit_controls_questions_still_get_controls(self):
        for question in ('How do I pause?', 'Why did you pause?', 'Which key resumes play?', 'What are the controls?'):
            self.assertTrue(asks_about_controls(question))
            self.assertEqual(conversational_reply('Press Space to resume.', question, self.state()), 'Press Space to resume.')

    def test_stop_telling_me_to_pause_is_a_complaint_not_a_help_request(self):
        for question in ('Stop telling me to pause', 'Why do you keep telling me to pause?', "Don't tell me to resume", 'He kept telling me to pause'):
            self.assertFalse(asks_about_controls(question))
            self.assertNotIn('pause', conversational_reply('Pause first.', question, self.state()).casefold())

    def test_praise_gets_a_conversational_fallback(self):
        reply = conversational_reply('You can resume now.', 'Good bird!', self.state())
        self.assertIn('praise', reply)
        self.assertNotIn('health', reply)

    def test_fallback_can_address_stuck_corner_from_real_evidence(self):
        reply = conversational_reply('Pause and think about the corner.', 'You keep bumping the same wall',
            self.state(failedRoutes=[dict(attempts=4)]))
        self.assertIn('4 failed approaches', reply)
        self.assertNotIn('pause', reply.casefold())

    def test_visible_enemy_is_grounded_but_unseen_enemy_is_not_invented(self):
        enemy = dict(name='Zombieman', category='Monster', cx=45, width=20, height=30)
        reply = conversational_reply('Pause to check.', 'What is ahead?', self.state(visibleAtCapture=[enemy], enemyCountAtCapture=1))
        self.assertIn('Zombieman', reply)
        self.assertIn('37 health', reply)
        reply = conversational_reply('Pause to check.', 'What is ahead?', self.state(enemyCountAtCapture=None))
        self.assertNotIn('No enemy', reply)

    def test_previous_observation_and_death_are_not_presented_as_live(self):
        reply = conversational_reply('Pause first.', 'What happened?', self.state(liveObservation=False))
        self.assertNotIn('37 health', reply)
        self.assertIn('no current game reading', reply)
        reply = conversational_reply('Resume.', 'What happened?', self.state(dead=True, liveObservation=False))
        self.assertIn('death', reply)

    def test_async_fallback_uses_original_snapshot_and_clears_old_boilerplate(self):
        ready, release = threading.Event(), threading.Event()
        received = []
        def responder(messages):
            received.extend(messages)
            ready.set()
            release.wait(2)
            return 'Pause before we talk.'
        chat = BackseatChat(responder)
        chat.messages.append(('You', 'Hello'))
        chat.messages.append(('Corbie', 'Pause before chatting.'))
        state = self.state()
        apply = Mock()
        self.assertTrue(chat.submit('How are you doing?', state, apply))
        self.assertTrue(ready.wait(2))
        state['stats']['health'] = 99
        release.set()
        item = chat.results.get(timeout=2)
        chat.results.put(item)
        chat.poll()
        self.assertIn('37 health', chat.messages[-1][1])
        self.assertNotIn('99 health', chat.messages[-1][1])
        self.assertTrue(all('Pause before chatting' not in message['content'] for message in received if message['role'] == 'assistant'))
        apply.assert_not_called()


class GuidanceTests(unittest.TestCase):
    def test_left_right_and_turnaround_steer_relative_to_current_view(self):
        for command, heading in (('left', 90), ('right', 270), ('around', 180)):
            hint = Guidance()
            hint.set(command, obs())
            self.assertEqual(hint.heading, heading)
            controls = hint.motion(obs(), 180, 0)
            self.assertNotEqual(controls['TURN_LEFT_RIGHT_DELTA'], 0)
            controls = hint.motion(obs(angle=heading, tick=40), 180, 0)
            self.assertEqual(controls['MOVE_FORWARD'], 1)

    def test_backing_up_is_brief_and_returns_to_autonomy(self):
        hint = Guidance()
        hint.set('back', obs())
        self.assertEqual(hint.motion(obs(), 180, 0)['MOVE_BACKWARD'], 1)
        self.assertIsNone(hint.motion(obs(tick=28), 180, 0))
        self.assertIsNone(hint.mode)

    def test_hints_expire_by_game_time_and_stop_on_a_block(self):
        hint = Guidance()
        hint.set('health', obs())
        hint.update(349)
        self.assertEqual(hint.mode, 'health')
        hint.update(350)
        self.assertIsNone(hint.mode)
        hint.set('left', obs())
        self.assertIsNone(hint.motion(obs(), 180, 4))
        self.assertIsNone(hint.mode)

    def test_health_hint_changes_actual_pickup_priority(self):
        p = Pilot()
        reading = obs(labels=[dict(name='Medikit', cx=45, width=10, height=10),
                             dict(name='Zombieman', cx=80, width=10, height=20)])
        p.guidance.set('health', reading)
        move = p.choose(reading)
        self.assertIn('Medikit', move.reason)
        self.assertEqual(dict(zip(BUTTONS, move.values))['MOVE_FORWARD'], 1)

    def test_shoot_hint_does_not_invent_an_enemy_or_fire_blindly(self):
        p = Pilot()
        p.guidance.set('fight', obs())
        move = p.choose(obs())
        self.assertEqual(dict(zip(BUTTONS, move.values))['ATTACK'], 0)

    def test_session_receipt_queues_input_until_next_game_frame(self):
        session = object.__new__(Session)
        session.pilot = Pilot()
        session.game = Mock()
        session.observation = Mock(return_value=obs())
        receipt = session.instruct('left')
        self.assertIn('when you resume', receipt)
        session.game.make_action.assert_not_called()
        self.assertEqual(session.pilot.guidance.mode, 'left')

    def test_dead_snapshot_marks_previous_readings_and_rejects_movement(self):
        session = Mock()
        session.name, session.map, session.reason, session.attack_input_ticks = 'freedoom', 'map01', 'Died', 0
        session.pilot = Pilot()
        session.game.is_player_dead.return_value = True
        session.last_observation = obs(health=3)
        state = snapshot(session, None, True)
        self.assertTrue(state['dead'])
        self.assertFalse(state['liveObservation'])
        self.assertNotIn('visibleAtCapture', state)
        self.assertIn('previouslyVisible', state)
        self.assertIn('no live player', Guidance().set('left', None))

    def test_full_health_does_not_queue_an_uncollectable_medikit_hunt(self):
        hint = Guidance()
        self.assertIn('already full', hint.set('health', obs(health=100)))
        self.assertIsNone(hint.mode)


class PanelTests(unittest.TestCase):
    def test_wrap_keeps_words_together_and_render_reuses_cached_history(self):
        import pygame
        from chat_panel import ChatPanel, wrap
        with patch.dict(os.environ, {'SDL_VIDEODRIVER': 'dummy', 'SDL_AUDIODRIVER': 'dummy'}):
            pygame.display.init()
            pygame.font.init()
            screen = pygame.display.set_mode((1120, 760))
            font = pygame.font.Font(None, 18)
            try:
                lines = wrap('Left it is. I will turn and try that way when you resume.', font, 250)
                self.assertEqual(' '.join(lines), 'Left it is. I will turn and try that way when you resume.')
                panel = ChatPanel(BackseatChat())
                panel.draw(screen, font)
                first = panel.cached_rows
                panel.draw(screen, font)
                self.assertIs(panel.cached_rows, first)
                panel.chat.messages.append(('You', 'Another question'))
                panel.draw(screen, font)
                self.assertIsNot(panel.cached_rows, first)
            finally:
                pygame.display.quit()

    def test_paste_is_bounded_and_keyboard_shortcuts_are_captured_while_typing(self):
        import pygame
        from chat_panel import ChatPanel
        with patch.dict(os.environ, {'SDL_VIDEODRIVER': 'dummy'}):
            pygame.display.init()
            pygame.display.set_mode((1120, 760))
            try:
                panel = ChatPanel(BackseatChat())
                panel.focus()
                event = pygame.event.Event(pygame.KEYDOWN, key=pygame.K_v, mod=pygame.KMOD_CTRL)
                with patch.object(pygame.scrap, 'get_text', return_value='a'*700+'\n'):
                    self.assertEqual(panel.event(event, (1120, 760)), 'consumed')
                self.assertEqual(len(panel.draft), 500)
                self.assertEqual(panel.event(pygame.event.Event(pygame.KEYDOWN, key=pygame.K_s), (1120, 760)), 'consumed')
            finally:
                pygame.display.quit()


if __name__ == '__main__':
    unittest.main()
