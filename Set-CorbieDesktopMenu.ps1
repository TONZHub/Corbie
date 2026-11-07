[CmdletBinding(SupportsShouldProcess=$true)]
param([ValidateSet('Enable','Disable','Status')][string]$Action='Enable')
$ErrorActionPreference='Stop'
$menuKey='HKCU:\Software\Classes\DesktopBackground\Shell\CorbieLatest'
$launcher=Join-Path $PSScriptRoot 'Launch-Latest-Corbie.ps1'
$powershell=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$command='"'+$powershell+'" -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "'+$launcher+'"'
if($Action -eq 'Enable') {
  if(!(Test-Path -LiteralPath $launcher -PathType Leaf)) {throw "Latest-version launcher is missing: $launcher"}
  if($PSCmdlet.ShouldProcess($menuKey,'Add Launch latest Corbie to the Windows desktop right-click menu')) {
    $null=New-Item -Path $menuKey -Force
    $null=New-ItemProperty -LiteralPath $menuKey -Name 'MUIVerb' -Value 'Launch latest Corbie' -PropertyType String -Force
    $null=New-ItemProperty -LiteralPath $menuKey -Name 'Icon' -Value ('"'+(Join-Path $PSScriptRoot 'assets\corbie.ico')+'",0') -PropertyType String -Force
    $null=New-ItemProperty -LiteralPath $menuKey -Name 'Position' -Value 'Bottom' -PropertyType String -Force
    $null=New-Item -Path ($menuKey+'\command') -Force
    Set-Item -LiteralPath ($menuKey+'\command') -Value $command
  }
} elseif($Action -eq 'Disable') {
  if((Test-Path -LiteralPath $menuKey) -and $PSCmdlet.ShouldProcess($menuKey,'Remove the Corbie desktop menu item')) {
    # Only these two exact, app-owned registry keys are removed.
    if(Test-Path -LiteralPath ($menuKey+'\command')) {Remove-Item -LiteralPath ($menuKey+'\command')}
    Remove-Item -LiteralPath $menuKey
  }
}
if(Test-Path -LiteralPath $menuKey) {
  [pscustomobject]@{Installed=$true;Label=(Get-ItemPropertyValue -LiteralPath $menuKey -Name 'MUIVerb');Command=(Get-Item -LiteralPath ($menuKey+'\command')).GetValue('');Key=$menuKey}
} else {
  [pscustomobject]@{Installed=$false;Label=$null;Command=$null;Key=$menuKey}
}
