param()
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if ($taskRoot -ne 'D:\elifoot_98_013A') { throw 'Worktree 013B isolado obrigatório.' }
$pgBin = 'C:\Program Files\PostgreSQL\18\bin'
$auditDir = Join-Path $taskRoot 'docs\audit-013b-coverage'
$artifactDir = Join-Path $taskRoot 'artifacts\013b-coverage'
$catalogSql = Join-Path $artifactDir 'snapshots.sql'
$worldsSql = Join-Path $artifactDir 'worlds.sql'
@'
SELECT coalesce(json_agg(row_to_json(x)), '[]'::json) FROM (SELECT s."Id" AS id, s."ScopeKey" AS scope, s."Version" AS version, s."Hash" AS hash, s."IsFixture" AS fixture, s."UpdatedAt" AS updated_at, (c."ActiveSnapshotId" = s."Id") AS active FROM "wc013a_CatalogSnapshot" s JOIN "wc013a_CatalogScope" c ON c."Key" = s."ScopeKey" ORDER BY s."Id") x;
'@ | Set-Content $catalogSql -Encoding utf8
@'
SELECT coalesce(json_agg(row_to_json(x)), '[]'::json) FROM (SELECT "Id" AS id, "Name" AS name, md5("SnapshotJson") AS snapshot_md5 FROM ef_worlds ORDER BY "Id") x;
'@ | Set-Content $worldsSql -Encoding utf8
function Read-Evidence([string]$phase) {
    & "$pgBin\psql.exe" -h 127.0.0.1 -p 55434 -U elifoot013b -d elifoot_013b_catalog -At -f $catalogSql | Set-Content (Join-Path $auditDir "snapshots-$phase.json") -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw 'Leitura do catálogo falhou.' }
    & "$pgBin\psql.exe" -h 127.0.0.1 -p 55434 -U elifoot013b -d elifoot_013b_worlds -At -f $worldsSql | Set-Content (Join-Path $auditDir "worlds-$phase.json") -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw 'Leitura de mundos falhou.' }
}
Read-Evidence 'before-restart'
& (Join-Path $PSScriptRoot 'test-real-data-013b.ps1') -Action Stop
if ($LASTEXITCODE -ne 0) { throw 'Parada isolada falhou.' }
$pgData = Join-Path $taskRoot 'artifacts\013b\pgdata'
$startLog = Join-Path $artifactDir 'postgres-start.log'
$startError = Join-Path $artifactDir 'postgres-start-error.log'
$pgLog = Join-Path $taskRoot 'artifacts\013b\postgres.log'
$starter = Start-Process -FilePath (Join-Path $pgBin 'pg_ctl.exe') -ArgumentList @('-D', $pgData, '-l', $pgLog, '-o', '"-p 55434 -h 127.0.0.1"', '-w', 'start') -WindowStyle Hidden -RedirectStandardOutput $startLog -RedirectStandardError $startError -PassThru
if (-not $starter.WaitForExit(30000) -or $starter.ExitCode -ne 0) { throw 'Reinício isolado falhou; conferir logs locais.' }
Read-Evidence 'after-restart'
foreach ($type in 'snapshots','worlds') {
    if ((Get-Content -Raw (Join-Path $auditDir "$type-before-restart.json")).Trim() -ne (Get-Content -Raw (Join-Path $auditDir "$type-after-restart.json")).Trim()) { throw "$type mudou após reinício." }
}
$env:ELIFOOT_013B_CATALOG_PG = 'Host=127.0.0.1;Port=55434;Database=elifoot_013b_catalog;Username=elifoot013b'
dotnet run --project (Join-Path $taskRoot 'tools/catalog-013b/importer') -c Release -- coverage-audit $auditDir ELIFOOT_013B_CATALOG_PG
if ($LASTEXITCODE -ne 0) { throw 'Auditoria offline/persistência falhou.' }
$backupDir = Join-Path $artifactDir 'backups'
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
foreach ($database in 'elifoot_013b_catalog','elifoot_013b_worlds') {
    & "$pgBin\pg_dump.exe" -h 127.0.0.1 -p 55434 -U elifoot013b -d $database -Fc -f (Join-Path $backupDir "$database-after.dump")
    if ($LASTEXITCODE -ne 0) { throw 'Backup pós-auditoria falhou.' }
}
$backups = Get-ChildItem -LiteralPath $backupDir -Filter '*.dump' | ForEach-Object { @{ path = $_.FullName; bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } }
@{ postgresRestart = 'PASS'; snapshotHashesUnchanged = $true; existingWorldHashesUnchanged = $true; backups = @($backups) } | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $auditDir 'restart-verification.json') -Encoding utf8
