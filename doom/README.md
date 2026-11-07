# Corbie plays Doom

Open **Corbie-Doom.cmd**, **Corbie-Doom-Player.cmd**, or restart CLI Corbie and type **/doom**. This opens his dedicated Doom console. Type **play** to watch Corbie control his own game instance. **Austrian Avian Association** is selected automatically when AUAVIAN.wad is present in games; Freedoom Phase 2 is the fallback game.

This is an experimental local player, not a trained campaign solver. He uses ViZDoom's live image, visible-object labels, depth and player statistics for fast aiming, firing, strafing, supplies, door checks and stuck recovery. These are engine sensors, not screen OCR or the ordinary chat model. He has no full-map knowledge or learned route. Unknown mod actors may not be recognized, and complex switches, platforming, secrets and level completion will need better planning. No successful playthrough is claimed.

## Controls

- **play** opens the spectator window and starts his controller.
- **watch** opens it paused; **step** performs one decision and opens the resulting picture paused.
- **T**, **Enter**, or clicking the chat box opens backseat chat and pauses play. **Enter** or **Send** submits. **Esc** while composing leaves the message unsent and keeps play paused.
- **Space** outside the chat box pauses/resumes. **F8** always returns to the console; **Esc** does so when not composing. Switching away from the spectator window pauses him.
- **S** outside the chat box, or **save** in the console, writes a local checkpoint. **load** restores it. Saves are separated by base/mod content and map; they are independent of the manual arcade's saves.
- **status** prints observed health, armor, ammunition, kills, items, damage totals, last decision and available maps. It also saves last-frame.png and report.json in player-state. After death, last observed statistics are explicitly marked as previous.
- **avian** / **freedoom** changes games. **map MAP02** selects a new attempt on that map. **restart** starts the selected map over. These explicit commands discard unsaved current progress.
- **quit** closes only this player's game.

Input goes through ViZDoom's own game API; no Windows keyboard or mouse input is injected. Playback presents one game tick per frame, targeting Doom's 35 ticks/second; the controller normally replans every four ticks and immediately replans on observed damage. The window shows measured display FPS, health and armor. Slow machines slow the simulation instead of skipping pictures or catching up in a large input burst. Death, episode end and the twenty-minute episode limit stop action. Episode end never counts as beating the campaign. Moving to another map is currently manual, with fresh starting inventory; this is not continuous campaign progression. Save explicitly before quitting; there is no automatic checkpoint. F8 is polled while the spectator view is running, not during engine startup.

### Player v10: chat without constant pause reminders

The conversation prompt now treats the automatic typing pause as UI bookkeeping. Ordinary replies answer the human's message instead of prescribing pause/resume or repeating /help. A display guard removes unsolicited pause instructions, preserves useful remaining sentences, and uses observed game facts or a brief acknowledgement when the model supplied only boilerplate. Genuine controls questions still receive controls answers. Old assistant pause reminders are omitted from subsequent ordinary-chat prompts so they do not reinforce the loop.

All **117 Python checks pass**, including pause-only replies, useful answers with appended reminders, complaints about pause advice, praise, stuck-corner evidence, visible enemies, death, and immutable send-time snapshots through the asynchronous reply path. Native model quality remains unverified in this coding session. Close the entire console and reopen **Corbie-Doom-Player.cmd**; the title says **Player v10**. The desktop pet does not need rebuilding. Typing/focus pause behavior and explicit resume controls are unchanged.

### Player v9: see openings and narrow obstacles

Depth scanning now retains contiguous visible gaps and nine depth sectors. Exploration can turn toward a sufficiently wide opening instead of choosing solely from left/right averages. Tiny slits are ignored, and a conservative central reading keeps narrow obstacles from being averaged away. Combat, damage response, manual directions and corner recovery retain priority. Opening width is a screen fraction, not proof that the player's body fits; this remains a local reflex controller with no hidden map or route solver.

Reports and backseat chat include `lastDecisionSight`: previous decision depth sectors, visible openings and preferred gap, not a fresh full-map observation. All **108 Python checks pass**, including left/right openings, slits, central obstacles, floor/ceiling rejection, combat priority and the earlier collision/chat checks. Actual v9 navigation needs desktop verification. Close the entire console and reopen **Corbie-Doom-Player.cmd**; the title says **Player v9**.

### Player v8: keep playing through repeated collisions

