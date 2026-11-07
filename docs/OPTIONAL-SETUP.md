# Optional integrations

The desktop bird builds and runs without these extras. Use the controls in Corbie's Nest to enable the features you want.

| Feature | Local setup |
| --- | --- |
| Local screen comments, chat and Viewer Mail | Install Ollama and the downloaded `gemma3:4b` model. Corbie connects to `127.0.0.1:11434`. Run `Start-Corbie.ps1 -InstallModel` to start the local server and pull that model. |
| Voice | Install Python and `piper-tts`, then run `Setup-Piper.ps1`. Voices and generated `piper.json` are local. |
| Microphone | Run `Setup-Whisper.ps1` to retrieve the Whisper runtime and model. |
| Browser and CLI tools | Use Node.js 22 or later. Run `npm ci --prefix browser-agent`, then `Corbie-CLI.cmd` or the Browser controls in the Nest. Browser profiles remain local. |
| Postmark | The CLI uses Corbie's public character record. An authorized key is required for actions that use a resident account. The PowerShell launchers can prompt for it for the current session. |
| Doom | See [Doom setup](../doom/README.md). The repository includes Corbie's adapter and setup scripts; acquire the engine and game data through the setup flow. |
| Princess Maker 2 | See [game agent setup](../game-agent/README.md). Use your own game install. Optional pm2-solver setup is described [here](../game-agent/solver/INTEGRATION.md). |
| Mining | Configure your own XMRig install and public payout address in the Nest if you choose to use it. Mining tools, logs, and configuration are local. |
| Calendar, eBird, Checkiday and VirusTotal | Configure your own feeds or keys from the relevant Nest panel. |

## FlyWire motion circuit

The nervous-system source and importer are included. The FlyWire source tables and derived circuit file are local inputs. With the matching connection and cell-type tables from the [FlyWire Codex](https://codex.flywire.ai/), run this from the repository root after building:

```powershell
$exe = .\Launch-Latest-Corbie.ps1 -PrintTarget
$dir = Join-Path $PWD 'brain-data\fafb-v783'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
# Place connections_princeton.csv.gz and consolidated_cell_types.csv.gz in $dir first.
Start-Process -FilePath $exe -ArgumentList '--fly-build brain-data/fafb-v783/connections_princeton.csv.gz brain-data/fafb-v783/consolidated_cell_types.csv.gz brain-data/fafb-v783/motion-circuit.json' -WorkingDirectory $PWD -WindowStyle Hidden -Wait
```

Check for `motion-circuit.json` before enabling fly nerves. An import error is written next to that file. The habitat pages are included under `habitat/`.

## Sign-in and desktop launch

Run `Set-CorbieStartup.ps1` for a per-user sign-in shortcut, or `Set-CorbieDesktopMenu.ps1` for **Launch latest Corbie** in the desktop context menu. Windows may put the entry under **Show more options**. Both scripts support `-Action Status` and `-Action Disable`. Run them from the new folder only if you want the Windows shortcuts to point there.
