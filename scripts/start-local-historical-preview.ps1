param([switch]$OpenBrowser)

$ErrorActionPreference = 'Stop'
$previewRoot = Split-Path -Parent $PSScriptRoot
$previewRuntime = Join-Path $previewRoot 'artifacts/local-historical-qa'
$previewWebRoot = Join-Path $previewRuntime 'web'
$previewApi = Join-Path $previewRoot 'src/Elifoot.Api/bin/Release/net10.0/Elifoot.Api.dll'
$previewServer = Join-Path $PSScriptRoot 'serve-web.mjs'
$previewPgBin = 'C:/Program Files/PostgreSQL/18/bin'
$previewPgData = [IO.Path]::GetFullPath((Join-Path $previewRoot '../presentation-versions-017a/artifacts/presentation-017a/postgres-data'))
$previewUrl = 'http://127.0.0.1:5282/#/plantel'
$previewStamp = Get-Date -Format 'yyyyMMdd-HHmmss'

function Test-PreviewPort([int]$Port) {
    $previewSocket = [Net.Sockets.TcpClient]::new()
    try { return ($previewSocket.ConnectAsync('127.0.0.1', $Port).Wait(500) -and $previewSocket.Connected) }
    catch { return $false }
    finally { $previewSocket.Dispose() }
}

