$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$runtimeDir = Join-Path $taskRoot 'artifacts/account-login'
New-Item -ItemType Directory -Path $runtimeDir -Force | Out-Null
if (-not (Test-Path -LiteralPath (Join-Path $taskRoot 'client/elifoot_flutter/build/web/index.html'))) { throw 'Compile a versão Web antes de iniciar esta prévia.' }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
function Is-AccountPortOpen([int] $port) {
    $client = [Net.Sockets.TcpClient]::new()
    try { return ($client.ConnectAsync('127.0.0.1', $port).Wait(500) -and $client.Connected) }
    catch { return $false } finally { $client.Dispose() }
}
if (-not (Is-AccountPortOpen 55436)) {
    $data = [IO.Path]::GetFullPath((Join-Path $taskRoot '../presentation-versions-017a/artifacts/presentation-017a/postgres-data'))
    if (-not (Test-Path -LiteralPath (Join-Path $data 'PG_VERSION'))) { throw 'PostgreSQL local não encontrado. Nenhum banco foi recriado.' }
    Start-Process -FilePath 'C:/Program Files/PostgreSQL/18/bin/postgres.exe' -ArgumentList @('-D', "`"$data`"", '-p', '55436', '-h', '127.0.0.1') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runtimeDir "pg-$stamp.log") -RedirectStandardError (Join-Path $runtimeDir "pg-$stamp-error.log") | Out-Null
}
$readyUntil = [DateTime]::UtcNow.AddMinutes(3)
do {
    & 'C:/Program Files/PostgreSQL/18/bin/pg_isready.exe' -h 127.0.0.1 -p 55436 -U qa017a -t 1 | Out-Null
    if ($LASTEXITCODE -eq 0) { break }
    if ([DateTime]::UtcNow -gt $readyUntil) { throw 'PostgreSQL ainda indisponível. Consulte os logs.' }
    Start-Sleep -Milliseconds 500
} while ($true)
$requiredDatabases = & 'C:/Program Files/PostgreSQL/18/bin/psql.exe' -h 127.0.0.1 -p 55436 -U qa017a -d postgres -tAc "SELECT count(*) FROM pg_database WHERE datname IN ('account018_013a_worlds','account018_013a_catalog')"
if ($LASTEXITCODE -ne 0 -or $requiredDatabases.Trim() -ne '2') { throw 'Bancos da prévia de contas não encontrados. Restaure os clones preparados; nenhum banco vazio será criado por este iniciador.' }
if (-not (Is-AccountPortOpen 5241)) {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = 'http://127.0.0.1:5241'
    $env:Elifoot__AuthMode = 'AccountTest'
    $env:Elifoot__DatabaseProvider = 'PostgreSQL'
    $env:Elifoot__AutoSeedDefaultWorld = 'false'
    $env:ConnectionStrings__DefaultConnection = 'Host=127.0.0.1;Port=55436;Database=account018_013a_worlds;Username=qa017a'
    $env:ConnectionStrings__Catalog013A = 'Host=127.0.0.1;Port=55436;Database=account018_013a_catalog;Username=qa017a'
    $env:Catalog013A__Enabled = 'true'
    $env:Catalog013A__DatabaseProvider = 'PostgreSQL'
    $env:Catalog013A__LoadFixtures = 'false'
    $env:Catalog013B__Enabled = 'false'
    $env:Catalog014C__OnlineColorResearch = 'false'
    $env:Catalog016A__Enabled = 'true'
    $env:Catalog016B__Enabled = 'true'
    foreach ($name in @('migration-admin', 'catalog-admin')) {
        $file = Join-Path $runtimeDir "$name.secret"
        if (-not (Test-Path -LiteralPath $file)) {
            $secretBytes = New-Object byte[] 32
            $randomSource = [Security.Cryptography.RandomNumberGenerator]::Create()
            try { $randomSource.GetBytes($secretBytes) } finally { $randomSource.Dispose() }
            [IO.File]::WriteAllText($file, [BitConverter]::ToString($secretBytes).Replace('-', ''))
        }
    }
    $env:Accounts__MigrationAdminToken = [IO.File]::ReadAllText((Join-Path $runtimeDir 'migration-admin.secret'))
    $env:Catalog016A__AdminToken = [IO.File]::ReadAllText((Join-Path $runtimeDir 'catalog-admin.secret'))
    Start-Process -FilePath dotnet -ArgumentList @("`"$(Join-Path $taskRoot 'src/Elifoot.Api/bin/Release/net10.0/Elifoot.Api.dll')`"") -WorkingDirectory (Join-Path $taskRoot 'src/Elifoot.Api') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runtimeDir "api-$stamp.log") -RedirectStandardError (Join-Path $runtimeDir "api-$stamp-error.log") | Out-Null
}
if (-not (Is-AccountPortOpen 5242)) {
    Start-Process -FilePath node -ArgumentList @("`"$(Join-Path $taskRoot 'scripts/serve-web.mjs')`"", '5242') -WorkingDirectory $taskRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runtimeDir "web-$stamp.log") -RedirectStandardError (Join-Path $runtimeDir "web-$stamp-error.log") | Out-Null
}
$apiDeadline = [DateTime]::UtcNow.AddSeconds(45)
do {
    try { $health = Invoke-RestMethod 'http://127.0.0.1:5241/api/health' -TimeoutSec 3; if ($health.status -eq 'OK' -and $health.authMode -eq 'AccountTest') { break } } catch { }
    if ([DateTime]::UtcNow -gt $apiDeadline) { throw 'API de contas indisponível. Consulte os logs em artifacts/account-login.' }
    Start-Sleep -Milliseconds 500
} while ($true)
$web = Invoke-WebRequest 'http://127.0.0.1:5242/' -UseBasicParsing -TimeoutSec 10
if ($web.StatusCode -ne 200) { throw 'O build da página não está disponível.' }
Write-Output 'Prévia de contas pronta: http://127.0.0.1:5242/'
