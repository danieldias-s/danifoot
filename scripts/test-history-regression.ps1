param([string]$TestFilter = '', [string]$LogName = 'history-integration-prepared')
$ErrorActionPreference='Stop'
if($LogName -notmatch '^history-[a-z0-9-]+$'){throw 'Nome do relatório inválido'}
$regressionRoot=Split-Path -Parent $PSScriptRoot
$regressionOutput=Join-Path $regressionRoot 'artifacts/history-regression'
New-Item -ItemType Directory -Path $regressionOutput -Force | Out-Null
$regressionBase='Host=127.0.0.1;Port=55436;Username=qa017a'
$databases=@('catalog13a','worlds13a','catalog14a','worlds14a','reference','market15a','013bimport16a')
foreach($suffix in $databases) {
    $dbName='elifoot_013c_017c_'+$suffix
    if($dbName -notmatch '^elifoot_013c_017c_[a-z0-9]+$'){throw 'Banco de regressão fora do escopo'}
    $exists=& 'C:/Program Files/PostgreSQL/18/bin/psql.exe' -h 127.0.0.1 -p 55436 -U qa017a -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$dbName'"
    if(-not $exists){& 'C:/Program Files/PostgreSQL/18/bin/createdb.exe' -h 127.0.0.1 -p 55436 -U qa017a $dbName;if($LASTEXITCODE -ne 0){throw 'Falha ao criar banco isolado de regressão'}}
}
$env:ELIFOOT_013A_TEST_PG="$regressionBase;Database=elifoot_013c_017c_catalog13a"
$env:ELIFOOT_013A_WORLD_PG="$regressionBase;Database=elifoot_013c_017c_worlds13a"
$env:ELIFOOT_013A_LEGACY_PG="$regressionBase;Database=postgres"
$env:ELIFOOT_014A_CATALOG_PG="$regressionBase;Database=elifoot_013c_017c_catalog14a"
$env:ELIFOOT_014A_WORLD_PG="$regressionBase;Database=elifoot_013c_017c_worlds14a"
$env:ELIFOOT_013C_REAL_CATALOG_PG="$regressionBase;Database=elifoot_013c_017c_reference"
$env:ELIFOOT_015A_MARKET_PG="$regressionBase;Database=elifoot_013c_017c_market15a"
$env:ELIFOOT_016A_IMPORT_TEST_PG="$regressionBase;Database=elifoot_013c_017c_013bimport16a"
$env:ELIFOOT_014B_INPUTS_DIRECTORY='D:/elifoot_98_014B/artifacts/014b/inputs'
$env:ELIFOOT_AUDIT_OUTPUT=$regressionOutput
if(-not (Test-Path -LiteralPath (Join-Path $env:ELIFOOT_014B_INPUTS_DIRECTORY 'Male.input.json')) -or -not (Test-Path -LiteralPath (Join-Path $env:ELIFOOT_014B_INPUTS_DIRECTORY 'Female.input.json'))){throw 'Snapshots de regressão aprovados da 014B não encontrados'}
$failedMethods=Select-String -LiteralPath (Join-Path $regressionRoot 'artifacts/history-integration-final.log') -Pattern '^\[xUnit.*\].*\[FAIL\]' | ForEach-Object {
    ($_.Line -replace '^.*\]\s+','' -replace '\(.*','' -replace '\s+\[FAIL\].*','').Trim()
} | Sort-Object -Unique
if(-not $failedMethods){throw 'Nenhum cenário pendente identificado no relatório inicial'}
$filter=if($TestFilter){$TestFilter}else{($failedMethods | ForEach-Object {'FullyQualifiedName~'+$_}) -join '|'}
$report=Join-Path $regressionRoot "artifacts/$LogName.log"
dotnet test (Join-Path $regressionRoot 'tests/Elifoot.Integration.Tests') --no-restore --filter $filter --nologo *> $report
if($LASTEXITCODE -ne 0){throw "Consulte $report para os cenários pendentes"}
Get-Content -LiteralPath $report -Tail 10
