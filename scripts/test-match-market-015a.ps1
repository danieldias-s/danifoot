$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if($taskRoot -ne 'D:\elifoot_98_015A'){throw 'Runner exclusivo do worktree 015A.'}
$taskPgBin='C:\Program Files\PostgreSQL\18\bin'
$taskArea=Join-Path $taskRoot 'artifacts\015a'
$taskPgData=Join-Path $taskArea 'pgdata'
New-Item -ItemType Directory -Force -Path $taskArea | Out-Null
$taskPasswordFile=Join-Path $taskArea 'pg-password.txt'
if(-not(Test-Path -LiteralPath $taskPasswordFile)){
 [IO.File]::WriteAllText($taskPasswordFile,[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)))
}
if(-not(Test-Path -LiteralPath (Join-Path $taskPgData 'PG_VERSION'))){
 if(Get-NetTCPConnection -LocalPort 55439 -State Listen -ErrorAction SilentlyContinue){throw 'Porta 55439 ocupada; serviço existente preservado.'}
 & "$taskPgBin\initdb.exe" -D $taskPgData -U elifoot015a -A scram-sha-256 --pwfile=$taskPasswordFile --encoding UTF8 --no-locale *> (Join-Path $taskArea 'pg-init.log')
 if($LASTEXITCODE -ne 0){throw 'Falha ao criar o cluster 015A; verificar pg-init.log.'}
}
& "$taskPgBin\pg_ctl.exe" -D $taskPgData status *> $null
if($LASTEXITCODE -ne 0){
 if(Get-NetTCPConnection -LocalPort 55439 -State Listen -ErrorAction SilentlyContinue){throw 'Porta 55439 ocupada; serviço existente preservado.'}
 $taskStarter=Start-Process -FilePath "$taskPgBin\pg_ctl.exe" -ArgumentList @('-D',$taskPgData,'-l',(Join-Path $taskArea 'postgres.log'),'-o','"-p 55439 -h 127.0.0.1"','-w','-t','60','start') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $taskArea 'pg-start.log') -RedirectStandardError (Join-Path $taskArea 'pg-start-error.log') -PassThru
 if(-not $taskStarter.WaitForExit(60000) -or $taskStarter.ExitCode -ne 0){throw 'Banco 015A não confirmou início; verificar logs antes de repetir.'}
}
$env:PGPASSWORD=[IO.File]::ReadAllText($taskPasswordFile).Trim()
try{
 foreach($taskDb in 'catalog','worlds','tests','catalog_real','worlds_real','market_tests'){
  $taskDbName='elifoot_015a_013b_'+$taskDb
  $taskExists=& "$taskPgBin\psql.exe" -h 127.0.0.1 -p 55439 -U elifoot015a -d postgres -tAc "SELECT datname FROM pg_database WHERE datname='$taskDbName'"
  if($LASTEXITCODE -ne 0){throw 'Falha na consulta do cluster 015A.'}
  if(-not $taskExists){& "$taskPgBin\createdb.exe" -h 127.0.0.1 -p 55439 -U elifoot015a $taskDbName;if($LASTEXITCODE -ne 0){throw 'Falha ao criar banco 015A.'}}
 }
 $taskBase="Host=127.0.0.1;Port=55439;Username=elifoot015a;Password=$env:PGPASSWORD"
 $env:ELIFOOT_013A_TEST_PG="$taskBase;Database=elifoot_015a_013b_tests"
 $env:ELIFOOT_013A_WORLD_PG="$taskBase;Database=elifoot_015a_013b_worlds"
 $env:ELIFOOT_013A_LEGACY_PG="$taskBase;Database=postgres"
 $env:ELIFOOT_014A_CATALOG_PG="$taskBase;Database=elifoot_015a_013b_catalog"
 $env:ELIFOOT_014A_WORLD_PG=$env:ELIFOOT_013A_WORLD_PG
 $env:ELIFOOT_013C_REAL_CATALOG_PG="$taskBase;Database=elifoot_015a_013b_catalog_real"
 $env:ELIFOOT_013C_REAL_WORLD_PG="$taskBase;Database=elifoot_015a_013b_worlds_real"
 $env:ELIFOOT_014B_REAL_WORLD_PG=$env:ELIFOOT_013C_REAL_WORLD_PG
 $env:ELIFOOT_014B_REAL_CATALOG_PG=$env:ELIFOOT_013C_REAL_CATALOG_PG
 $env:ELIFOOT_015A_MARKET_PG="$taskBase;Database=elifoot_015a_013b_market_tests"
}finally{Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue}
$env:ELIFOOT_014B_INPUTS_DIRECTORY=Join-Path $taskArea 'inputs'
$env:ELIFOOT_014C_INPUTS_DIRECTORY=$env:ELIFOOT_014B_INPUTS_DIRECTORY
$env:ELIFOOT_AUDIT_OUTPUT=Join-Path $taskArea 'regression'
New-Item -ItemType Directory -Force $env:ELIFOOT_014B_INPUTS_DIRECTORY,$env:ELIFOOT_AUDIT_OUTPUT | Out-Null
$taskSourceInputs='D:\elifoot_98_014C\artifacts\014c\inputs'
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
Write-Output '015A: PostgreSQL isolado 55439; dados e relatórios em artifacts/015a.'
