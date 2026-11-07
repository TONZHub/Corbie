# Corbie plays Princess Maker 2 Refine

Goal: finish a playthrough alive and reach any adulthood ending. This is an experimental local player. Dialogue, opening the calendar and filling two Farm periods worked live; starting the month and a full playthrough remain unverified.

## Decision and feedback upgrade

### Read and review game stats

Corbie can now propose a solver snapshot from actual game pictures, using explicit labelled OCR plus the existing local vision model. Scans never click, arm gameplay, or update the solver automatically. The five body measurements under Standard diet are excluded from stat extraction; unknown values remain missing. Image-only readings are flagged for review, and conflicting readings block acceptance.

1. At home before scheduling the next month, open a stats page in Refine yourself. Type **solver scan** in Corbie-Play.cmd. It focuses Refine and captures only the game window; Ctrl+C cancels reading.
2. Open other stats/profile pages and repeat **solver scan** to collect the remaining fields in the same readable game month. **solver review** shows every proposed field and the latest source-picture path. Source pictures are saved locally in session/stats-pictures.
3. Use **solver set stamina=120 stress=10** to correct numbers; **solver set birth_month=1 blood=A sign=capricorn diet=normal** fills metadata when verified manually. Supported Refine stat aliases are accepted. A changed year/month clears old draft values; other missing values are never filled from a default daughter.
4. Once every value is reviewed and these are the actual **start-of-month** stats, type **solver accept**. This validates the complete snapshot through the Python solver before replacing its current observations. Missing/conflicting fields block acceptance. The accepted Attack value is the displayed value; the solver performs its internal Strength adjustment once.
5. Use **solver plan** to preview advice, then resume ordinary play. Re-scan/review actual stats next month rather than advancing the simulation as though it were the game.

If a page hides the calendar, **solver scan same** explicitly attests that it belongs to the draft's existing game month. Use it only when that is true. **solver draft clear** removes the draft while preserving accepted solver stats and source pictures. Imported JSON remains available through solver load.

The session keeps stats-review.txt, stats-draft.json, accepted-reading records and stats-notebook.md. Reviewed values for the next month are compared with a matching prior forecast's gold/stress, while noting that execution of the proposed schedule was not verified and other events can affect the difference. Forecasts never overwrite observations or automatically change the simulator's parameters. Image extraction remains fallible; live stats-page reading and a complete playthrough have not been verified in this coding session.

### pm2-solver adviser

Corbie can use a separately supplied **pm2-solver** package. See solver/INTEGRATION.md for where to place it. Job figures reach his strategy decisions immediately. Optional numerical forecasts require actual start-of-month stats; no example daughter is loaded automatically.

In the game console:

1. **solver** shows adviser status and commands.
2. **solver template** creates a JSON form with unknown values left null. Fill the form with the game's current date, age, money, birthday/sign/blood/diet and 22 visible stats. Use the solver's canonical names or supported Refine stat aliases. No `version` field means Attack is entered as displayed in the game.
3. **solver load "C:\\path\\stats.json"** validates and stores your actual snapshot. Missing visible stats are rejected; unknown experience/hidden-data defaults are disclosed.
4. **solver plan** previews a three-period survival schedule without game input. **solver plan queen**, **solver plan scholar**, or another supported target uses the original ending planner for one month. **solver plan survival** returns to finishing alive with any ending.
5. **solver info farm** (or church, inn, protocol, etc.) shows the underlying guide figures. **solver clear** removes loaded stats and the selected target.

During play, the adviser supplies forecasts only when the current readable game month matches the snapshot and any independently read gold/age/stress agrees. A mismatch withholds the forecast and requests fresh stats; job reference remains available. Projections never overwrite observed stats or count as successful work, income, victories or an ending. Current controls and health checks still take priority. October festival handling and uncalibrated lessons are not automatically enabled by the solver. The small model still chooses from current controls; this does not establish a reliable full playthrough.

Close and reopen Corbie-Play.cmd to load the integration. Python 3 is required, without extra packages. See **solver/INTEGRATION.md** for the bridge and test details. Use your own local copy of the optional solver.

### Local strategy guide

