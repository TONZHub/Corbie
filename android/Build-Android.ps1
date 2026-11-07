param([string]$SdkPath, [string]$JavaHome)
$ErrorActionPreference='Stop'
if(!$SdkPath) {
  $SdkPath=@($env:ANDROID_SDK_ROOT,$env:ANDROID_HOME,(Join-Path $env:USERPROFILE 'AppData\Local\Android\Sdk')) |
    Where-Object { $_ -and (Test-Path -LiteralPath (Join-Path $_ 'platforms\android-35\android.jar')) } | Select-Object -First 1
}
if(!$SdkPath) {throw 'Provide -SdkPath to an Android SDK with platform 35 and build-tools 35.0.0.'}
if(!$JavaHome) {$JavaHome=$env:JAVA_HOME}
if(!$JavaHome) {throw 'Set JAVA_HOME or pass -JavaHome with the path to your JDK (17 or later).'}
$java=Join-Path $JavaHome 'bin\java.exe'
$javac=Join-Path $JavaHome 'bin\javac.exe'
$keytool=Join-Path $JavaHome 'bin\keytool.exe'
$androidJar=Join-Path $SdkPath 'platforms\android-35\android.jar'
$tools=Join-Path $SdkPath 'build-tools\35.0.0'
$aapt=Join-Path $tools 'aapt2.exe'
$zipalign=Join-Path $tools 'zipalign.exe'
$d8=Join-Path $tools 'lib\d8.jar'
$apksigner=Join-Path $tools 'lib\apksigner.jar'
foreach($file in @($java,$javac,$keytool,$androidJar,$aapt,$zipalign,$d8,$apksigner)) {
  if(!(Test-Path -LiteralPath $file -PathType Leaf)) {throw "Build tool not found: $file"}
}
function Run-Tool([string]$Tool, [string[]]$Arguments) {
  & $Tool @Arguments
  if($LASTEXITCODE -ne 0) {throw "$([IO.Path]::GetFileName($Tool)) exited with $LASTEXITCODE"}
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$build=Join-Path $PSScriptRoot 'build'
$work=Join-Path $build ('work-'+[Guid]::NewGuid().ToString('N'))
$signing=Join-Path $PSScriptRoot '.signing'
foreach($dir in @($work,$signing,(Join-Path $work 'generated'),(Join-Path $work 'classes'),(Join-Path $work 'dex'),(Join-Path $work 'checks'))) {
  $null=New-Item -ItemType Directory -Force -Path $dir
}
$res=Join-Path $work 'resources.zip'
$unsigned=Join-Path $work 'unsigned.apk'
$generated=Join-Path $work 'generated'
$classes=Join-Path $work 'classes'
$dex=Join-Path $work 'dex'
$jar=Join-Path $work 'classes.jar'
$aligned=Join-Path $work 'aligned.apk'
$version='0.4.0'
$apk=Join-Path $build "Corbie-Pocket-v$version.apk"
Run-Tool $aapt @('compile','--dir',(Join-Path $PSScriptRoot 'res'),'-o',$res)
Run-Tool $aapt @('link','-o',$unsigned,'-I',$androidJar,'--manifest',(Join-Path $PSScriptRoot 'AndroidManifest.xml'),'--java',$generated,'--version-code','7','--version-name',$version,$res)
$source=@((Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Recurse -File -Filter '*.java').FullName) + @((Get-ChildItem -LiteralPath $generated -Recurse -File -Filter '*.java').FullName)
Run-Tool $javac (@('--release','8','-encoding','UTF-8','-classpath',$androidJar,'-d',$classes) + $source)
[IO.Compression.ZipFile]::CreateFromDirectory($classes,$jar)
Run-Tool $java @('-cp',$d8,'com.android.tools.r8.D8','--min-api','26','--lib',$androidJar,'--output',$dex,$jar)
$archive=[IO.Compression.ZipFile]::Open($unsigned,[IO.Compression.ZipArchiveMode]::Update)
try {
  foreach($file in (Get-ChildItem -LiteralPath $dex -File -Filter '*.dex')) {
    $null=[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$file.Name)
  }
} finally {$archive.Dispose()}
Run-Tool $zipalign @('-f','4',$unsigned,$aligned)
$keystore=Join-Path $signing 'development.p12'
if(!(Test-Path -LiteralPath $keystore)) {
  Run-Tool $keytool @('-genkeypair','-keystore',$keystore,'-storetype','PKCS12','-storepass','android','-keypass','android','-alias','corbie-development','-keyalg','RSA','-keysize','2048','-validity','10000','-dname','CN=Corbie Development')
}
Run-Tool $java @('-jar',$apksigner,'sign','--ks',$keystore,'--ks-pass','pass:android','--ks-key-alias','corbie-development','--out',$apk,$aligned)
Run-Tool $java @('-jar',$apksigner,'verify','--verbose',$apk)
Run-Tool $zipalign @('-c','4',$apk)
$checks=Join-Path $work 'checks'
Run-Tool $javac @('--release','8','-encoding','UTF-8','-d',$checks,(Join-Path $PSScriptRoot 'src\com\tonzhub\corbie\NestState.java'),(Join-Path $PSScriptRoot 'checks\NestStateCheck.java'))
Run-Tool $java @('-cp',$checks,'com.tonzhub.corbie.NestStateCheck')
$legacyAapt=Join-Path $tools 'aapt.exe'
$permissions=& $legacyAapt dump permissions $apk
if($LASTEXITCODE -ne 0) {throw 'Could not inspect APK permissions.'}
$expectedPermissions=@(
  "uses-permission: name='android.permission.SYSTEM_ALERT_WINDOW'"
  "uses-permission: name='android.permission.FOREGROUND_SERVICE'"
  "uses-permission: name='android.permission.FOREGROUND_SERVICE_SPECIAL_USE'"
  "uses-permission: name='android.permission.POST_NOTIFICATIONS'"
  "uses-permission: name='android.permission.SCHEDULE_EXACT_ALARM'"
)
$declaredPermissions=@($permissions -match '^uses-permission')
if($declaredPermissions.Count -ne $expectedPermissions.Count -or
   (Compare-Object $expectedPermissions $declaredPermissions)) {throw 'Unexpected APK permissions.'}
$badging=& $legacyAapt dump badging $apk
if($LASTEXITCODE -ne 0 -or !($badging -match "name='com.tonzhub.corbie.PocketActivity'") -or !($badging -match "^sdkVersion:'26'")) {throw 'APK launcher or minimum Android version did not match.'}
$badging | Where-Object {$_ -match '^(package|sdkVersion|targetSdkVersion|launchable-activity):'}
Write-Output "Built $apk"
