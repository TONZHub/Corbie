@echo off
cd /d "%~dp0"
where node >nul 2>nul
if errorlevel 1 (
  echo Corbie needs Node.js to open this terminal.
  pause
  exit /b 1
)
node "%~dp0corbie-shell.mjs"
if errorlevel 1 pause
