param(
    [ValidateSet('creator5252_013a_worlds', 'creator5252_013a_smoke')]
    [string]$WorldDatabase = 'creator5252_013a_worlds',
    [ValidateSet(5251, 5253)]
    [int]$ApiPort = 5251,
    [switch]$ApiOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$runtime = Join-Path $repoRoot 'artifacts/world-creator-preview'
$apiDll = Join-Path $runtime 'api/Elifoot.Api.dll'
$webRoot = Join-Path $runtime 'web'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'

function Test-PreviewPort([int]$Port) {
    $client = [Net.Sockets.TcpClient]::new()
    try { return $client.ConnectAsync('127.0.0.1', $Port).Wait(300) -and $client.Connected }
    catch { return $false }
    finally { $client.Dispose() }
}

if (-not (Test-Path -LiteralPath $apiDll)) { throw 'Compile a API da prévia.' }
if (-not $ApiOnly -and -not (Test-Path -LiteralPath (Join-Path $webRoot 'index.html'))) {
    throw 'Compile o Web da prévia.'
}

if (-not (Test-PreviewPort $ApiPort)) {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = "http://127.0.0.1:$ApiPort"
    $env:Elifoot__AuthMode = 'AccountTest'
    $env:Elifoot__DatabaseProvider = 'PostgreSQL'
    $env:Elifoot__AutoSeedDefaultWorld = 'false'
    $env:Accounts__AllowedOrigins__0 = 'http://127.0.0.1:5252'
    $env:Accounts__AllowedOrigins__1 = 'http://localhost:5252'
    $localPg = 'Host=127.0.0.1;Port=55436;Username=qa017a;Command Timeout=180'
    $env:ConnectionStrings__DefaultConnection = "$localPg;Database=$WorldDatabase"
    $env:ConnectionStrings__Catalog013A = "$localPg;Database=creator5252_013a_catalog"
    $env:Catalog013A__Enabled = 'true'
    $env:Catalog013A__DatabaseProvider = 'PostgreSQL'
    $env:Catalog013A__LoadFixtures = 'false'
    $env:Catalog013B__Enabled = 'false'
    $env:Catalog014C__OnlineColorResearch = 'false'
    $env:Catalog016A__Enabled = 'true'
    $env:Catalog016B__Enabled = 'true'
    Start-Process -FilePath dotnet -ArgumentList @("`"$apiDll`"") `
        -WorkingDirectory (Join-Path $repoRoot 'src/Elifoot.Api') -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $runtime "api-$ApiPort-$stamp.log") `
        -RedirectStandardError (Join-Path $runtime "api-$ApiPort-$stamp-error.log") | Out-Null
}

$deadline = [DateTime]::UtcNow.AddSeconds(45)
do {
    try {
        $health = Invoke-RestMethod "http://127.0.0.1:$ApiPort/api/health" -TimeoutSec 2
        if ($health.status -eq 'OK' -and $health.authMode -eq 'AccountTest') { break }
    } catch {}
    if ([DateTime]::UtcNow -gt $deadline) { throw "API da prévia indisponível na porta $ApiPort." }
    Start-Sleep -Milliseconds 300
} while ($true)

if (-not $ApiOnly -and -not (Test-PreviewPort 5252)) {
    $staticScript = Join-Path $PSScriptRoot 'serve-web.mjs'
    Start-Process -FilePath node -ArgumentList @("`"$staticScript`"", '5252', "`"$webRoot`"") `
        -WorkingDirectory $repoRoot -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $runtime "web-$stamp.log") `
        -RedirectStandardError (Join-Path $runtime "web-$stamp-error.log") | Out-Null
}