The unconditional Farm selection and fixed Work/Work/Rest pattern have been removed. Normal period/job choices now use the local image model with the current picture, a bounded excerpt from **strategy-guide.md**, any imported notes, and recent confirmed job-menu choices. The starter guide summarizes income/development/stress tradeoffs with source links; its planning policy prioritizes survival and current evidence. Repeating Farm is allowed when justified, rather than forbidden solely for variety. A menu click is never counted as completed work or earned wages.

In Corbie-Play.cmd:

- **guide** shows the starter file and whether personal notes are loaded.
- **guide load "C:\\path\\my-pm2-notes.md"** copies a local UTF-8 `.md` or `.txt` guide into the session. It accepts up to 256 KB, selects relevant excerpts for the small model and keeps them across restarts. URLs and PDFs must be saved/exported to text first. Nothing is uploaded or used to train a model.
- **guide clear** removes imported notes and keeps the starter guide active.
- Edit **strategy-guide.md** directly to change the starter advice. Both files reload at the next strategic decision. **why** shows the resulting reason.

Ordinary dialogue, Add to schedule confirmations, urgent recovery and Run schedule still use recognized menu mechanics. Confirmation rules now handle all six initially calibrated jobs, rather than Farm alone. Strategy choices require a local model call, so they can take longer than the previous fixed rule. The guide cannot create controls, enable uncalibrated lessons/adventures, or override input safeguards. Automated tests validate the connection and imports; effective use of the advice by the small model still needs live verification.

The final "This is the schedule for this month" prompt now selects the visible **Run schedule** option directly. That label was missing from the click filter; it is now included, and its adjacent Cancel is excluded from automatic clicks. Other cancellation menus remain available. Tests cover the observed prompt/labels, final-confirmation grounding and preserving cancellation elsewhere. The new month-start click still needs live verification.

Use **esc** (or **escape**) in the game console to send one Escape to Refine, capture the result and pause. This command uses the game's existing focus/F8/fresh-observation checks and does not need Ollama. `back` still exits the game console. The planner also has explicit Escape guidance for leaving option submenus; unchanged keypresses remain blocked in that scene.

Click targets now pass a controller-enforced menu-label filter before planning and again before input. Calendar/HUD text, character names, measurements and dialogue prose remain readable but cannot become click targets. Unknown menu labels need calibration rather than arbitrary text clicks. This improves input reliability but does not fix every reasoning error in the small local models.

Ordinary detected dialogue now sends **Enter once** rather than clicking its arrow, then checks the resulting picture. Image-based planning can also choose Enter for a clearly visible narrative box whose marker was missed. Choice menus remain separate decisions. An unchanged Enter is blocked in that scene, so the controller cannot hammer the key indefinitely. Restart the console to load this change; the native helper already supports Enter and needs no rebuild.

GameWindow-v6 matches the triangle's interior instead of surrounding portrait/background pixels. The saved salary and short daughter replies both resolve to their correct continue target; the saved rest menu has no false marker. Short dialogue is no longer excluded by a twenty-character minimum. Unknown screens and repeated waits use image interpretation; detected question/choice menus cannot request wait. The observed pocket-money prompt directly declines the unpriced optional expense using current text bounds. Restart the console to load the new helper and planner.

The November 20 salary dialogue uses an elevated portrait textbox, outside the former arrow-search region. GameWindow-v5 searches the main game area and includes that observed marker style. Ordinary narrative with a detected continue marker advances directly; questions, choices and possible ending/save text require image-based interpretation. The salary notice's rhetorical question is recognized only with explicit salary and payment text. The saved salary screenshot selects its actual arrow; a saved rest menu correctly produces no dialogue marker. These are offline recognition checks, not proof of the resulting live click.

### Monthly conversations and castle visits

At home, after seasonal clothing checks, Corbie prioritizes a daughter conversation and then a town/castle visit through uniquely recognized Talk/Castle/Go out/Go to town controls. The previous routine silently fell through to scheduling when OCR missed the ornate calendar year/month. It now asks the local vision model to read the focused current-calendar crop if needed, validates the result, rejects conflicts with readable calendar text and shares the reading with seasonal clothing checks. The upcoming scheduling calendar is not treated as the current date.