Exhausting three corner-recovery plans no longer raises an exception or closes the spectator window. After the side steps and backtrack, Corbie tries short probes along different world headings, anchored to the remembered corner. Each probe has a finite duration and replans early when movement is blocked. Failure-heading history is capped at 12 entries per corner. Repeated damage does not cancel an advanced escape on every hit, and return-fire controls remain available.

This removes the collision-triggered stop; it does not guarantee a route out of every geometry trap. Death, episode end, invalid sensor data and explicit pause/focus/chat controls retain their existing behavior. Close the entire Doom console and reopen **Corbie-Doom-Player.cmd**, then type **play**. Look for **Player v8**. All 100 Python checks pass, including 1,200 game-frame calls with repeated simulated collisions; actual v8 navigation still needs a desktop try.

### Backseat chat retained from v7

The spectator has a scrollable chat sidebar with a text box, Send button and Ctrl+V paste. Opening the box pauses play. Submitting a message leaves play paused until **Space** outside the box or **/resume**. Direction receipts say what is queued, not that movement happened while paused. The conversation is kept in memory for this game session, capped at 64 messages; it is not saved to disk. F8 and reopening the view preserves chat, while changing games/restarting creates a fresh session.

These exact short messages apply real, temporary instructions without a model:

| Message | Effect when play resumes |
| --- | --- |
| **go left**, **go right** | Turn 90 degrees relative to the current view, then briefly try that direction if clear. |
| **turn around** | Turn 180 degrees, then briefly try moving forward if clear. |
| **back up** | Briefly move backward. |
| **find health** | Prioritize visible medicine for ten game seconds; full health does not start a hunt. |
| **shoot** | Focus on visible enemies for ten game seconds; critical health retains survival priority. |
| **auto** | Cancel the current backseat hint. |
| **/pause**, **/resume** | Pause or resume the current game. |
| **/help**, **/clear** | List directions or clear only chat history. |

Movement instructions expire after three game seconds at most, and blocked movement yields to normal corner recovery. They do not know unseen destinations or bypass collision. A question such as "Should I go left?" stays conversation rather than turning into an input command.

Ordinary questions such as "Why are you stuck?" use the existing local **gemma3:4b** model in Ollama. Corbie receives a bounded snapshot of player statistics, visible labels, route failures and his current decision, plus recent chat. Previous readings after death are labelled as previous. His focused character prompt keeps the dry, affectionate raven voice without chef shtick. This version supplies structured game readings, not a picture or full map. Replies are text only and cannot execute game actions; unsupported tactics are discussed rather than silently applied.

