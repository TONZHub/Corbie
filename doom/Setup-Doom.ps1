param([switch]$BuildOnly)
$ErrorActionPreference = 'Stop'
if ($BuildOnly -or -not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Corbie-Doom.exe'))) { & (Join-Path $PSScriptRoot 'Build-Doom.ps1') }
if ($BuildOnly) { return }

# Pinned official releases. Engine digest is published in GitHub release assets.
$engineUrl = 'https://github.com/fabiangreffrath/crispy-doom/releases/download/crispy-doom-7.1/crispy-doom-7.1.0-win64.zip'
$engineHash = '7a41e999473f6e9333f76684af12a8d19c2928250809214681ccf4c79d5dba0d'
$gameUrl = 'https://github.com/freedoom/freedoom/releases/download/v0.13.0/freedoom-0.13.0.zip'
$checksumUrl = 'https://github.com/freedoom/freedoom/releases/download/v0.13.0/freedoom-0.13.0-CHECKSUM'
. (Join-Path $PSScriptRoot 'Setup-Common.ps1')
$mutexHash = [Security.Cryptography.SHA256]::Create()
try { $mutexName = 'Local\CorbieArcadeSetup-' + [BitConverter]::ToString($mutexHash.ComputeHash([Text.Encoding]::UTF8.GetBytes($doomRoot.ToLowerInvariant()))).Replace('-','').Substring(0,16) }
finally { $mutexHash.Dispose() }
$mutex = New-Object Threading.Mutex($false,$mutexName)
$acquired = $false
$stage = Join-Path $doomRoot ('.stage-'+[Guid]::NewGuid().ToString('N'))
try {
  try { $acquired=$mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $acquired=$true }
  if (-not $acquired) { throw 'Another Corbie arcade setup is already running.' }
  if ((Test-Path -LiteralPath (Join-Path $doomRoot 'runtime\crispy-doom.exe')) -and (Test-Path -LiteralPath (Join-Path $doomRoot 'games\freedoom1.wad'))) {
    Write-Output 'Starter pack is already installed. Open Corbie-Doom.cmd and press Play.'; return
  }
  [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
  New-Item -ItemType Directory -Path $stage | Out-Null
  $engineZip = Join-Path $stage 'engine.zip'; $gameZip=Join-Path $stage 'freedoom.zip'; $checksums=Join-Path $stage 'CHECKSUM'
  Write-Output 'Downloading official Crispy Doom 7.1.0 (about 10 MB)...'
  Fetch-File $engineUrl $engineZip
  if ((Get-FileHash -LiteralPath $engineZip -Algorithm SHA256).Hash.ToLowerInvariant() -ne $engineHash) { throw 'Crispy Doom checksum mismatch. Nothing installed.' }
  Write-Output 'Downloading official Freedoom 0.13.0 (about 23 MB)...'
  Fetch-File $gameUrl $gameZip; Fetch-File $checksumUrl $checksums
  $checksumText=[IO.File]::ReadAllText($checksums)
  $match=[regex]::Match($checksumText,'(?im)^([a-f0-9]{64})\s+\*?(?:\./)?freedoom-0\.13\.0\.zip\s*$')
  if (-not $match.Success) { $match=[regex]::Match($checksumText,'(?im)^SHA256\s*\(freedoom-0\.13\.0\.zip\)\s*=\s*([a-f0-9]{64})\s*$') }
  if (-not $match.Success) { throw 'Could not read the upstream Freedoom SHA256 checksum. Nothing installed.' }
  $gameHash=$match.Groups[1].Value.ToLowerInvariant()
  if ((Get-FileHash -LiteralPath $gameZip -Algorithm SHA256).Hash.ToLowerInvariant() -ne $gameHash) { throw 'Freedoom checksum mismatch. Nothing installed.' }
  Write-Output 'Checksums match. Unpacking into Corbie only...'
  $engineStage=Join-Path $stage 'engine';$gameStage=Join-Path $stage 'games'
  Expand-Checked $engineZip $engineStage;Expand-Checked $gameZip $gameStage
  $engines=@(Get-ChildItem -LiteralPath $engineStage -Filter 'crispy-doom.exe' -Recurse -File)
  $games=@(Get-ChildItem -LiteralPath $gameStage -Filter 'freedoom1.wad' -Recurse -File)
  if ($engines.Count -ne 1 -or $games.Count -ne 1) { throw 'The official package layout was unexpected. Nothing installed.' }
  $runtime=Assert-LocalPath (Join-Path $doomRoot 'runtime');$gameDir=Assert-LocalPath (Join-Path $doomRoot 'games')
  # Preserve a partial engine as a backup. Add starter files without moving or
  # overwriting user mods (including their README/credits) already in games.
  if (Test-Path -LiteralPath $runtime) { Move-Item -LiteralPath $runtime -Destination ($runtime+'.previous-'+[Guid]::NewGuid().ToString('N')) }
  Move-Item -LiteralPath (Assert-LocalPath $engines[0].DirectoryName) -Destination $runtime
  New-Item -ItemType Directory -Path $gameDir -Force | Out-Null
  foreach ($item in (Get-ChildItem -LiteralPath $games[0].DirectoryName)) {
    $destination=Assert-LocalPath (Join-Path $gameDir $item.Name)
    if (-not (Test-Path -LiteralPath $destination)) { Move-Item -LiteralPath (Assert-LocalPath $item.FullName) -Destination $destination }
  }
  $record=@{ engine='Crispy Doom 7.1.0'; engineSource=$engineUrl; engineSha256=$engineHash; game='Freedoom 0.13.0'; gameSource=$gameUrl; gameSha256=$gameHash; installedUtc=[DateTime]::UtcNow.ToString('o') }
  [IO.File]::WriteAllText((Join-Path $doomRoot 'installed.json'),($record|ConvertTo-Json))
  Write-Output 'Ready. Open Corbie-Doom-Arcade.cmd for manual play. Corbie-Doom.cmd opens the autonomous player.'
} finally {
  Remove-Stage $stage
  if ($acquired) { $mutex.ReleaseMutex() }
  $mutex.Dispose()
}