An unresolved date, missing/ambiguous home control or blocked home-check click now pauses with a specific explanation instead of skipping ahead to work. During an active social task, Schedule is excluded from model click choices. The conversation or audience menu uses local image-based planning, with instructions to choose a gentle check-in, inspect available audiences, return after refusal and avoid assuming access to royalty. Exact menu labels still need live calibration; no fixed coordinates are invented.

Attempts are recorded before input, persisted across restarts and limited to one of each kind per observed month. Leaving home and returning records either observed dialogue or an unconfirmed visit; neither implies a relationship/reputation increase. Each active task has a twelve-input cap. Daughter dialogue is kept as bounded OCR quotes; an explicit tiredness statement can prioritize rest in that same readable month. `status` and the notebook show the social record. `watch` previews without recording an attempted visit.

These routines have automated coverage but have not yet been calibrated against this installation's daughter/castle screens. Restart Corbie-Play.cmd, return to the home menu and run watch for the first live check. Automatic audiences are not yet demonstrated end to end.

The second Farm block stalled at its introduction because the dialogue marker's dark animation frame was not recognized. GameWindow-v4 matches both captured marker appearances. The exact farm-introduction caption plus a currently detected marker now selects Next dialogue directly. The saved November 11 screenshot validates this detection; advancing the live game still needs confirmation. A separate twelve-consecutive-wait limit prevents OCR fluctuations from resetting the wait guard indefinitely.

### Seasonal wardrobe

Corbie now records summer/winter clothing seen on explicitly labelled inventory/equipment screens and distinguishes ownership from explicit equipped labels. Shop stock and dialogue mentions do not establish ownership. These sightings appear in status and the notebook; absence from a partial list never proves an item is missing.

At home in June–August or December–February, a readable game calendar triggers an inventory check before scheduling. It uses only a unique visible inventory/items/equipment control, otherwise pauses for you to open that screen. A verified relevant garment satisfies that month's check for the current run. A new month, shop visit or restarted run requires rechecking. Unreadable dates remain unknown. Inventory and weather/clothing conversations use the image model so it can inspect visual context; prompts prioritize responding to seasonal clothing offers over scheduling. Equipping and shopping remain model-guided and need live calibration, not fixed-coordinate automation.

