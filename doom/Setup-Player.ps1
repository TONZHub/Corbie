$ErrorActionPreference='Stop'
$python=Join-Path $env:LOCALAPPDATA 'Programs\Python\Python312\python.exe'
if (-not (Test-Path -LiteralPath $python)) {
  $python=(Get-Command python.exe -ErrorAction Stop).Source
}
$environment=Join-Path $PSScriptRoot 'agent-env'
$tempPath=Join-Path $PSScriptRoot '.stage-python'
New-Item -ItemType Directory -Path $tempPath -Force | Out-Null
$env:TEMP=$tempPath; $env:TMP=$tempPath
& $python -m venv --without-pip $environment
if ($LASTEXITCODE -ne 0) {throw 'Could not create the local Python environment.'}
& $python -m pip --python (Join-Path $environment 'Scripts\python.exe') install --only-binary=:all: --no-cache-dir 'vizdoom==1.3.0'
if ($LASTEXITCODE -ne 0) {throw 'ViZDoom install failed.'}
Write-Output 'Corbie Doom player is ready. Open Corbie-Doom-Player.cmd.'
