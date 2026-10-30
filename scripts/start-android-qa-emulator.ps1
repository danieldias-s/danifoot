param(
    [string]$AvdName = 'elifoot_qa003_light',
    [switch]$UseSoftwareFallback,
    [ValidateSet('swiftshader_indirect', 'off')][string]$Gpu = 'swiftshader_indirect'
)
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$env:ANDROID_HOME = Join-Path $RepoRoot '.tools\android-sdk'
$env:ANDROID_SDK_ROOT = $env:ANDROID_HOME
$env:ANDROID_AVD_HOME = Join-Path $RepoRoot '.tools\avd'
$emulatorRoot = Join-Path $env:ANDROID_HOME 'emulator'
if ($UseSoftwareFallback) {
    $emulatorRoot = Join-Path $RepoRoot '.tools\emulator-fallback\emulator'
}
$emulator = Join-Path $emulatorRoot 'emulator.exe'
if (-not (Test-Path -LiteralPath $emulator)) { throw "Emulador ausente: $emulator" }
$avds = & $emulator -list-avds
if ($AvdName -notin $avds) { throw "AVD ausente: $AvdName. Nenhum AVD foi apagado ou recriado." }
$env:PATH = "$emulatorRoot;$emulatorRoot\lib64;$emulatorRoot\lib64\qt\lib;$env:PATH"
$arguments = @('-avd', $AvdName, '-no-window', '-no-snapshot', '-no-audio', '-no-boot-anim')
if ($UseSoftwareFallback) {
    $arguments += @('-no-accel', '-cores', '1', '-memory', '1024', '-gpu', $Gpu, '-feature', '-Vulkan')
}
# Cold boot preserves userdata. Never use -wipe-data for this workflow.
& $emulator @arguments
if ($LASTEXITCODE -ne 0) { throw "Emulador encerrou com codigo $LASTEXITCODE" }
