@echo off
cd /d "%~dp0"
if not exist "%~dp0doom\Corbie-Doom.exe" (
  powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File "%~dp0doom\Build-Doom.ps1"
  if errorlevel 1 (
    pause
    exit /b 1
  )
)
start "Corbie plays Doom" "%~dp0doom\Corbie-Doom.exe" --player