Initial seasonal assumptions follow the [Refine player discussion](https://steamcommunity.com/app/523000/discussions/0/135509823659051881/); current game prompts take precedence. The first real inventory capture is still needed to verify labels in this installation.

The player now checks the screen after each move, blocks unchanged clicks in that scene, retries a bad reading once on a fresh picture and records uncertain input without repeating it. Sprite animation alone does not count as menu progress when readable controls exist. A changed screen is evidence of a change, not proof the intended task succeeded.

Use **why** for the last decision, **history** for recent moves and observed outcomes, **status** for progress, and **resume** to continue. The local session folder now includes a readable **session.md** notebook, **observation.json**, and **before-frame.png** alongside the latest picture. These are not game saves.

Unfamiliar menus use a separate Gemma age/gold reading, followed by the installed local **qwen3:4b-instruct** choosing one move from current control IDs. Gemma handles image decisions if Qwen is unavailable and always checks ending pictures. Both models must be local; nothing is downloaded automatically. Identical status images reuse cached readings. Date remains unknown rather than guessed. Familiar scheduling menus skip model inference and respond to explicit current illness or stress of at least 60 by prioritizing recovery.

The saved rest-menu picture passed a real local inference check with menu rules disabled: **age 10, 500G, Free time**, in approximately 19 seconds. Close and reopen the game console for this update.

## Start

1. Open **Corbie-Play.cmd**, or use **/game** inside Corbie's CLI.
2. Type **open** to launch the installed Steam copy of Refine. Prepare a playthrough yourself in the game, then return to Corbie.
3. Type **watch** for a screen-reading preview. It brings Refine forward and describes the next proposed move without game input.
4. Type **step** to try one move. Corbie then pauses and saves a picture of the result. Once the reading and click accuracy are correct, type **play** for the read → decide → one input → read loop.
5. **F8 pauses input**, including while Corbie is thinking. Switching to another app also disarms the controls. The terminal reports the pause after the current model request finishes. Ctrl+C cancels that request. Type **play** again to resume. **back** returns to ordinary CLI chat.

Keep Refine visible and unobscured. The adapter matches the installed executable path, so similarly named apps cannot receive clicks. The default installation is:

    C:\Program Files (x86)\Steam\steamapps\common\Princess Maker 2 Refine\pm2.exe

The mode supports this Windows Steam Refine executable. Other editions, emulator windows, translations and resolutions have not been calibrated. The exported runGameConsole function accepts an explicit alternative executable path; no arbitrary process search is exposed to the model.

## What he does

- Uses existing downloaded models in local Ollama as described above. Only the game's client area is captured. Cloud-hosted models are rejected.
- Reads visible money, stress, health, age and date when possible. Enlarged status/menu/calendar/dialogue crops and local Windows text recognition help with small text. Unreadable fields stay unknown. Templates from the observed Refine interface locate its red next-dialogue marker and home Schedule button afresh on each picture; missing matches supply no suggested coordinate.
- Aims for suitable paid work, inexpensive rest and recovery, and advancing the schedule. Period and job choices use the local strategy guide and current image instead of a fixed farming routine. Explicit illness, stress of at least 60 or an observed tiredness report can still force recovery in the recognized period menu. Recognized job/free-time confirmations and the completed schedule use direct current-screen rules. This is not a proven eight-year strategy.
- Makes one click or allowed game keypress at a time. A click's named target must resolve to a current image match or uniquely recognized text label. The resolved position replaces the model's coordinates. Missing or ambiguous labels pause; repeating a remembered label never invents a location. Text recognition locates labels, while the planner still decides which are appropriate controls. Click coordinates are normalized to the client area; resizing/moving the window or a changed target requires a fresh observation.
- Pauses on title/setup, save/load screens, suspected overwrite actions, death, low confidence, unknown screens, stale observations, lost focus, adapter errors, or repeated non-progress.
- Does not automatically save or overwrite a slot. The human handles saving and resumes afterward. Corbie's journal is not a game save.
- Requires explicit adulthood ending evidence in two fresh pictures before marking the run complete. A birthday, festival, death, menu or a mere age reading does not count. This is model-based recognition; saved ending pictures let the human verify its conclusion.

## Progress and limitations

The local **game-agent/session** folder keeps the latest game picture, a small progress journal, action records and any detected ending evidence. It is excluded from Git. Restarting the CLI reads the journal but always observes the current game again before acting. Each run is capped at 1,500 observations; reaching the limit pauses without claiming completion.

The small local vision model can misread text or choose imprecise coordinates. First watch a real screen and supervise the initial moves. Interactions with animated menus may pause conservatively. There is no guaranteed route, automatic save-state recovery, adventure combat controller, or tested complete playthrough yet.

The GameWindow-v6.exe adapter is compiled from GameWindow.cs and FrameVision.cs using game-agent/build.ps1, with dialog-next.png and schedule.png beside it. The console builds it on first use if missing. The desktop pet does not need rebuilding. After an update, close and reopen Corbie-Play.cmd so its console and helper load the new code.

Scheduling rules in schedule.mjs reload when their saved version changes, allowing further menu calibration in the same console. Updates to the main player or native adapter still need a restart. Rules always inspect the fresh frame and do not reuse a remembered button position.

## Verification

All **131 CLI, MCP, world and game tests pass**. They cover current-picture targets, decision validation, bounded reading recovery, before/after records, unchanged-click blocking, uncertain input, F8/focus checks, local model routing, HUD caching and two-image ending confirmation. The existing adapter compiled successfully. Dialogue, opening the calendar and two Farm periods worked live. The remaining rest confirmation, month start and a full playthrough need live verification: this coding session cannot directly access the user's desktop game.

Sources: the [official Refine listing](https://store.steampowered.com/app/523000/Princess_Maker_2_Refine/) describes the eight-year raising period; [Ollama vision](https://docs.ollama.com/capabilities/vision) and [structured output](https://docs.ollama.com/capabilities/structured-outputs) document the local model request format. The low-risk scheduling policy is a heuristic, not an official guaranteed winning guide.
