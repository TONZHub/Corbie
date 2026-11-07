@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -File "%~dp0doom\Setup-Player.ps1"
if errorlevel 1 echo Setup failed. Check the message above.
pause
