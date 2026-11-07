[CmdletBinding(SupportsShouldProcess=$true)]
param([switch]$PrintTarget)
$ErrorActionPreference='Stop'
$releases=@(Get-ChildItem -LiteralPath $PSScriptRoot -Filter 'Corbie-v*.exe' -File | ForEach-Object {
  if($_.Name -match '^Corbie-v(?<release>[0-9]+\.[0-9]+(?:\.[0-9]+)?)\.exe$') {
    $parsedVersion=$null
    if([Version]::TryParse($Matches.release,[ref]$parsedVersion)) {
      [pscustomobject]@{Path=$_.FullName;Version=(New-Object Version($parsedVersion.Major,$parsedVersion.Minor,[Math]::Max(0,$parsedVersion.Build)))}
    }
  }
})
$latestPath=$null
foreach($release in ($releases | Sort-Object Version -Descending)) {
  try { $null=[Reflection.AssemblyName]::GetAssemblyName($release.Path);$latestPath=$release.Path;break } catch { }
}
if(!$latestPath) {throw 'No runnable Corbie-v*.exe build was found beside this launcher.'}
if($PrintTarget) {Write-Output $latestPath;return}
if($PSCmdlet.ShouldProcess($latestPath,'Launch the newest installed Corbie')) {
  Start-Process -FilePath $latestPath -WorkingDirectory $PSScriptRoot
}
