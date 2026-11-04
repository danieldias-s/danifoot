$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$runtimeDir = Join-Path $taskRoot 'artifacts/presentation-017a'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'

function Test-PreviewPort([int] $port) {
    $probe = [Net.Sockets.TcpClient]::new()
    try {
        $connect = $probe.ConnectAsync('127.0.0.1', $port)
        return ($connect.Wait(500) -and $probe.Connected)
    } catch { return $false } finally { $probe.Dispose() }
}

function Wait-PreviewPort([int] $port) {
    $deadline = [DateTime]::UtcNow.AddSeconds(25)
    while (-not (Test-PreviewPort $port)) {
        if ([DateTime]::UtcNow -gt $deadline) { throw "Servico local nao respondeu na porta $port. Consulte $runtimeDir." }
        Start-Sleep -Milliseconds 400
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $taskRoot 'client/elifoot_flutter/build/web/index.html'))) {
    throw 'A previa web ainda precisa ser compilada.'
}
New-Item -ItemType Directory -Path $runtimeDir -Force | Out-Null

if (-not (Test-PreviewPort 55436)) {
    $pgData = Join-Path $runtimeDir 'postgres-data'
    if (-not (Test-Path -LiteralPath (Join-Path $pgData 'PG_VERSION'))) { throw 'Banco local de testes nao encontrado. Nenhum banco novo foi criado.' }
    Start-Process -FilePath 'C:/Program Files/PostgreSQL/18/bin/postgres.exe' -ArgumentList @('-D', "`"$pgData`"", '-p', '55436', '-h', '127.0.0.1') -WorkingDirectory $taskRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runtimeDir "pg-$stamp.log") -RedirectStandardError (Join-Path $runtimeDir "pg-$stamp-error.log") | Out-Null
}
Wait-PreviewPort 55436
$pgDeadline = [DateTime]::UtcNow.AddMinutes(3)
do {
    & 'C:/Program Files/PostgreSQL/18/bin/pg_isready.exe' -h 127.0.0.1 -p 55436 -U qa017a -t 1 | Out-Null
    $pgReady = $LASTEXITCODE -eq 0
    if (-not $pgReady) {
        if ([DateTime]::UtcNow -gt $pgDeadline) { throw 'O PostgreSQL ainda esta recuperando o desligamento. Consulte os logs e tente novamente.' }
        Start-Sleep -Milliseconds 500
    }
} until ($pgReady)

if (-not (Test-PreviewPort 5231)) {
    & (Join-Path $runtimeDir 'start-preview.ps1')
}
Wait-PreviewPort 5231

if (-not (Test-PreviewPort 5232)) {
    Start-Process -FilePath node -ArgumentList @("`"$(Join-Path $taskRoot 'scripts/serve-web.mjs')`"", '5232') -WorkingDirectory $taskRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runtimeDir "web-$stamp.log") -RedirectStandardError (Join-Path $runtimeDir "web-$stamp-error.log") | Out-Null
}
Wait-PreviewPort 5232

$api = Invoke-WebRequest -Uri 'http://127.0.0.1:5231/api/worlds' -UseBasicParsing -TimeoutSec 20
$web = Invoke-WebRequest -Uri 'http://127.0.0.1:5232/' -UseBasicParsing -TimeoutSec 20
if ($api.StatusCode -ne 200 -or $web.StatusCode -ne 200) { throw 'A previa nao passou na verificacao HTTP.' }
Write-Output 'Previa pronta: http://127.0.0.1:5232/#/plantel'
