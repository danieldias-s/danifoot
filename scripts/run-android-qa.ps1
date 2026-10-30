param(
    [string]$DeviceSerial,
    [ValidateSet('Emulator', 'UsbReverse', 'Lan')][string]$Transport = 'Emulator',
    [string]$ApiUrl,
    [switch]$SkipBuild,
    [switch]$SoftwareRendering,
    [ValidateSet('debug', 'profile')][string]$Mode = 'debug',
    [int]$BootTimeoutSeconds = 120,
    [int]$AdbTimeoutSeconds = 600
)
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$adb = Join-Path $RepoRoot '.tools\android-sdk\platform-tools\adb.exe'
$evidence = Join-Path $RepoRoot 'docs\android-runtime-003\device-run'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null

function Invoke-QaAdb([string[]]$Arguments) {
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new($adb)
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $startInfo.ArgumentList.Add($argument) }
    $commandProcess = [System.Diagnostics.Process]::new()
    $commandProcess.StartInfo = $startInfo
    try {
        [void]$commandProcess.Start()
        $stdout = $commandProcess.StandardOutput.ReadToEndAsync()
        $stderr = $commandProcess.StandardError.ReadToEndAsync()
        if (-not $commandProcess.WaitForExit($AdbTimeoutSeconds * 1000)) {
            $commandProcess.Kill()
            throw "ADB excedeu ${AdbTimeoutSeconds}s: $($Arguments -join ' ')"
        }
        $output = ($stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()) -split '\r?\n'
        if ($commandProcess.ExitCode -ne 0) { throw "ADB falhou: $($Arguments -join ' ')`n$output" }
        if ($output -match '^\s*(Error:|Failure\[|Error type \d|android\.util\.AndroidException:)') {
            throw "Android recusou o comando: $($Arguments -join ' ')`n$output"
        }
        return $output
    } finally { $commandProcess.Dispose() }
}

$devices = Invoke-QaAdb @('devices', '-l')
$devices | Set-Content -LiteralPath (Join-Path $evidence 'devices.log')
$online = @($devices | ForEach-Object {
    if ($_ -match '^(\S+)\s+device(?:\s|$)') { $Matches[1] }
})
if (-not $DeviceSerial) {
    if ($online.Count -ne 1) {
        throw 'Informe -DeviceSerial com um dispositivo online. Nenhum dispositivo online unico foi encontrado; consulte device-run/devices.log.'
    }
    $DeviceSerial = $online[0]
}
if ($DeviceSerial -notin $online) { throw "Dispositivo $DeviceSerial offline ou nao autorizado." }
if ($Transport -eq 'Emulator' -and $DeviceSerial -notlike 'emulator-*') {
    throw 'Para celular fisico selecione -Transport UsbReverse ou Lan.'
}
$deadline = (Get-Date).AddSeconds($BootTimeoutSeconds)
do {
    $boot = Invoke-QaAdb @('-s', $DeviceSerial, 'shell', 'getprop', 'sys.boot_completed')
    if (($boot -join '').Trim() -eq '1') { break }
    if ((Get-Date) -ge $deadline) { throw 'Android nao concluiu o boot no prazo.' }
    Start-Sleep -Seconds 2
} while ($true)

if (-not $ApiUrl) {
    $ApiUrl = switch ($Transport) {
        'Emulator' { 'http://10.0.2.2:5080' }
        'UsbReverse' { 'http://127.0.0.1:5080' }
        'Lan' { throw 'Informe -ApiUrl http://IP_DO_WINDOWS:5080 e exponha a API na rede local.' }
    }
}
if ($Transport -eq 'UsbReverse') {
    Invoke-QaAdb @('-s', $DeviceSerial, 'reverse', 'tcp:5080', 'tcp:5080')
}
if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'build-android-qa.ps1') -ApiUrl $ApiUrl -Mode $Mode
}
$apk = Join-Path $RepoRoot "artifacts\android\elifoot-qa003-$Mode.apk"
if (-not (Test-Path -LiteralPath $apk)) { throw "APK nao encontrado: $apk" }
Invoke-QaAdb @('-s', $DeviceSerial, 'install', '--no-streaming', '-r', $apk) |
    Set-Content -LiteralPath (Join-Path $evidence 'install.log')
$launchArgs = @('-s', $DeviceSerial, 'shell', 'am', 'start', '-W', '-n',
    'com.example.elifoot_flutter/.MainActivity')
if ($SoftwareRendering) {
    $launchArgs += @('--ez', 'enable-software-rendering', 'true', '--ez', 'enable-impeller', 'false')
}
$launchOutput = Invoke-QaAdb $launchArgs
$launchOutput | Set-Content -LiteralPath (Join-Path $evidence 'launch.log')
Start-Sleep -Seconds 5
Invoke-QaAdb @('-s', $DeviceSerial, 'shell', 'screencap', '-p', '/data/local/tmp/elifoot-qa003-launch.png') | Out-Null
Invoke-QaAdb @('-s', $DeviceSerial, 'pull', '/data/local/tmp/elifoot-qa003-launch.png',
    (Join-Path $evidence 'launch.png')) | Out-Null
$appPid = (Invoke-QaAdb @('-s', $DeviceSerial, 'shell', 'pidof', 'com.example.elifoot_flutter') -join '').Trim()
if (-not $appPid) { throw 'O processo do aplicativo nao permaneceu ativo.' }
Invoke-QaAdb @('-s', $DeviceSerial, 'logcat', '-d', "--pid=$appPid") |
    Set-Content -LiteralPath (Join-Path $evidence 'app-logcat.log')
Get-FileHash -LiteralPath $apk -Algorithm SHA256 |
    Format-List | Out-File -LiteralPath (Join-Path $evidence 'apk-sha256.log')
if ($launchOutput -match 'Status:\s*timeout') {
    throw 'Android aceitou a Activity mas nao confirmou a primeira tela no prazo. Consulte launch.log, launch.png e app-logcat.log.'
}
Write-Output "Aplicativo iniciado em $DeviceSerial. API configurada: $ApiUrl. Evidencias: $evidence"
Write-Output 'Instalacao/abertura nao certificam gameplay. Execute o roteiro de docs/ANDROID_RUNTIME_003.md.'
