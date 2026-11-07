@echo off
cd /d "%~dp0"
if not exist "%~dp0doom\agent-env\Scripts\python.exe" (
  echo Run Setup-Corbie-Doom-Player.cmd first.
  pause
  exit /b 1
)
"%~dp0doom\agent-env\Scripts\python.exe" -B "%~dp0doom\player.py"
if errorlevel 1 pause
