param([ValidateSet('Start','Stop')][string]$Action='Start')
$ErrorActionPreference='Stop'
$env:ELIFOOT_AUDIT_OUTPUT='D:\elifoot_98_integration\docs\audit-013c\verified-final'
$taskRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if($taskRoot -ne 'D:\elifoot_98_integration'){throw 'Runner exclusivo do worktree 013C.'}
$pgBin='C:\Program Files\PostgreSQL\18\bin'
$pgArea=Join-Path $taskRoot 'artifacts\013c'
$pgData=Join-Path $pgArea 'pgdata'
if(-not ([IO.Path]::GetFullPath($pgData)).StartsWith($taskRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Dados fora da integração.'}
if($Action -eq 'Stop'){
 if(Test-Path -LiteralPath (Join-Path $pgData 'postmaster.pid')){& "$pgBin\pg_ctl.exe" -D $pgData -m fast -w stop; if($LASTEXITCODE -ne 0){throw 'Falha ao parar a instância 013C.'}}
 return
}
New-Item -ItemType Directory -Force -Path $pgArea | Out-Null
$passwordFile=Join-Path $pgArea 'pg-password.txt'
if(-not(Test-Path -LiteralPath $passwordFile)){
 [IO.File]::WriteAllText($passwordFile,[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)))
}
if(-not(Test-Path -LiteralPath (Join-Path $pgData 'PG_VERSION'))){
 if(Get-NetTCPConnection -LocalPort 55436 -State Listen -ErrorAction SilentlyContinue){throw 'Porta 55436 ocupada; outra instância não será usada.'}
 & "$pgBin\initdb.exe" -D $pgData -U elifoot013c -A scram-sha-256 --pwfile=$passwordFile --encoding UTF8 --no-locale
 if($LASTEXITCODE -ne 0){throw 'Falha no initdb 013C.'}
}
& "$pgBin\pg_ctl.exe" -D $pgData status *> $null
if($LASTEXITCODE -ne 0){
 if(Get-NetTCPConnection -LocalPort 55436 -State Listen -ErrorAction SilentlyContinue){throw 'Porta 55436 ocupada; outra instância não será usada.'}
 $starter=Start-Process -FilePath (Join-Path $pgBin 'pg_ctl.exe') -ArgumentList @('-D',$pgData,'-l',(Join-Path $pgArea 'postgres.log'),'-o','"-p 55436 -h 127.0.0.1"','-w','start') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $pgArea 'start.log') -RedirectStandardError (Join-Path $pgArea 'start-error.log') -PassThru
 if(-not $starter.WaitForExit(30000) -or $starter.ExitCode -ne 0){throw 'PostgreSQL 013C não iniciou; conferir artifacts/013c/start-error.log.'}
}
$env:PGPASSWORD=[IO.File]::ReadAllText($passwordFile).Trim()
try{
 foreach($database in 'elifoot_013c_013b_catalog','elifoot_013c_013b_worlds','elifoot_013c_013b_tests','elifoot_013c_013b_catalog_real','elifoot_013c_013b_worlds_real'){
  $exists=& "$pgBin\psql.exe" -h 127.0.0.1 -p 55436 -U elifoot013c -d postgres -tAc "SELECT datname FROM pg_database WHERE datname='$database'"
  if($LASTEXITCODE -ne 0){throw 'Falha ao verificar bancos 013C.'}
  if(-not $exists){& "$pgBin\createdb.exe" -h 127.0.0.1 -p 55436 -U elifoot013c $database;if($LASTEXITCODE -ne 0){throw 'Falha ao criar banco 013C.'}}
 }
 $base="Host=127.0.0.1;Port=55436;Username=elifoot013c;Password=$env:PGPASSWORD"
 $env:ELIFOOT_013A_TEST_PG="$base;Database=elifoot_013c_013b_tests"
 $env:ELIFOOT_013A_WORLD_PG="$base;Database=elifoot_013c_013b_worlds"
 $env:ELIFOOT_013A_LEGACY_PG="$base;Database=postgres"
 $env:ELIFOOT_013C_CATALOG_PG="$base;Database=elifoot_013c_013b_catalog"
 $env:ELIFOOT_013C_WORLD_PG=$env:ELIFOOT_013A_WORLD_PG
 $env:ELIFOOT_013C_REAL_CATALOG_PG="$base;Database=elifoot_013c_013b_catalog_real"
 $env:ELIFOOT_013C_REAL_WORLD_PG="$base;Database=elifoot_013c_013b_worlds_real"
 $env:ELIFOOT_014A_CATALOG_PG=$env:ELIFOOT_013C_CATALOG_PG
 $env:ELIFOOT_014A_WORLD_PG=$env:ELIFOOT_013C_WORLD_PG
}finally{Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue}
Write-Output 'PostgreSQL exclusivo 013C: loopback 55436, credenciais próprias em artifacts/013c (fora do Git).'