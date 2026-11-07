# Corbie

Corbie is a Windows desktop raven. He perches, flies, collects shinies, and performs skits. His Nest includes Viewer Mail, a scene director, music reactions, and optional local voice and vision.

**Source snapshot: v0.115.** This is the portable repository copy of RowanPet.

The [Android companion](android/README.md) has an interactive Pocket Nest, a home-screen widget, and an optional floating raven with offline speech.

## Build and run

Use 64-bit Windows with .NET Framework 4.8 or later. Run these commands from the repository root in Windows PowerShell:

```powershell
.\Restore-Dependencies.ps1
.\build.ps1
.\Launch-Latest-Corbie.ps1
```

The first command restores the pinned build dependencies from NuGet. The build generates the icon from its XAML source and produces `Corbie-v0.115.exe` beside the launcher. If an older Corbie is already running, close it before testing a fresh copy. Corbie's normal runtime settings are shared under `%LOCALAPPDATA%\Corbie`.

For features beyond the desktop bird, see [optional setup](docs/OPTIONAL-SETUP.md). See [dependencies and attribution](docs/DEPENDENCIES.md) for package versions.

## Check a build

```powershell
.\Test-Corbie.ps1
```

Run on a Windows desktop session. The checks use synthetic data for startup, the icon, screen-reader lifecycle, version handoff, the scene director, and Viewer Mail. They do not require an Ollama model, a microphone, or account credentials. Reports go into `.test-results/`.

For the CLI and browser unit tests:

```powershell
npm ci --prefix browser-agent
$tests = @(Get-ChildItem -File -Filter '*.test.mjs') + @(Get-ChildItem browser-agent -File -Filter '*.test.mjs')
$testPaths = $tests.FullName
node --test @testPaths
```

The optional PM2 solver integration tests require your local solver package. See [game-agent setup](game-agent/README.md).

## Layout

- `src/Desktop/` — WPF desktop source and native checks.
- `assets/`, `motion/`, `habitat/` — original icon source, authored poses and habitat pages.
- `browser-agent/` and the root CLI modules — browser, Postmark, and local CLI tools.
- `doom/` and `game-agent/` — optional game adapters and setup scripts.
- `docs/` — setup and dependency notes.

Build output, local settings, browser profiles, downloaded models and games, FlyWire datasets, private captures, and the original working history are excluded. The [roadmap](ROADMAP.md) tracks the next project ideas.
