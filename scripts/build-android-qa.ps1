param(
    [string]$ApiUrl = 'http://10.0.2.2:5080',
    [ValidateSet('debug', 'profile')][string]$Mode = 'debug'
)
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$env:ANDROID_HOME = Join-Path $RepoRoot '.tools\android-sdk'
$env:ANDROID_SDK_ROOT = $env:ANDROID_HOME
$jdk = Get-ChildItem -LiteralPath (Join-Path $RepoRoot '.tools\jdk') -Directory | Where-Object Name -Like 'jdk-17*' | Select-Object -First 1
if (-not $jdk) { throw 'Instale um JDK 17 oficial em .tools\jdk ou adapte JAVA_HOME neste script.' }
$env:JAVA_HOME = $jdk.FullName
$env:GRADLE_USER_HOME = Join-Path $RepoRoot '.tools\gradle'
Push-Location (Join-Path $RepoRoot 'client\elifoot_flutter')
try {
    flutter build apk "--$Mode" --target=lib/main.dart "--dart-define=API_BASE_URL=$ApiUrl"
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar APK.' }
    $artifactDir = Join-Path $RepoRoot 'artifacts\android'
    New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
    Copy-Item -LiteralPath "build\app\outputs\flutter-apk\app-$Mode.apk" -Destination (Join-Path $artifactDir "elifoot-qa003-$Mode.apk") -Force
} finally { Pop-Location }
