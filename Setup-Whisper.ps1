param(
  [string]$Release='v1.8.3',
  [ValidateSet('tiny.en','base.en','small.en')][string]$Model='small.en'
)
$ErrorActionPreference='Stop'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
$root=$PSScriptRoot
$whisperDir=Join-Path $root 'whisper'
$runtimeDir=Join-Path $whisperDir 'runtime'
$modelDir=Join-Path $whisperDir 'models'
$staging=Join-Path ([IO.Path]::GetTempPath()) ('corbie-whisper-setup-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runtimeDir,$modelDir,$staging -Force | Out-Null
try {
  $zip=Join-Path $staging 'whisper-win-x64.zip'
  $runtimeUrl="https://github.com/ggml-org/whisper.cpp/releases/download/$Release/whisper-bin-x64.zip"
  Write-Output 'Downloading the official whisper.cpp Windows runtime...'
  Invoke-WebRequest -Uri $runtimeUrl -OutFile $zip -UseBasicParsing
  if((Get-Item -LiteralPath $zip).Length -lt 1000000) { throw 'The whisper.cpp runtime download was incomplete.' }
  Remove-Item -LiteralPath $runtimeDir -Recurse -Force
  New-Item -ItemType Directory -Path $runtimeDir -Force | Out-Null
  Expand-Archive -LiteralPath $zip -DestinationPath $runtimeDir -Force
  $cli=Get-ChildItem -LiteralPath $runtimeDir -Filter 'whisper-cli.exe' -File -Recurse | Select-Object -First 1
  if(-not $cli) { throw 'whisper-cli.exe was not found in the official Windows archive.' }

  $modelPath=Join-Path $modelDir ("ggml-$Model.bin")
  $modelUrl="https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-$Model.bin"
  Write-Output "Downloading the local Whisper $Model speech model..."
  Invoke-WebRequest -Uri $modelUrl -OutFile $modelPath -UseBasicParsing
  if((Get-Item -LiteralPath $modelPath).Length -lt 50000000) { throw 'The Whisper model download was incomplete.' }

  $relativeCli=$cli.FullName.Substring($root.Length).TrimStart('\')
  $relativeModel=$modelPath.Substring($root.Length).TrimStart('\')
  @{ Cli=$relativeCli; Model=$relativeModel; Release=$Release; Language='en'; Prompt='Corbie, trinket, raven, octopus, Ollama, Piper, Blackboard, Spotify, Libre Hardware Monitor.' } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'whisper.json') -Encoding UTF8
  & $cli.FullName -h 2>&1 | Out-Null
  if($LASTEXITCODE -ne 0) { throw 'The downloaded whisper.cpp runtime did not start.' }
  Write-Output "Corbie now has local Whisper $Model hearing. Audio transcription remains on this PC."
} finally {
  if(Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
}
