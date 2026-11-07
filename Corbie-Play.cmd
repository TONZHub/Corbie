@echo off
cd /d "%~dp0"
node "%~dp0game-agent\play.mjs"
if errorlevel 1 pause
