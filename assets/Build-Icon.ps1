$ErrorActionPreference='Stop'
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$iconBuilder=Join-Path ([IO.Path]::GetTempPath()) ('corbie-icon-builder-'+[Guid]::NewGuid().ToString('N')+'.exe')
try {
  $references=@('WPF\PresentationFramework.dll','WPF\PresentationCore.dll','WPF\WindowsBase.dll','System.Xaml.dll') | ForEach-Object {'/r:'+(Join-Path $framework $_)}
  & (Join-Path $framework 'csc.exe') /nologo /target:exe "/out:$iconBuilder" @references (Join-Path $PSScriptRoot 'RenderIcon.cs')
  if($LASTEXITCODE -ne 0) {throw 'Icon renderer did not compile.'}
  & $iconBuilder $PSScriptRoot
  if($LASTEXITCODE -ne 0) {throw 'Icon rendering failed.'}
  Write-Output 'Corbie icon rendered at 16, 20, 24, 32, 40, 48, 64, 128 and 256 pixels.'
} finally {
  if(Test-Path -LiteralPath $iconBuilder) {Remove-Item -LiteralPath $iconBuilder -Force}
}
