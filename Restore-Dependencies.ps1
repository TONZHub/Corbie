$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$cache = Join-Path $PSScriptRoot '.deps'
$null = New-Item -ItemType Directory -Force -Path $cache
function Install-CorbiePackage([string]$Name, [string]$Version, [hashtable]$Files) {
  $missing = @($Files.Values | Where-Object { !(Test-Path -LiteralPath (Join-Path $PSScriptRoot $_) -PathType Leaf) })
  if (!$missing.Count) { return }
  $package = Join-Path $cache "$Name.$Version.nupkg"
  if (!(Test-Path -LiteralPath $package -PathType Leaf)) {
    $partial = $package + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
      $url = "https://api.nuget.org/v3-flatcontainer/$Name/$Version/$Name.$Version.nupkg"
      Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $partial -TimeoutSec 120
      Move-Item -LiteralPath $partial -Destination $package
    } finally { if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial } }
  }
  $zip = [IO.Compression.ZipFile]::OpenRead($package)
  try {
    foreach ($entryName in $Files.Keys) {
      $entry = $zip.GetEntry($entryName)
      if ($null -eq $entry -or $entry.Length -eq 0) { throw "Missing package file: $entryName in $package" }
    }
    foreach ($entryName in $Files.Keys) {
      $dest = Join-Path $PSScriptRoot $Files[$entryName]
      $null = New-Item -ItemType Directory -Force -Path (Split-Path $dest)
      [IO.Compression.ZipFileExtensions]::ExtractToFile($zip.GetEntry($entryName), $dest, $true)
    }
  } finally { $zip.Dispose() }
  Write-Output "Restored $Name $Version"
}
$priorTls = [Net.ServicePointManager]::SecurityProtocol
try {
  [Net.ServicePointManager]::SecurityProtocol = $priorTls -bor [Net.SecurityProtocolType]::Tls12
  Install-CorbiePackage 'naudio' '1.10.0' @{ 'lib/net35/NAudio.dll' = 'NAudio.dll' }
  $contracts = @{}
  foreach ($file in @('Windows.WinMD','Windows.Foundation.FoundationContract.winmd','Windows.Foundation.UniversalApiContract.winmd')) {
    $contracts["ref/netstandard2.0/$file"] = ".deps/sdk-contracts/ref/netstandard2.0/$file"
  }
  Install-CorbiePackage 'microsoft.windows.sdk.contracts' '10.0.26100.7705' $contracts
} finally { [Net.ServicePointManager]::SecurityProtocol = $priorTls }
