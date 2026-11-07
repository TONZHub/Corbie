$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Setup-Common.ps1')
# Official Windows release; SHA256 published by GitHub's release-assets API.
$engineUrl = 'https://github.com/fabiangreffrath/woof/releases/download/woof_16.0.0/Woof-16.0.0-win64.zip'
$engineHash = '1c7fd0afe9b1ab9517ab900fc4b4974282430e50cb57fe3291d6218f125ccd31'
$runtime = Assert-LocalPath (Join-Path $doomRoot 'woof-runtime')
$stage = Join-Path $doomRoot ('.stage-'+[Guid]::NewGuid().ToString('N'))
$mutex = New-Object Threading.Mutex($false,'Local\CorbieAvianSetup')
$acquired = $false
try {
  try { $acquired=$mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $acquired=$true }
  if (-not $acquired) { throw 'Another Avian engine setup is running.' }
  if (Test-Path -LiteralPath (Join-Path $runtime 'woof.exe')) { Write-Output 'Avian engine already installed.'; return }
  [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
  New-Item -ItemType Directory -Path $stage | Out-Null
  $archive = Join-Path $stage 'woof.zip'
  Write-Output 'Downloading official Woof! 16.0.0 (about 6 MB)...'
  Fetch-File $engineUrl $archive
  if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $engineHash) { throw 'Woof checksum mismatch. Nothing installed.' }
  $unpacked = Join-Path $stage 'unpacked'
  Expand-Checked $archive $unpacked
  $engines = @(Get-ChildItem -LiteralPath $unpacked -Recurse -Filter 'woof.exe' -File)
  if ($engines.Count -ne 1) { throw 'Unexpected Woof package layout.' }
  if (Test-Path -LiteralPath $runtime) { Move-Item -LiteralPath $runtime -Destination ($runtime+'.previous-'+[Guid]::NewGuid().ToString('N')) }
  Move-Item -LiteralPath (Assert-LocalPath $engines[0].DirectoryName) -Destination $runtime
  $record = @{engine='Woof! 16.0.0';source=$engineUrl;sha256=$engineHash;installedUtc=[DateTime]::UtcNow.ToString('o')}
  [IO.File]::WriteAllText((Join-Path $doomRoot 'avian-installed.json'),($record|ConvertTo-Json))
  Write-Output 'Avian engine ready. Open Corbie-Doom-Arcade.cmd, choose Austrian Avian and press Play.'
} finally {
  Remove-Stage $stage
  if ($acquired) { $mutex.ReleaseMutex() }
  $mutex.Dispose()
}
