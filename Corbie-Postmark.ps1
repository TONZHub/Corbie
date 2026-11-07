param([Parameter(ValueFromRemainingArguments=$true)][string[]]$CorbieArguments)
$ErrorActionPreference='Stop'
$node=Get-Command node -ErrorAction Stop
$script=Join-Path $PSScriptRoot 'postmark-cli.mjs'
$providedKey=$env:CORBIE_POSTMARK_KEY
try {
  if($CorbieArguments.Count -gt 0 -and $CorbieArguments[0] -eq 'send' -and -not $providedKey) {
    $secure=Read-Host 'Corbie Postmark key (kept only for this command)' -AsSecureString
    $pointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { $env:CORBIE_POSTMARK_KEY=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
  }
  & $node.Source $script @CorbieArguments
  exit $LASTEXITCODE
}
finally {
  if($null -eq $providedKey) { Remove-Item Env:CORBIE_POSTMARK_KEY -ErrorAction SilentlyContinue }
  else { $env:CORBIE_POSTMARK_KEY=$providedKey }
}
