$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if($taskRoot -ne 'D:\elifoot_98_016B'){throw 'Runner exclusivo do worktree 016B.'}
$taskPgBin='C:\Program Files\PostgreSQL\18\bin'
$taskArea=Join-Path $taskRoot 'artifacts\016b'
$taskPgData=Join-Path $taskArea 'pgdata'
New-Item -ItemType Directory -Force -Path $taskArea | Out-Null
$taskPasswordFile=Join-Path $taskArea 'pg-password.txt'
if(-not(Test-Path -LiteralPath $taskPasswordFile)){
 [IO.File]::WriteAllText($taskPasswordFile,[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)))
}
if(-not(Test-Path -LiteralPath (Join-Path $taskPgData 'PG_VERSION'))){
 if(Get-NetTCPConnection -LocalPort 55442 -State Listen -ErrorAction SilentlyContinue){throw 'Porta 55442 ocupada; serviço existente preservado.'}
 & "$taskPgBin\initdb.exe" -D $taskPgData -U elifoot016b -A scram-sha-256 --pwfile=$taskPasswordFile --encoding UTF8 --no-locale *> (Join-Path $taskArea 'pg-init.log')
 if($LASTEXITCODE -ne 0){throw 'Falha ao criar o cluster 016B; verificar pg-init.log.'}
}
& "$taskPgBin\pg_ctl.exe" -D $taskPgData status *> $null
if($LASTEXITCODE -ne 0){
 if(Get-NetTCPConnection -LocalPort 55442 -State Listen -ErrorAction SilentlyContinue){throw 'Porta 55442 ocupada; serviço existente preservado.'}
 $taskStarter=Start-Process -FilePath "$taskPgBin\pg_ctl.exe" -ArgumentList @('-D',$taskPgData,'-l',(Join-Path $taskArea 'postgres.log'),'-o','"-p 55442 -h 127.0.0.1"','-w','-t','60','start') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $taskArea 'pg-start.log') -RedirectStandardError (Join-Path $taskArea 'pg-start-error.log') -PassThru
 if(-not $taskStarter.WaitForExit(60000) -or $taskStarter.ExitCode -ne 0){throw 'Banco 016B não confirmou início; verificar logs antes de repetir.'}
}
$env:PGPASSWORD=[IO.File]::ReadAllText($taskPasswordFile).Trim()
try{
 foreach($taskDb in 'catalog','worlds','tests','catalog_real','worlds_real','market_tests','catalog_dev','worlds_dev','import_tests'){
  $taskDbName='elifoot_016b_013b_'+$taskDb
  $taskExists=& "$taskPgBin\psql.exe" -h 127.0.0.1 -p 55442 -U elifoot016b -d postgres -tAc "SELECT datname FROM pg_database WHERE datname='$taskDbName'"
  if($LASTEXITCODE -ne 0){throw 'Falha na consulta do cluster 016B.'}
  if(-not $taskExists){& "$taskPgBin\createdb.exe" -h 127.0.0.1 -p 55442 -U elifoot016b $taskDbName;if($LASTEXITCODE -ne 0){throw 'Falha ao criar banco 016B.'}}
 }
 $taskBase="Host=127.0.0.1;Port=55442;Username=elifoot016b;Password=$env:PGPASSWORD"
 $env:ELIFOOT_013A_TEST_PG="$taskBase;Database=elifoot_016b_013b_tests"
 $env:ELIFOOT_013A_WORLD_PG="$taskBase;Database=elifoot_016b_013b_worlds"
 $env:ELIFOOT_013A_LEGACY_PG="$taskBase;Database=postgres"
 $env:ELIFOOT_014A_CATALOG_PG="$taskBase;Database=elifoot_016b_013b_catalog"
 $env:ELIFOOT_014A_WORLD_PG=$env:ELIFOOT_013A_WORLD_PG
 $env:ELIFOOT_013C_REAL_CATALOG_PG="$taskBase;Database=elifoot_016b_013b_catalog_real"
 $env:ELIFOOT_013C_REAL_WORLD_PG="$taskBase;Database=elifoot_016b_013b_worlds_real"
 $env:ELIFOOT_014B_REAL_WORLD_PG=$env:ELIFOOT_013C_REAL_WORLD_PG
 $env:ELIFOOT_014B_REAL_CATALOG_PG=$env:ELIFOOT_013C_REAL_CATALOG_PG
 $env:ELIFOOT_016B_CATALOG_PG="$taskBase;Database=elifoot_016b_013b_catalog_dev"
 $env:ELIFOOT_016B_WORLD_PG="$taskBase;Database=elifoot_016b_013b_worlds_dev"
 $env:ELIFOOT_016A_IMPORT_TEST_PG="$taskBase;Database=elifoot_016b_013b_import_tests"
 $env:ELIFOOT_015A_MARKET_PG="$taskBase;Database=elifoot_016b_013b_market_tests"
}finally{Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue}
$env:ELIFOOT_014B_INPUTS_DIRECTORY=Join-Path $taskArea 'inputs'
$env:ELIFOOT_014C_INPUTS_DIRECTORY=$env:ELIFOOT_014B_INPUTS_DIRECTORY
$env:ELIFOOT_AUDIT_OUTPUT=Join-Path $taskArea 'regression'
New-Item -ItemType Directory -Force $env:ELIFOOT_014B_INPUTS_DIRECTORY,$env:ELIFOOT_AUDIT_OUTPUT | Out-Null
$taskSourceInputs='D:\elifoot_98_015B\artifacts\015b\inputs'
if(Test-Path -LiteralPath $taskSourceInputs){
 Get-ChildItem -LiteralPath $taskSourceInputs -File | ForEach-Object {
  $taskDestination=Join-Path $env:ELIFOOT_014B_INPUTS_DIRECTORY $_.Name
  if(-not(Test-Path -LiteralPath $taskDestination)){Copy-Item -LiteralPath $_.FullName -Destination $taskDestination}
 }
}
$taskRankingFixture=Join-Path $env:ELIFOOT_AUDIT_OUTPUT 'real-season-world.json'
if(-not(Test-Path -LiteralPath $taskRankingFixture)){
 $taskSource='D:\elifoot_98_014C\docs\audit-014c\verified-final\real-season-world.json'
 if(Test-Path -LiteralPath $taskSource){Copy-Item -LiteralPath $taskSource -Destination $taskRankingFixture}
}
Write-Output '016B: PostgreSQL isolado 55442; dados e relatórios em artifacts/016b.'
