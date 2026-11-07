$ErrorActionPreference='Stop'
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$contracts=Join-Path (Split-Path $PSScriptRoot) '.deps\sdk-contracts\ref\netstandard2.0'
if (!(Test-Path -LiteralPath (Join-Path $contracts 'Windows.WinMD'))) { throw 'Run Restore-Dependencies.ps1 from the repository root first.' }
$refs=@('Windows.WinMD','Windows.Foundation.FoundationContract.winmd','Windows.Foundation.UniversalApiContract.winmd') | ForEach-Object { '/r:'+(Join-Path $contracts $_) }
$refs += '/r:'+(Join-Path $framework 'System.Runtime.WindowsRuntime.dll')
$refs += '/r:'+(Join-Path $framework 'System.Runtime.dll')
& (Join-Path $framework 'csc.exe') /nologo /target:exe "/out:$PSScriptRoot\GameWindow-v6.exe" /r:System.Drawing.dll /r:System.Web.Extensions.dll @refs "$PSScriptRoot\GameWindow.cs" "$PSScriptRoot\FrameVision.cs"
if($LASTEXITCODE -ne 0) { throw 'Game window adapter could not compile.' }
