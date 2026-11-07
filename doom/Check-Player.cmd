@echo off
cd /d "%~dp0"
"%~dp0agent-env\Scripts\python.exe" -B "%~dp0player.py" --headless --steps 150
if errorlevel 1 echo Player check failed. Please share the message above.
pause
