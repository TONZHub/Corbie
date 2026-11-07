[CmdletBinding(SupportsShouldProcess=$true)]
param([ValidateSet('Enable','Disable','Status')][string]$Action='Enable')
$ErrorActionPreference='Stop'
$startupFolder=[Environment]::GetFolderPath('Startup')
if([string]::IsNullOrWhiteSpace($startupFolder)) { throw 'Windows did not provide a per-user Startup folder.' }
$shortcutPath=Join-Path $startupFolder 'Corbie.lnk'
$launcher=Join-Path $PSScriptRoot 'Start-Corbie.ps1'
$powershell=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$arguments='-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "'+$launcher+'" -AtLogin'

if($Action -eq 'Enable') {
  if(-not (Test-Path -LiteralPath $launcher -PathType Leaf)) { throw "Corbie launcher is missing: $launcher" }
  if($PSCmdlet.ShouldProcess($shortcutPath,'Start Corbie when this Windows user signs in')) {
    # Stage locally: the Windows shortcut writer may need temporary sibling files.
    # Copying the finished link only needs access to the requested Startup file.
    $stagingPath=Join-Path $PSScriptRoot ('.corbie-startup-'+[Guid]::NewGuid().ToString('N')+'.lnk')
    try {
      $shell=New-Object -ComObject WScript.Shell
      $shortcut=$shell.CreateShortcut($stagingPath)
      $shortcut.TargetPath=$powershell
      $shortcut.Arguments=$arguments
      $shortcut.WorkingDirectory=$PSScriptRoot
      $shortcut.Description='Launch Corbie when you sign in to Windows.'
      $shortcut.IconLocation=(Join-Path $PSScriptRoot 'assets\corbie.ico')+',0'
      $shortcut.WindowStyle=7
      $shortcut.Save()
      Copy-Item -LiteralPath $stagingPath -Destination $shortcutPath -Force
    } finally {
      if(Test-Path -LiteralPath $stagingPath) { Remove-Item -LiteralPath $stagingPath }
    }
  }
} elseif($Action -eq 'Disable') {
  if((Test-Path -LiteralPath $shortcutPath) -and $PSCmdlet.ShouldProcess($shortcutPath,'Remove Corbie sign-in shortcut')) {
    Remove-Item -LiteralPath $shortcutPath
  }
}

if(Test-Path -LiteralPath $shortcutPath) {
  $shell=New-Object -ComObject WScript.Shell
  $saved=$shell.CreateShortcut($shortcutPath)
  [pscustomobject]@{Installed=$true;Shortcut=$shortcutPath;Target=$saved.TargetPath;Arguments=$saved.Arguments;WorkingDirectory=$saved.WorkingDirectory}
} else {
  [pscustomobject]@{Installed=$false;Shortcut=$shortcutPath;Target=$null;Arguments=$null;WorkingDirectory=$null}
}
