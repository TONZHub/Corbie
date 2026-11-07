"""Small pygame chat sidebar; keyboard capture is separate from game shortcuts."""
import pygame


def wrap(text, font, width):
    lines = []
    for paragraph in str(text).splitlines() or ['']:
        line = ''
        for word in paragraph.split():
            candidate = line + (' ' if line else '') + word
            if line and font.size(candidate)[0] > width:
                lines.append(line)
                line = ''
            while font.size(word)[0] > width and len(word) > 1:
                count = 1
                while count < len(word) and font.size(word[:count+1])[0] <= width:
                    count += 1
                lines.append(word[:count])
                word = word[count:]
            line += (' ' if line else '') + word
        lines.append(line)
    return lines


class ChatPanel:
    def __init__(self, chat):
        self.chat = chat
        self.editing = False
        self.draft = ''
        self.scroll = 0
        self.cached_key = None
        self.cached_rows = []
        pygame.key.stop_text_input()

    def layout(self, width, height):
        sidebar = min(350, max(220, int(width * .34)))
        left = width - sidebar
        return (pygame.Rect(left, 0, sidebar, height),
                pygame.Rect(left + 12, height - 86, max(1, sidebar - 94), 38),
                pygame.Rect(width - 74, height - 86, 62, 38))

    def focus(self):
        self.editing = True
        pygame.key.start_text_input()

    def blur(self):
        self.editing = False
        pygame.key.stop_text_input()

    def event(self, event, size):
        """Return focus/send/consumed, or None for a game shortcut."""
        panel, entry, send = self.layout(*size)
        if event.type == pygame.MOUSEBUTTONDOWN and event.button == 1:
            if entry.collidepoint(event.pos):
                self.focus()
                return 'focus'
            if send.collidepoint(event.pos):
                return 'send'
        if event.type == pygame.MOUSEWHEEL:
            self.scroll = max(0, self.scroll + event.y * 3)
            return 'consumed'
        if event.type == pygame.TEXTINPUT and self.editing:
            self.draft = (self.draft + event.text)[:500]
            return 'consumed'
        if event.type != pygame.KEYDOWN:
            return None
        if self.editing:
            if event.key == pygame.K_RETURN:
                return 'send'
            if event.key == pygame.K_ESCAPE:
                self.blur()
            elif event.key == pygame.K_BACKSPACE:
                self.draft = self.draft[:-1]
            elif event.key == pygame.K_v and getattr(event, 'mod', 0) & pygame.KMOD_CTRL:
                try:
                    pasted = (pygame.scrap.get_text() or '').replace('\r', ' ').replace('\n', ' ')
                    self.draft = (self.draft + pasted)[:500]
                except (pygame.error, AttributeError):
                    pass
            return 'consumed'
        if event.key in (pygame.K_t, pygame.K_RETURN):
            self.focus()
            return 'focus'
        return None

    def draw(self, screen, font):
        panel, entry, send = self.layout(*screen.get_size())
        pygame.draw.rect(screen, (28, 33, 45), panel)
        screen.blit(font.render('BACKSEAT CHAT', True, (235, 202, 139)), (panel.x+12, 15))
        screen.blit(font.render('T / Enter to type | /help for directions', True, (166, 179, 196)), (panel.x+12, 40))
        key = (panel.width, id(font), tuple(self.chat.messages), self.chat.busy)
        if key != self.cached_key:
            rows = []
            for who, text in self.chat.messages:
                color = (152, 207, 244) if who == 'You' else (233, 215, 180) if who == 'Corbie' else (180, 183, 193)
                rows.extend((line, color) for line in wrap(who + ': ' + text, font, panel.width-26))
                rows.append(('', color))
            if self.chat.busy:
                rows.append(('Corbie is thinking...', (166, 179, 196)))
            self.cached_key, self.cached_rows = key, rows
        rows = self.cached_rows
        count = max(1, (panel.height-174) // 22)
        self.scroll = min(self.scroll, max(0, len(rows)-count))
        end = len(rows)-self.scroll
        clip = screen.get_clip()
        screen.set_clip(pygame.Rect(panel.x+10, 68, panel.width-20, max(1, panel.height-174)))
        for i, (line, color) in enumerate(rows[max(0, end-count):end]):
            screen.blit(font.render(line, True, color), (panel.x+12, 68+i*22))
        screen.set_clip(clip)
        pygame.draw.rect(screen, (13, 18, 28), entry, border_radius=6)
        pygame.draw.rect(screen, (235, 202, 139) if self.editing else (71, 85, 107), entry, 1, border_radius=6)
        text = self.draft or ('Type a message...' if self.editing else 'Click here or press T')
        while text and font.size(text)[0] > entry.width-12:
            text = text[1:]
        screen.blit(font.render(text, True, (226, 231, 239)), (entry.x+6, entry.y+9))
        pygame.draw.rect(screen, (72, 91, 112), send, border_radius=6)
        screen.blit(font.render('Send', True, (244, 236, 218)), (send.x+10, send.y+9))
        note = 'Typing pauses play. Space outside chat resumes.'
        if self.chat.busy:
            note = 'Directions work while Corbie thinks.'
        for i, line in enumerate(wrap(note, font, panel.width-24)[:2]):
            screen.blit(font.render(line, True, (166, 179, 196)), (panel.x+12, panel.height-43+i*18))
