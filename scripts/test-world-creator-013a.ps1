param([ValidateSet('Start','Stop','Test')][string]$Action = 'Test')
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if ($taskRoot -ne 'D:\elifoot_98_013A') { throw 'Execute somente no worktree isolado D:\elifoot_98_013A.' }
$pgBin = 'C:\Program Files\PostgreSQL\18\bin'
$pgData = Join-Path $taskRoot 'artifacts\013a\pgdata'
if (-not $pgData.StartsWith($taskRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Caminho de dados inválido.' }
if ($Action -eq 'Stop') {
    if (Test-Path -LiteralPath (Join-Path $pgData 'postmaster.pid')) { & "$pgBin\pg_ctl.exe" -D $pgData -m fast -w stop }
    exit $LASTEXITCODE
}
New-Item -ItemType Directory -Force -Path (Join-Path $taskRoot 'artifacts\013a') | Out-Null
if (-not (Test-Path -LiteralPath (Join-Path $pgData 'PG_VERSION'))) {
    & "$pgBin\initdb.exe" -D $pgData -U elifoot013a -A trust --encoding UTF8 --no-locale
    if ($LASTEXITCODE -ne 0) { throw 'initdb falhou.' }
}
& "$pgBin\pg_ctl.exe" -D $pgData status > $null 2>&1
if ($LASTEXITCODE -ne 0) {
    if (Get-NetTCPConnection -LocalPort 55433 -State Listen -ErrorAction SilentlyContinue) { throw 'Porta 55433 ocupada; nenhum servidor existente será utilizado.' }
    & "$pgBin\pg_ctl.exe" -D $pgData -l (Join-Path $taskRoot 'artifacts\013a\postgres.log') -o '-p 55433 -h 127.0.0.1' -w start
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL isolado não iniciou.' }
}
foreach ($dbName in 'elifoot_013a_catalog','elifoot_013a_worlds','elifoot_013a_tests') {
    $existing = & "$pgBin\psql.exe" -h 127.0.0.1 -p 55433 -U elifoot013a -d postgres -tAc "SELECT datname FROM pg_database WHERE datname='$dbName'"
    if (-not $existing) { & "$pgBin\createdb.exe" -h 127.0.0.1 -p 55433 -U elifoot013a $dbName }
}
if ($Action -eq 'Test') {
    $env:ELIFOOT_013A_TEST_PG = 'Host=127.0.0.1;Port=55433;Database=elifoot_013a_tests;Username=elifoot013a'
    $env:ELIFOOT_013A_WORLD_PG = 'Host=127.0.0.1;Port=55433;Database=elifoot_013a_worlds;Username=elifoot013a'
    $env:ELIFOOT_013A_LEGACY_PG = 'Host=127.0.0.1;Port=55433;Database=postgres;Username=elifoot013a'
    Push-Location $taskRoot
    try { dotnet test Elifoot.slnx -c Release --filter 'FullyQualifiedName!~MultiSeasonAndBenchmarkTests'; $testExit = $LASTEXITCODE }
    finally { Pop-Location }
    exit $testExit
}
Write-Output 'PostgreSQL 013A disponível em 127.0.0.1:55433; encerrar com -Action Stop.'
