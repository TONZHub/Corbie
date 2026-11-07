$doomRoot = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
$rootPrefix = $doomRoot + '\'
function Assert-LocalPath([string]$path) {
  $full = [IO.Path]::GetFullPath($path)
  if (-not $full.StartsWith($rootPrefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'A setup path escaped the arcade folder.' }
  return $full
}
function Remove-Stage([string]$path) {
  $full = Assert-LocalPath $path
  if (-not ([IO.Path]::GetFileName($full)).StartsWith('.stage-')) { throw 'Refusing to remove a non-staging folder.' }
  if (Test-Path -LiteralPath $full) { Remove-Item -LiteralPath $full -Recurse -Force }
}
function Fetch-File([string]$url,[string]$path) {
  Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $path -TimeoutSec 180 -Headers @{'User-Agent'='Corbie-Arcade-Setup/1.0'}
}
function Expand-Checked([string]$archive,[string]$destination) {
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $target = (Assert-LocalPath $destination).TrimEnd('\')+'\'
  New-Item -ItemType Directory -Path $destination -Force | Out-Null
  $zip = [IO.Compression.ZipFile]::OpenRead($archive)
  try {
    if ($zip.Entries.Count -gt 10000) { throw 'Unexpectedly large archive directory.' }
    $total = [long]0
    foreach ($entry in $zip.Entries) {
      $total += $entry.Length
      if ($total -gt 600MB -or $entry.Length -gt 200MB) { throw 'Archive exceeded its extraction limit.' }
      $name = $entry.FullName.Replace('/','\')
      if ([IO.Path]::IsPathRooted($name) -or $name.Contains(':') -or (($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) { throw 'Unsafe archive entry.' }
      $output = [IO.Path]::GetFullPath((Join-Path $destination $name))
      if (-not $output.StartsWith($target,[StringComparison]::OrdinalIgnoreCase)) { throw 'Archive entry escaped the staging folder.' }
      if (-not $entry.Name) { New-Item -ItemType Directory -Path $output -Force | Out-Null; continue }
      New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($output)) -Force | Out-Null
      [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$output,$false)
    }
  } finally { $zip.Dispose() }
}
