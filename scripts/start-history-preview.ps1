$ErrorActionPreference='Stop'
$historyRoot=Split-Path -Parent $PSScriptRoot
$historyRuntime=Join-Path $historyRoot 'artifacts/history-preview'
New-Item -ItemType Directory -Path $historyRuntime -Force | Out-Null
$historyStamp=Get-Date -Format 'yyyyMMdd-HHmmss'
function Test-HistoryPort([int]$port) {
    $connection=[Net.Sockets.TcpClient]::new()
    try { return ($connection.ConnectAsync('127.0.0.1',$port).Wait(500) -and $connection.Connected) }
    catch { return $false } finally { $connection.Dispose() }
}
if(-not (Test-Path -LiteralPath (Join-Path $historyRoot 'client/elifoot_flutter/build/web/index.html'))) {throw 'Compile o Web antes de abrir a prévia.'}
if(-not (Test-HistoryPort 5251)) {
    $env:ASPNETCORE_ENVIRONMENT='Development'
    $env:ASPNETCORE_URLS='http://127.0.0.1:5251'
    $env:Elifoot__AuthMode='AccountTest'
    $env:Elifoot__DatabaseProvider='PostgreSQL'
    $env:Elifoot__AutoSeedDefaultWorld='false'
    $env:Accounts__AllowedOrigins__0='http://127.0.0.1:5252'
    $env:Accounts__AllowedOrigins__1='http://localhost:5252'
    $env:ConnectionStrings__DefaultConnection='Host=127.0.0.1;Port=55436;Database=history017c_worlds;Username=qa017a;Command Timeout=180'
    $env:Catalog013A__Enabled='false'
    $env:Catalog013B__Enabled='false'
    $env:Catalog014C__OnlineColorResearch='false'
    $env:Catalog016A__Enabled='false'
    $env:Catalog016B__Enabled='false'
    Start-Process -FilePath dotnet -ArgumentList @("`"$(Join-Path $historyRoot 'artifacts/history-api/Elifoot.Api.dll')`"") -WorkingDirectory (Join-Path $historyRoot 'src/Elifoot.Api') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $historyRuntime "api-$historyStamp.log") -RedirectStandardError (Join-Path $historyRuntime "api-$historyStamp-error.log") | Out-Null
}
if(-not (Test-HistoryPort 5252)) {
    Start-Process -FilePath node -ArgumentList @("`"$(Join-Path $historyRoot 'scripts/serve-web.mjs')`"",'5252') -WorkingDirectory $historyRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $historyRuntime "web-$historyStamp.log") -RedirectStandardError (Join-Path $historyRuntime "web-$historyStamp-error.log") | Out-Null
}
$historyDeadline=[DateTime]::UtcNow.AddSeconds(45)
do {
    try {$health=Invoke-RestMethod 'http://127.0.0.1:5251/api/health' -TimeoutSec 2;if($health.status -eq 'OK'){break}}catch{}
    if([DateTime]::UtcNow -gt $historyDeadline){throw 'API da prévia 017C indisponível.'}
    Start-Sleep -Milliseconds 300
} while($true)
Write-Output 'Prévia 017C: http://127.0.0.1:5252/'
