$ErrorActionPreference = 'Stop'
$version = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
$exe = Join-Path $PSScriptRoot "Corbie-v$version.exe"
if (!(Test-Path -LiteralPath $exe)) { throw 'Run .\build.ps1 first.' }
$results = Join-Path $PSScriptRoot '.test-results'
$null = New-Item -ItemType Directory -Force -Path $results
foreach ($check in @('smoke','icon','screen-read','latest','director','viewer-mail')) {
  $report = Join-Path $results "$check.txt"
  $arguments = @("--$check-test")
  if ($check -ne 'smoke') { $arguments += '"' + $report + '"' }
  $child = Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru
  try {
    if (!$child.WaitForExit(45000)) { $child.Kill(); throw "$check timed out." }
    if ($child.ExitCode -ne 0) {
      $failure = if ($check -eq 'smoke') { Join-Path $PSScriptRoot 'smoke-error.txt' } else { $report }
      if (Test-Path -LiteralPath $failure) { Get-Content -LiteralPath $failure }
      throw "$check failed (exit $($child.ExitCode))."
    }
    Write-Output "PASS: $check"
  } finally { $child.Dispose() }
}