function Assert-PreviewPortOwner([int]$Port, [string]$ExpectedCommand) {
    if (-not (Test-PreviewPort $Port)) { return }
    $previewListener = Get-NetTCPConnection -LocalPort $Port -State Listen | Select-Object -First 1
    $previewProcess = Get-CimInstance Win32_Process -Filter "ProcessId=$($previewListener.OwningProcess)"
    if (-not $previewProcess.CommandLine -or
        $previewProcess.CommandLine.IndexOf($ExpectedCommand, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "A porta $Port esta ocupada por outro programa. Nenhum processo foi encerrado."
    }
}

foreach ($previewFile in @(
    $previewApi, $previewServer, (Join-Path $previewWebRoot 'index.html'),
    (Join-Path $previewRuntime 'databases.json'),
    (Join-Path $previewPgData 'PG_VERSION'),
    (Join-Path $previewPgBin 'postgres.exe'),
    (Join-Path $previewPgBin 'pg_isready.exe'),
    (Join-Path $previewPgBin 'psql.exe')
)) {
    if (-not (Test-Path -LiteralPath $previewFile -PathType Leaf)) {
        throw "Arquivo da previa nao encontrado: $previewFile. Nenhum banco sera recriado."
    }
}
$previewDotnet = (Get-Command dotnet -ErrorAction Stop).Source
$previewNode = (Get-Command node -ErrorAction Stop).Source
if (-not (Test-Path -LiteralPath (Join-Path $previewRuntime 'catalogo/paises') -PathType Container)) {
    throw 'O catalogo de QA desta previa nao foi encontrado.'
}
$previewDatabases = Get-Content -LiteralPath (Join-Path $previewRuntime 'databases.json') -Raw | ConvertFrom-Json
foreach ($previewDatabase in @($previewDatabases.Worlds, $previewDatabases.Catalog)) {
    if ($previewDatabase -notmatch '^history_013a_(worlds|catalog)_qa_[a-z0-9]+$') {
        throw 'Configuracao dos bancos de QA invalida.'
    }
}
if ($previewDatabases.Worlds -eq $previewDatabases.Catalog) { throw 'Os bancos de mundos e catalogo precisam ser separados.' }
Assert-PreviewPortOwner 5281 $previewApi
Assert-PreviewPortOwner 5282 $previewServer
Assert-PreviewPortOwner 5282 $previewWebRoot

Write-Output 'Verificando PostgreSQL...'
if (-not (Test-PreviewPort 55436)) {
    Start-Process -FilePath (Join-Path $previewPgBin 'postgres.exe') `
        -ArgumentList @('-D', "`"$previewPgData`"", '-p', '55436', '-h', '127.0.0.1') `
        -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $previewRuntime "postgres-$previewStamp.log") `
        -RedirectStandardError (Join-Path $previewRuntime "postgres-$previewStamp-error.log") | Out-Null
}
$previewDeadline = [DateTime]::UtcNow.AddMinutes(5)
do {
    & (Join-Path $previewPgBin 'pg_isready.exe') -h 127.0.0.1 -p 55436 -U qa017a -d postgres -t 1 | Out-Null
    if ($LASTEXITCODE -eq 0) { break }
    if ([DateTime]::UtcNow -gt $previewDeadline) { throw "PostgreSQL indisponivel. Consulte $previewRuntime." }
    Start-Sleep -Milliseconds 1000
} while ($true)
$previewDatabaseCount = & (Join-Path $previewPgBin 'psql.exe') -w -h 127.0.0.1 -p 55436 -U qa017a -d postgres -tAc "SELECT count(*) FROM pg_database WHERE datname IN ('$($previewDatabases.Worlds)', '$($previewDatabases.Catalog)')"
if ($LASTEXITCODE -ne 0 -or "$previewDatabaseCount".Trim() -ne '2') {
    throw 'Os bancos existentes da previa nao foram encontrados. Nenhum banco vazio foi criado.'
}

Write-Output 'Iniciando API e tela...'
if (-not (Test-PreviewPort 5281)) {
    $previewSettings = @{
        ASPNETCORE_ENVIRONMENT = 'Development'
        ASPNETCORE_URLS = 'http://127.0.0.1:5281'
        Elifoot__AuthMode = 'AccountTest'
        Elifoot__DatabaseProvider = 'PostgreSQL'
        Elifoot__AutoSeedDefaultWorld = 'false'
        Elifoot__DisableBackgroundClock = 'true'
        Accounts__AllowedOrigins__0 = 'http://127.0.0.1:5282'
        Accounts__AllowedOrigins__1 = 'http://localhost:5282'
        Catalog013A__Enabled = 'true'
        Catalog013A__DatabaseProvider = 'PostgreSQL'
        Catalog013A__LoadFixtures = 'false'
        CatalogLocal__UseLegacyProviders = 'false'
        CatalogLocal__ReindexOnStartup = 'false'
        CatalogLocal__Root = (Join-Path $previewRuntime 'catalogo')
        Catalog013B__Enabled = 'false'
        Catalog014C__OnlineColorResearch = 'false'
        Catalog016A__Enabled = 'false'
        Catalog016B__Enabled = 'false'
        ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=55436;Username=qa017a;Command Timeout=180;Database=$($previewDatabases.Worlds)"
        ConnectionStrings__Catalog013A = "Host=127.0.0.1;Port=55436;Username=qa017a;Command Timeout=180;Database=$($previewDatabases.Catalog)"
    }
    $previewPreviousSettings = @{}
    try {
        foreach ($previewName in $previewSettings.Keys) {
            $previewPreviousSettings[$previewName] = [Environment]::GetEnvironmentVariable($previewName, 'Process')
            [Environment]::SetEnvironmentVariable($previewName, $previewSettings[$previewName], 'Process')
        }
        Start-Process -FilePath $previewDotnet -ArgumentList @("`"$previewApi`"") `
            -WorkingDirectory (Join-Path $previewRoot 'src/Elifoot.Api') -WindowStyle Hidden `
            -RedirectStandardOutput (Join-Path $previewRuntime "api-$previewStamp.log") `
            -RedirectStandardError (Join-Path $previewRuntime "api-$previewStamp-error.log") | Out-Null
    }
    finally {
        foreach ($previewName in $previewPreviousSettings.Keys) {
            [Environment]::SetEnvironmentVariable($previewName, $previewPreviousSettings[$previewName], 'Process')
        }
    }
}
if (-not (Test-PreviewPort 5282)) {
    Start-Process -FilePath $previewNode -ArgumentList @("`"$previewServer`"", '5282', "`"$previewWebRoot`"") `
        -WorkingDirectory $previewRoot -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $previewRuntime "web-$previewStamp.log") `
        -RedirectStandardError (Join-Path $previewRuntime "web-$previewStamp-error.log") | Out-Null
}
$previewDeadline = [DateTime]::UtcNow.AddSeconds(60)
do {
    try {
        $previewHealth = Invoke-RestMethod 'http://127.0.0.1:5281/api/health' -TimeoutSec 3
        if ($previewHealth.status -eq 'OK' -and $previewHealth.authMode -eq 'AccountTest') { break }
    } catch { }
    if ([DateTime]::UtcNow -gt $previewDeadline) { throw "API indisponivel. Consulte $previewRuntime." }
    Start-Sleep -Milliseconds 500
} while ($true)
$previewAuthSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
try {
    $previewAuth = Invoke-WebRequest 'http://127.0.0.1:5281/api/auth/csrf' `
        -Headers @{ Origin = 'http://127.0.0.1:5282' } -WebSession $previewAuthSession `
        -UseBasicParsing -TimeoutSec 10
    $previewAuthBody = $previewAuth.Content | ConvertFrom-Json
    if ($previewAuth.StatusCode -ne 200 -or [string]::IsNullOrWhiteSpace($previewAuthBody.csrfToken) -or
        $previewAuth.Headers['Access-Control-Allow-Origin'] -ne 'http://127.0.0.1:5282' -or
        $previewAuthSession.Cookies.GetCookies([Uri]'http://127.0.0.1:5281/').Count -eq 0) {
        throw 'A API nao preparou o formulario de acesso.'
    }
} catch {
    throw "A API responde, mas a autenticacao esta indisponivel. Inicie a previa pelo INICIAR-CATALOGO-LOCAL.cmd no Windows. Consulte $previewRuntime."
}
$previewResponse = Invoke-WebRequest 'http://127.0.0.1:5282/' -UseBasicParsing -TimeoutSec 10
if ($previewResponse.StatusCode -ne 200) { throw 'Tela da previa indisponivel.' }
Write-Output "Sistema pronto: $previewUrl"
Write-Output 'Esta previa utiliza os dados sinteticos de QA do catalogo historico.'
if ($OpenBrowser) { Start-Process $previewUrl }
