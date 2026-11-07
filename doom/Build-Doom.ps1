$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
& $compiler /nologo /target:winexe /platform:x64 "/out:$PSScriptRoot\Corbie-Doom.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll "$PSScriptRoot\DoomArcade.cs"
if ($LASTEXITCODE -ne 0) { throw 'Corbie arcade build failed.' }
Write-Output 'Corbie arcade built.'
