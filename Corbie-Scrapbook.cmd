@echo off
cd /d "%~dp0"
where node >nul 2>nul
if errorlevel 1 (
  echo Corbie needs Node.js to open his scrapbook.
  pause
  exit /b 1
)
node "%~dp0corbie-scrapbook.mjs" --gallery
if errorlevel 1 pause
