@echo off
cd /d "%~dp0"
if not exist "%~dp0doom\Corbie-Doom.exe" (
  powershell.exe -NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -File "%~dp0doom\Build-Doom.ps1"
  if errorlevel 1 exit /b 1
)
start "Corbie's Arcade" "%~dp0doom\Corbie-Doom.exe"
