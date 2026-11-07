@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File "%~dp0doom\Setup-Doom.ps1"
if errorlevel 1 (
  echo Setup did not finish. Your existing saves and game files are untouched.
)
pause
