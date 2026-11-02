param([switch]$NoBuild,[switch]$ApiOnly)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if($taskRoot -ne 'D:\elifoot_98_016B'){throw 'Runner exclusivo do worktree 016B.'}
Set-Location -LiteralPath $taskRoot
. (Join-Path $PSScriptRoot 'test-visual-016b.ps1')
$taskArea=Join-Path $taskRoot 'artifacts/016b/world-catalog'
New-Item -ItemType Directory -Force $taskArea | Out-Null
function Test-OwnedCatalog016B([int]$port,[string]$url){
 if(-not(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)){return $false}
 $record=Join-Path $taskArea "service-$port.json"
 if(-not(Test-Path -LiteralPath $record)){throw "Porta $port ocupada por outro serviço; preservada."}
 $owned=Get-Content -LiteralPath $record -Raw | ConvertFrom-Json
 $process=Get-Process -Id $owned.Pid -ErrorAction SilentlyContinue
 $listener=Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction Stop
 if($owned.Root -ne $taskRoot -or $owned.Port -ne $port -or $listener.OwningProcess -ne $owned.Pid -or $null -eq $process -or $process.StartTime.Ticks -ne $owned.StartTicks){throw "Porta $port não pertence ao catálogo 016B registrado; preservada."}
 $null=Invoke-RestMethod -Uri $url -TimeoutSec 5
 return $true
}
function Record-OwnedCatalog016B($process,[int]$port){
 @{Pid=$process.Id;StartTicks=$process.StartTime.Ticks;Port=$port;Root=$taskRoot;Task='world-catalog-016b'} | ConvertTo-Json | Set-Content (Join-Path $taskArea "service-$port.json")
}
# Copy the existing DEV catalogue once; the source database is only read.
$env:PGPASSWORD=[IO.File]::ReadAllText((Join-Path $taskRoot 'artifacts/016b/pg-password.txt')).Trim()
$taskPgBin='C:\Program Files\PostgreSQL\18\bin'
try{
 foreach($taskPart in 'catalog','worlds'){
  $taskDatabase='elifoot_016b_013b_wc_'+$taskPart
  $exists=& "$taskPgBin\psql.exe" -h 127.0.0.1 -p 55442 -U elifoot016b -d postgres -tAc "SELECT datname FROM pg_database WHERE datname='$taskDatabase'"
  if($LASTEXITCODE -ne 0){throw 'Falha ao consultar o banco DEV isolado.'}
  if(-not $exists){
   $dump=Join-Path $taskArea "$taskPart-initial.dump"
   & "$taskPgBin\pg_dump.exe" -h 127.0.0.1 -p 55442 -U elifoot016b -d ('elifoot_016b_013b_'+$taskPart+'_dev') -Fc -f $dump
   if($LASTEXITCODE -ne 0){throw 'Falha ao copiar dados DEV; nenhum banco foi alterado.'}
   & "$taskPgBin\createdb.exe" -h 127.0.0.1 -p 55442 -U elifoot016b $taskDatabase
   if($LASTEXITCODE -ne 0){throw 'Falha ao criar o banco exclusivo desta missão.'}
   & "$taskPgBin\pg_restore.exe" -h 127.0.0.1 -p 55442 -U elifoot016b -d $taskDatabase --no-owner --exit-on-error $dump
   if($LASTEXITCODE -ne 0){throw 'A cópia não foi concluída; o banco exclusivo foi preservado para inspeção.'}
  }
 }
 $taskConnection="Host=127.0.0.1;Port=55442;Username=elifoot016b;Password=$env:PGPASSWORD"
 $env:ConnectionStrings__DefaultConnection="$taskConnection;Database=elifoot_016b_013b_wc_worlds"
 $env:ConnectionStrings__Catalog013A="$taskConnection;Database=elifoot_016b_013b_wc_catalog"
}finally{Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue}
if(-not(Test-OwnedCatalog016B 5175 'http://127.0.0.1:5175/api/health')){
 if(-not $NoBuild){
  dotnet build src/Elifoot.Api -c Release --artifacts-path (Join-Path $taskArea 'api-build') *> (Join-Path $taskArea 'api-build.log')
  if($LASTEXITCODE -ne 0){throw 'Falha na build API do catálogo; verificar api-build.log.'}
 }
 $source=Join-Path $taskArea 'api-build/bin/Elifoot.Api/release'
 $runtime=Join-Path $taskArea 'api'
 if(-not(Test-Path -LiteralPath (Join-Path $source 'Elifoot.Api.dll'))){throw 'Build API do catálogo ausente.'}
 New-Item -ItemType Directory -Force $runtime | Out-Null
 Get-ChildItem -LiteralPath $source -Force | Copy-Item -Destination $runtime -Recurse -Force
 $env:ASPNETCORE_URLS='http://127.0.0.1:5175'
 $env:ASPNETCORE_ENVIRONMENT='Development'
 $env:Elifoot__DatabaseProvider='PostgreSQL';$env:Elifoot__AuthMode='LocalDev'
 $env:Elifoot__AutoSeedDefaultWorld='false';$env:Elifoot__DisableBackgroundClock='false'
 $env:Catalog013A__Enabled='true';$env:Catalog013A__DatabaseProvider='PostgreSQL';$env:Catalog013A__LoadFixtures='false'
 $env:Catalog013B__Enabled='true';$env:Catalog016A__Enabled='true';$env:Catalog016B__Enabled='true'
 $env:Catalog014C__OnlineColorResearch='false';$env:Catalog014C__ColorFile=Join-Path $taskRoot 'tools/catalog-013b/data/club-colors-014c.json'
 $tokenFile=Join-Path $taskArea 'admin-token.txt'
 if(-not(Test-Path -LiteralPath $tokenFile)){[IO.File]::WriteAllText($tokenFile,[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)))}
 $env:Catalog016A__AdminToken=[IO.File]::ReadAllText($tokenFile).Trim();$env:Catalog013A__ImportToken=$env:Catalog016A__AdminToken
 $process=Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @((Join-Path $runtime 'Elifoot.Api.dll')) -WorkingDirectory (Join-Path $taskRoot 'src/Elifoot.Api') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $taskArea 'api-5175.log') -RedirectStandardError (Join-Path $taskArea 'api-5175-error.log') -PassThru
 Record-OwnedCatalog016B $process 5175
 $ready=$false
 for($i=0;$i -lt 60;$i++){
  if($process.HasExited){throw 'API encerrou; consultar os logs 5175.'}
  try{$null=Invoke-RestMethod 'http://127.0.0.1:5175/api/health' -TimeoutSec 1;$ready=$true;break}catch{Start-Sleep -Milliseconds 250}
 }
 if(-not $ready){throw 'API não confirmou o health check.'}
}
if(-not $ApiOnly -and -not(Test-OwnedCatalog016B 5176 'http://127.0.0.1:5176/__world_catalog_016b_health')){
 if(-not $NoBuild){
  Push-Location (Join-Path $taskRoot 'client/elifoot_flutter')
  try{
   flutter build web '--dart-define=API_BASE_URL=http://127.0.0.1:5175' --pwa-strategy=none --no-wasm-dry-run "--output=$taskArea/web" *> (Join-Path $taskArea 'web-build.log')
   if($LASTEXITCODE -ne 0){throw 'Build Web do catálogo falhou.'}
  }finally{Pop-Location}
 }
 if(-not(Test-Path -LiteralPath (Join-Path $taskArea 'web/main.dart.js'))){throw 'Build Web do catálogo ausente.'}
 $process=Start-Process -FilePath (Get-Command node).Source -ArgumentList @((Join-Path $PSScriptRoot 'serve-world-catalog-016b.mjs')) -WorkingDirectory $taskRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $taskArea 'web-5176.log') -RedirectStandardError (Join-Path $taskArea 'web-5176-error.log') -PassThru
 Record-OwnedCatalog016B $process 5176
 $webReady=$false
 for($i=0;$i -lt 60;$i++){
  if($process.HasExited){throw 'Web encerrou; consultar os logs 5176.'}
  try{$null=Invoke-RestMethod 'http://127.0.0.1:5176/__world_catalog_016b_health' -TimeoutSec 1;$webReady=$true;break}catch{Start-Sleep -Milliseconds 250}
 }
 if(-not $webReady){throw 'Web não confirmou o health check.'}
}
if($ApiOnly){Write-Output 'API catálogo 016B: http://127.0.0.1:5175 | bancos wc_catalog/wc_worlds isolados.'}
else{Write-Output 'Catálogo 016B: http://127.0.0.1:5176 | API http://127.0.0.1:5175 | bancos wc_catalog/wc_worlds isolados.'}
