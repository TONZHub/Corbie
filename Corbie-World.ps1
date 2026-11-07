$ErrorActionPreference='Stop'
$node=Get-Command node -ErrorAction Stop
$previousKey=$env:CORBIE_POSTMARK_KEY
$previousWorld=$env:CORBIE_WORLD_ENABLED
try {
  if(-not $previousKey) {
    Write-Host 'Corbie world travel: enter the key for Corbie, not another resident. It stays in this session.'
    $secure=Read-Host 'Postmark key (hidden)' -AsSecureString
    $pointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { $env:CORBIE_POSTMARK_KEY=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
  }
  $env:CORBIE_WORLD_ENABLED='1'
  & $node.Source (Join-Path $PSScriptRoot 'corbie-shell.mjs')
  $result=$LASTEXITCODE
} finally {
  if($null -eq $previousKey) { Remove-Item Env:CORBIE_POSTMARK_KEY -ErrorAction SilentlyContinue }
  else { $env:CORBIE_POSTMARK_KEY=$previousKey }
  if($null -eq $previousWorld) { Remove-Item Env:CORBIE_WORLD_ENABLED -ErrorAction SilentlyContinue }
  else { $env:CORBIE_WORLD_ENABLED=$previousWorld }
}
exit $result