Local replies run on a background thread with bounded requests/history, so waiting does not block rendering or resume controls. Direct directions remain usable while a reply is pending or Ollama is unavailable. Requests go only to **127.0.0.1:11434**, ignore proxies, reject redirects and reject cloud-backed model metadata. Chat row wrapping is cached between changes to keep long conversations from slowing playback. The endpoint follows [Ollama's chat API](https://docs.ollama.com/api/chat).

Press **T**, type a message, and press **Enter**. No desktop pet rebuild is needed. V7's 96 Python checks passed and the actual panel renderer was inspected using a saved game frame. This coding session blocks the local Ollama socket and ViZDoom IPC, so generated replies and current gameplay need desktop verification.

### Route memory retained from v6

Failed approaches are remembered by nearby position and attempted movement heading for two game minutes. Forty units of back-and-forth no longer erases the failure. Four blocked movement decisions start recovery; a blocked backup skips ahead to a side step, and a blocked side step changes the plan early. Identical sensors cannot select the same escape three times: first try a side, next try the opposite side, then turn toward a previously visited position. With no suitable trail point, the third plan turns 180 degrees away instead. V8 replaces the old pause after three failed plans with continuing probes in varied directions.

During ordinary exploration, remembered blocked headings prompt a detour before another approach, including when a pickup would draw him back. Visible combat keeps priority, and recovery retains firing/weapon controls. Under-fire dodging cannot overwrite the selected opposite-side escape. The trail contains at most 64 points; at most 32 corner records are retained. Restarting/loading clears this session memory. Reports expose **failedRoutes**. This is local navigation memory from his own movement and depth view, not a full map or proof that a trail remains clear.

Existing game checkpoints remain compatible, and the desktop pet needs no rebuild. V6's 74 Python checks passed; its route memory is retained in v7.

### Recognition behavior retained from v5

The user's paused Freedoom MAP01 report showed **Zombieman**, a loaded pistol with 50 bullets, and zero fire-command ticks. The old allowlist contained **ZombieMan** and compared names exactly, so that enemy was ignored while Corbie pursued HealthBonus pickups. Names now match without case sensitivity, and visible labels retain ViZDoom's **Monster** category as an additional recognition path. Dead/Gore, Self and Player labels are excluded. Positive one- or two-pixel enemy boxes are accepted; invalid geometry is rejected. Category recognition is still an engine heuristic for custom mods, not a reliable friend-or-foe classifier for every mod.

Reports now keep visible label categories and boxes, a bounded set of previously observed labels, and **enemyVisibleTicks** alongside fire-command ticks. This evidence survives death so an empty final view cannot hide earlier recognition failures. The user confirmed that Player v5 is now shooting in the real game. This coding sandbox still cannot run ViZDoom's IPC.

### Firing behavior retained from v4

Firing now checks whether the visible enemy's bounding box crosses the horizontal aiming line, with a small tolerance, rather than demanding its centre fall inside a fixed narrow band. Retreat and all corner-recovery phases can return fire while aiming. Defensive side-running waits until a visible enemy is lined up, so it does not keep pulling the target away from his aim.

The controller reads the selected weapon and owned classic Doom weapon slots/ammunition. Fists/chainsaws in slot 1 can attack nearby enemies with zero ammo; distant enemies prompt an approach when he is not defending. A melee or empty weapon selects an owned loaded gun directly, with a one-second retry interval to allow it to equip. Gun selection favors ordinary bullet/shell weapons, avoids close-range rocket selection and requires 40 cells for a BFG. If all owned guns are dry, he can switch to melee. This is a classic Doom slot heuristic; custom weapons and the shotgun/super-shotgun sharing a slot still need more detailed handling.

The window shows weapon slot, ammo and combat status. Reports include owned weapons, ammo by slot, engine attack-ready state and **attackInputTicks**: ticks when the fire command was sent, not confirmed shots or hits. No desktop pet rebuild is needed. Existing checkpoints remain compatible.

### Damage behavior retained from v3

Health and armor losses are sampled every game tick and interrupt held movement immediately. A hit starts two seconds of evasive movement; repeated hits, a large health loss or low health extend that to three seconds after the latest hit. He backs away from visible enemies while aiming and firing when aligned, and moves while scanning if the attacker is unseen. Below 40 health he disengages even without a fresh hit. Below 55 health, visible medicine ahead gets priority when the depth view is clear and no visible enemy overlaps its path. These are local heuristics, not a guarantee of cover or a safe route behind him. Each recovery plan is bounded in duration and changes when movement fails, including when medicine is unreachable.

Healing and the first observation do not count as damage. Loading a checkpoint clears damage memory. Reports retain observed hit totals and the latest hit, plus explicitly previous stats after death. The spectator shows health, armor and the current defensive decision.

### Player v2: slideshow and circle fixes

The initial viewer displayed only five updated pictures per second, advancing seven ticks between them. It now advances/renders individual ticks and reuses its scaling surface. Turns are capped at 2.5 degrees per tick, aiming has a dead zone, and weapon/use commands pulse once per decision. The narrower depth sample avoids floor/ceiling pixels; ordinary room distances no longer trigger immediate wall avoidance. Exploration walks straight through clear space and obstacle turns commit to one side. The old three-attempt/40-unit recovery reset has been replaced by v6's remembered approaches and distinct escape plans. Depth thresholds are heuristic and may need further tuning from actual game evidence.

Existing game checkpoints remain compatible with Player v8; **load** restores one with a fresh controller state, including cleared backseat hints. The desktop bird needs no rebuild.

## The bird mod

Your **AUAVIAN.wad**, **AAA_WS.wad**, AUAVIAN.txt and README.txt were found in games. The mod's actual title is *Austrian Avian Association* by MundyC. Its readme specifies Doom II and a Boom-capable engine, with DSDA-Doom, Woof! and GZDoom tested by the author. It identifies single-player maps 01-09, 13-16 and 31-32; deathmatch and placeholder maps are excluded from Corbie's player choices.

Both launch modes use **freedoom2.wad** as the free Doom II-compatible base unless you put your own **doom2.wad** in games. Freedoom supplies alternative assets; it is not the original Doom II campaign. The main mod and optional widescreen patch are loaded in that order. **AAA_MIDI.wad is not loaded**, preserving the mod's original soundtrack. Originals and author credits remain unchanged. The author's accompanying license is CC BY-NC-SA 4.0; see AUAVIAN.txt for details.

The autonomous player uses **ViZDoom 1.3.0**, which is based on ZDoom. The first user run loaded this mod and produced a game picture and report with movement, two collected items and 2,299 game ticks; combat and complete levels remain unverified. The separate human arcade uses **Woof! 16.0.0** for this mod and **Crispy Doom 7.1.0** for classic Doom/Freedoom. These are separate engines and save formats.

## Installation and manual arcade

The Python environment and ViZDoom package are installed under **agent-env**. **Setup-Corbie-Doom-Player.cmd** recreates that isolated environment without changing system Python packages. Python 3.12 and its pip are required; setup checks the usual installed path before trying Python on PATH.

For playing yourself, open **Corbie-Doom-Arcade.cmd**. Choose **Austrian Avian**, then **Play**. To use an original Doom II base outside games, select it with **Classic Doom...**, then choose **Austrian Avian**. Classic Doom... also switches back to an unmodified base game. The manual arcade attempts to embed only its own launched process; **Separate window** detaches it. Closing it asks before stopping an unsaved game.

The official Woof!, Crispy Doom and Freedoom downloads are installed locally and passed SHA256 verification. The arcade's install button adds the appropriate missing engine. **Setup-Corbie-Doom.cmd** installs the classic starter; doom/Setup-Avian.ps1 installs the mod's Woof! engine. Archives are checked for unsafe paths and extraction limits. No administrator access is needed. Existing user mods and game settings are retained; existing partial engine folders become backups.

## Storage

- **games/**: base WADs, user-provided mod and credits.
- **runtime/**, **woof-runtime/**: portable manual-play engines and their bundled licenses.
- **agent-env/**: isolated Python dependencies and ViZDoom's engine.
- **player-state/CONTENT_HASH/MAP/**: autonomous-player checkpoints, settings, latest evidence image/depth array and report.
- **state/CONTENT_HASH/**: manual arcade settings/saves, separated by base and mod content.
- **choice.json**: the manual arcade's selected base/profile.
- **installed.json**, **avian-installed.json**: verified portable download receipts.

Machine-specific files and game data are ignored by Git. Keep the complete doom folder beside the launchers. Build-Doom.ps1 rebuilds the small manual host/CLI launcher using the Windows .NET Framework already installed. Python sources reload when the player console restarts; the desktop bird executable needs no rebuild.

## Verification and desktop check

**100 Python checks pass** after v8. Collision regressions cover continuing after three failed plans, varied world headings, bounded failure history, probe completion, escape continuity under damage, unreachable medicine and repeated calls through the game-input loop. Earlier chat, combat, survival, route memory and actual spectator-rendering checks still pass. Run **doom/agent-env/Scripts/python.exe -B -m unittest discover -s doom -p "test_*.py"** from RowanPet. The previous **145 Node checks** passed; Node launch routing is unchanged by v8. Native C# checks previously passed. Controlled tests and a visual panel preview pass, but actual local-model replies and v8 gameplay need desktop verification. A completed map or campaign remains unverified.

The real Crispy/Freedoom starter demo completed 7,117 ticks. Woof loaded the actual Avian base/mod/widescreen files and initialized its renderer/play loop, but its demo test rejected the mod's demo format; that is not evidence of a completed mod simulation.

This coding sandbox blocks ViZDoom's interprocess message queues with Access denied. The user's initial run established actual mod loading/movement but revealed spinning and slideshow playback. Post-fix frame rate, navigation, combat, audio and checkpoint round trips still need desktop verification. **doom/Check-Player.cmd** runs a bounded 150-decision check on the desktop and prints the observed result, saving evidence under player-state. Share any failure message. Normal launch catches startup failures and keeps the console open.

Sources: [ViZDoom Windows installation](https://vizdoom.farama.org/introduction/python_quickstart/), [game API](https://vizdoom.farama.org/api/python/doom_game/), [1.3.0 weapon/slot sensor implementation](https://github.com/Farama-Foundation/ViZDoom/blob/1.3.0/src/vizdoom/src/viz_game.cpp), [visible-object example](https://github.com/Farama-Foundation/ViZDoom/blob/master/examples/python/labels_buffer.py), [Woof! 16.0.0](https://github.com/fabiangreffrath/woof/releases/tag/woof_16.0.0), [Crispy Doom 7.1](https://github.com/fabiangreffrath/crispy-doom/releases/tag/crispy-doom-7.1), [Freedoom 0.13.0](https://github.com/freedoom/freedoom/releases/tag/v0.13.0). Mod requirements and credits come from the author's local AUAVIAN.txt and README.txt.
