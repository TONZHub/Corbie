param([switch]$InstallModel, [switch]$ServerOnly, [switch]$AtLogin)
$ErrorActionPreference='Stop'
$petPath=if(-not $ServerOnly) { & (Join-Path $PSScriptRoot 'Launch-Latest-Corbie.ps1') -PrintTarget }
if($AtLogin) {
  if($InstallModel -or $ServerOnly) { throw 'AtLogin starts the pet only; use model/server options separately.' }
  if(-not (Test-Path -LiteralPath $petPath -PathType Leaf)) { throw "Corbie executable is missing: $petPath" }
  # Ollama has its own sign-in entry. The bird can appear while that service warms up.
  Start-Process -FilePath $petPath -WorkingDirectory $PSScriptRoot
  return
}
$ollamaPath=Join-Path $env:LOCALAPPDATA 'Programs\Ollama\ollama.exe'
if (-not (Test-Path -LiteralPath $ollamaPath)) { throw 'Install Ollama first, then run this launcher again.' }
try { $null=Invoke-RestMethod 'http://127.0.0.1:11434/api/version' -TimeoutSec 2 } catch {
  $env:OLLAMA_HOST='127.0.0.1:11434'
  $env:OLLAMA_NO_CLOUD='1'
  Start-Process -FilePath $ollamaPath -ArgumentList 'serve' -WindowStyle Hidden | Out-Null
  $ready=$false
  for($attempt=0;$attempt -lt 20;$attempt++) {
    Start-Sleep -Milliseconds 500
    try { $null=Invoke-RestMethod 'http://127.0.0.1:11434/api/version' -TimeoutSec 1; $ready=$true; break } catch { }
  }
  if(-not $ready) { throw 'The local vision service did not start. Open Ollama and try again.' }
}
if($InstallModel) {
  Write-Output 'Downloading Gemma 3 4B for local vision (about 3.3 GB)...'
  $null=Invoke-RestMethod 'http://127.0.0.1:11434/api/pull' -Method Post -ContentType 'application/json' -Body '{"model":"gemma3:4b","stream":false}' -TimeoutSec 3600
  Write-Output 'Vision model ready.'
}
if(-not $ServerOnly) { Start-Process -FilePath $petPath -WorkingDirectory $PSScriptRoot }
