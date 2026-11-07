param(
  [string]$Python,
  [string[]]$Voices=@('en_US-ryan-medium','en_GB-northern_english_male-medium'),
  [string]$Select='en_GB-northern_english_male-medium'
)
$ErrorActionPreference='Stop'
if(-not $Python) {
  $launcher=Get-Command py -ErrorAction SilentlyContinue
  if($launcher) { $Python=(& $launcher.Source -c 'import sys; print(sys.executable)' | Select-Object -Last 1) }
  else { $launcher=Get-Command python -ErrorAction SilentlyContinue; if($launcher) { $Python=$launcher.Source } }
}
if(-not $Python -or -not (Test-Path -LiteralPath $Python)) { throw 'Python was not found. Run this script with -Python followed by the full path to your Python executable.' }
& $Python -c 'import piper'
if($LASTEXITCODE -ne 0) { throw 'Piper is not installed in this Python. Install piper-tts using this interpreter first.' }
$voiceDir=Join-Path $PSScriptRoot 'voices'
New-Item -ItemType Directory -Path $voiceDir -Force | Out-Null
foreach($voice in $Voices) {
  & $Python -m piper.download_voices $voice --data-dir $voiceDir
  if($LASTEXITCODE -ne 0) { throw "Voice download failed: $voice" }
  $downloaded=Join-Path $voiceDir ($voice+'.onnx')
  if(-not (Test-Path -LiteralPath $downloaded) -or -not (Test-Path -LiteralPath ($downloaded+'.json'))) { throw "Voice files are incomplete: $voice" }
}
$model=Join-Path $voiceDir ($Select+'.onnx')
if(-not (Test-Path -LiteralPath $model)) { throw "Selected voice was not downloaded: $Select" }
$tone='Bird'
$settingsPath=Join-Path $PSScriptRoot 'piper.json'
if(Test-Path -LiteralPath $settingsPath) { try { $old=Get-Content -LiteralPath $settingsPath -Raw|ConvertFrom-Json; if($old.Tone){$tone=$old.Tone} } catch { } }
@{ Python=$Python; Model=$model; Voice=$Select; Tone=$tone } | ConvertTo-Json | Set-Content -LiteralPath $settingsPath -Encoding UTF8
Write-Output "Piper voice pack ready. Selected: $Select. Restart Corbie and enable Voice on."
