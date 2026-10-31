param([ValidateSet('Start','Stop')][string]$Action='Start')
$ErrorActionPreference='Stop'
$env:ELIFOOT_AUDIT_OUTPUT='D:\elifoot_98_014C\docs\audit-014c\verified-final'
$taskRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if($taskRoot -ne 'D:\elifoot_98_014C'){throw 'Runner exclusivo do worktree 014C.'}
$pgBin='C:\Program Files\PostgreSQL\18\bin'
$pgArea=Join-Path $taskRoot 'artifacts\014c'
$pgData=Join-Path $pgArea 'pgdata'
if(-not ([IO.Path]::GetFullPath($pgData)).StartsWith($taskRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Dados fora da integração.'}
if($Action -eq 'Stop'){
 if(Test-Path -LiteralPath (Join-Path $pgData 'postmaster.pid')){& "$pgBin\pg_ctl.exe" -D $pgData -m fast -w stop; if($LASTEXITCODE -ne 0){throw 'Falha ao parar a instância 014C.'}}
 return
}
New-Item -ItemType Directory -Force -Path $pgArea | Out-Null
$passwordFile=Join-Path $pgArea 'pg-password.txt'
if(-not(Test-Path -LiteralPath $passwordFile)){
 [IO.File]::WriteAllText($passwordFile,[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)))
}
if(-not(Test-Path -LiteralPath (Join-Path $pgData 'PG_VERSION'))){
 if(Get-NetTCPConnection -LocalPort 55438 -State Listen -ErrorAction SilentlyContinue){throw 'Porta 55438 ocupada; outra instância não será usada.'}
 & "$pgBin\initdb.exe" -D $pgData -U elifoot014c -A scram-sha-256 --pwfile=$passwordFile --encoding UTF8 --no-locale
 if($LASTEXITCODE -ne 0){throw 'Falha no initdb 014C.'}
}
& "$pgBin\pg_ctl.exe" -D $pgData status *> $null
if($LASTEXITCODE -ne 0){
 if(Get-NetTCPConnection -LocalPort 55438 -State Listen -ErrorAction SilentlyContinue){throw 'Porta 55438 ocupada; outra instância não será usada.'}
 $starter=Start-Process -FilePath (Join-Path $pgBin 'pg_ctl.exe') -ArgumentList @('-D',$pgData,'-l',(Join-Path $pgArea 'postgres.log'),'-o','"-p 55438 -h 127.0.0.1"','-w','start') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $pgArea 'start.log') -RedirectStandardError (Join-Path $pgArea 'start-error.log') -PassThru
 if(-not $starter.WaitForExit(30000) -or $starter.ExitCode -ne 0){throw 'PostgreSQL 014C não iniciou; conferir artifacts/014c/start-error.log.'}
}
$env:PGPASSWORD=[IO.File]::ReadAllText($passwordFile).Trim()
try{
 foreach($database in 'elifoot_014c_013b_catalog','elifoot_014c_013b_worlds','elifoot_014c_013b_tests','elifoot_014c_013b_catalog_real','elifoot_014c_013b_worlds_real'){
  $exists=& "$pgBin\psql.exe" -h 127.0.0.1 -p 55438 -U elifoot014c -d postgres -tAc "SELECT datname FROM pg_database WHERE datname='$database'"
  if($LASTEXITCODE -ne 0){throw 'Falha ao verificar bancos 014C.'}
  if(-not $exists){& "$pgBin\createdb.exe" -h 127.0.0.1 -p 55438 -U elifoot014c $database;if($LASTEXITCODE -ne 0){throw 'Falha ao criar banco 014C.'}}
 }
 $base="Host=127.0.0.1;Port=55438;Username=elifoot014c;Password=$env:PGPASSWORD"
 $env:ELIFOOT_013A_TEST_PG="$base;Database=elifoot_014c_013b_tests"
 $env:ELIFOOT_013A_WORLD_PG="$base;Database=elifoot_014c_013b_worlds"
 $env:ELIFOOT_013A_LEGACY_PG="$base;Database=postgres"
 $env:ELIFOOT_014C_CATALOG_PG="$base;Database=elifoot_014c_013b_catalog"
 $env:ELIFOOT_014C_WORLD_PG=$env:ELIFOOT_013A_WORLD_PG
 $env:ELIFOOT_014C_REAL_CATALOG_PG="$base;Database=elifoot_014c_013b_catalog_real"
 $env:ELIFOOT_014C_REAL_WORLD_PG="$base;Database=elifoot_014c_013b_worlds_real"
 $env:ELIFOOT_013C_REAL_CATALOG_PG=$env:ELIFOOT_014C_REAL_CATALOG_PG
 $env:ELIFOOT_013C_REAL_WORLD_PG=$env:ELIFOOT_014C_REAL_WORLD_PG
 $env:ELIFOOT_014C_INPUTS_DIRECTORY=Join-Path $taskRoot 'artifacts/014c/inputs'
 $env:ELIFOOT_014A_CATALOG_PG=$env:ELIFOOT_014C_CATALOG_PG
 $env:ELIFOOT_014A_WORLD_PG=$env:ELIFOOT_014C_WORLD_PG
}finally{Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue}
Write-Output 'PostgreSQL exclusivo 014C: loopback 55438, credenciais próprias em artifacts/014c (fora do Git).'
$env:ELIFOOT_014B_INPUTS_DIRECTORY=$env:ELIFOOT_014C_INPUTS_DIRECTORY
$env:ELIFOOT_014B_REAL_WORLD_PG=$env:ELIFOOT_014C_REAL_WORLD_PG
$env:ELIFOOT_014B_REAL_CATALOG_PG=$env:ELIFOOT_014C_REAL_CATALOG_PG

# Regression reuses the completed real-season artifact; no additional long simulation.
$rankingsArtifact=Join-Path $env:ELIFOOT_AUDIT_OUTPUT 'real-season-world.json'
if(-not(Test-Path -LiteralPath $rankingsArtifact)){
 $preservedArtifact='D:\elifoot_98_014B\docs\audit-014b\verified-final\real-season-world.json'
 if(Test-Path -LiteralPath $preservedArtifact){
  New-Item -ItemType Directory -Force $env:ELIFOOT_AUDIT_OUTPUT | Out-Null
  Copy-Item -LiteralPath $preservedArtifact -Destination $rankingsArtifact
 }
}
