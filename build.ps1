$ErrorActionPreference = 'Stop'
$version = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
if ($version -notmatch '^[0-9]+\.[0-9]+(\.[0-9]+)?$') { throw 'VERSION must be numeric, such as 0.115.' }
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'The x64 Windows .NET Framework compiler was not found.' }
$contracts = Join-Path $PSScriptRoot '.deps\sdk-contracts\ref\netstandard2.0'
$audio = Join-Path $PSScriptRoot 'NAudio.dll'
$winFiles = @('Windows.WinMD','Windows.Foundation.FoundationContract.winmd','Windows.Foundation.UniversalApiContract.winmd') | ForEach-Object { Join-Path $contracts $_ }
foreach ($file in @($audio) + $winFiles) {
  if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw 'Run .\Restore-Dependencies.ps1 before building.' }
}
$references = @('WPF\PresentationFramework.dll','WPF\PresentationCore.dll','WPF\WindowsBase.dll','WPF\System.Speech.dll','System.Xaml.dll','System.Windows.Forms.dll','System.Drawing.dll','Microsoft.CSharp.dll','System.Net.Http.dll','System.Security.dll','System.Web.Extensions.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Runtime.WindowsRuntime.dll','System.Runtime.dll') | ForEach-Object { '/r:' + (Join-Path $framework $_) }
$winReferences = $winFiles | ForEach-Object { '/r:' + $_ }
$icon = Join-Path $PSScriptRoot 'assets\corbie.ico'
& (Join-Path $PSScriptRoot 'assets\Build-Icon.ps1')
$source = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src\Desktop') -File -Filter '*.cs' | Sort-Object Name | ForEach-Object FullName)
$exe = Join-Path $PSScriptRoot "Corbie-v$version.exe"
& $compiler /nologo /target:winexe "/win32icon:$icon" "/resource:$icon,CorbiePet.AppIcon.ico" "/out:$exe" @references @winReferences "/r:$audio" @source
if ($LASTEXITCODE -ne 0) { throw 'Corbie build failed.' }
Write-Output "Built $exe"
