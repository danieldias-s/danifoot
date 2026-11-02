param([switch]$NoBuild,[switch]$ApiOnly)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if($taskRoot -ne 'D:\elifoot_98_016B'){throw 'Runner exclusivo do worktree 016B.'}
Set-Location -LiteralPath $taskRoot
. (Join-Path $PSScriptRoot 'test-visual-016b.ps1')
$taskArea=Join-Path $taskRoot 'artifacts/016b'
function Test-Owned016B([int]$port,[string]$url){
 if(-not(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)){return $false}
 $record=Join-Path $taskArea "service-$port.json"
 if(-not(Test-Path -LiteralPath $record)){throw "Porta $port ocupada por serviço sem registro 016B; preservado."}
 $owned=Get-Content -LiteralPath $record -Raw | ConvertFrom-Json
 $process=Get-Process -Id $owned.Pid -ErrorAction SilentlyContinue
 $listener=Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction Stop
 if($owned.Root -ne $taskRoot -or $owned.Port -ne $port -or $listener.OwningProcess -ne $owned.Pid -or $null -eq $process -or $process.StartTime.Ticks -ne $owned.StartTicks){throw "Porta $port não corresponde ao processo 016B registrado; preservado."}
 $null=Invoke-RestMethod -Uri $url -TimeoutSec 5
 return $true
}
function Record-Owned016B($process,[int]$port){
 @{Pid=$process.Id;StartTicks=$process.StartTime.Ticks;Port=$port;Root=$taskRoot} | ConvertTo-Json | Set-Content (Join-Path $taskArea "service-$port.json")
}
if(-not(Test-Owned016B 5173 'http://127.0.0.1:5173/api/health')){
 if(-not $NoBuild){
  dotnet build src/Elifoot.Api -c Release *> (Join-Path $taskArea 'local-api-build.log')
  if($LASTEXITCODE -ne 0){throw 'Falha na build API; consultar local-api-build.log.'}
 }
 $apiSource=Join-Path $taskRoot 'src/Elifoot.Api/bin/Release/net10.0'
 $apiRuntime=Join-Path $taskArea 'api'
 New-Item -ItemType Directory -Force $apiRuntime | Out-Null
 Copy-Item -Path (Join-Path $apiSource '*') -Destination $apiRuntime -Recurse -Force
 $env:ASPNETCORE_URLS='http://127.0.0.1:5173'
 $env:ASPNETCORE_ENVIRONMENT='Development'
 $env:Elifoot__DatabaseProvider='PostgreSQL'
 $env:Elifoot__AuthMode='LocalDev'
 $env:Elifoot__AutoSeedDefaultWorld='false'
 $env:Elifoot__DisableBackgroundClock='false'
 $env:Catalog013A__Enabled='true'
 $env:Catalog013A__DatabaseProvider='PostgreSQL'
 $env:Catalog013A__LoadFixtures='false'
 $env:Catalog013B__Enabled='true'
 $env:Catalog016A__Enabled='true'
 $taskAdminFile=Join-Path $taskArea 'admin-token.txt'
 if(-not(Test-Path -LiteralPath $taskAdminFile)){[IO.File]::WriteAllText($taskAdminFile,[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)))}
 $env:Catalog016A__AdminToken=[IO.File]::ReadAllText($taskAdminFile).Trim()
 $env:Catalog013A__ImportToken=$env:Catalog016A__AdminToken
 $env:Catalog014C__OnlineColorResearch='false'
 $env:Catalog014C__ColorFile=Join-Path $taskRoot 'tools/catalog-013b/data/club-colors-014c.json'
 $env:ConnectionStrings__DefaultConnection=$env:ELIFOOT_016B_WORLD_PG
 $env:ConnectionStrings__Catalog013A=$env:ELIFOOT_016B_CATALOG_PG
 $process=Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @((Join-Path $apiRuntime 'Elifoot.Api.dll')) -WorkingDirectory (Join-Path $taskRoot 'src/Elifoot.Api') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $taskArea 'api-5173.log') -RedirectStandardError (Join-Path $taskArea 'api-5173-error.log') -PassThru
 Record-Owned016B $process 5173
 $ready=$false
 for($i=0;$i -lt 60;$i++){
  if($process.HasExited){throw 'API encerrou; consultar api-5173-error.log.'}
  try{$null=Invoke-RestMethod 'http://127.0.0.1:5173/api/health' -TimeoutSec 1;$ready=$true;break}catch{Start-Sleep -Milliseconds 250}
 }
 if(-not $ready){throw 'API não respondeu ao health check.'}
}
if(-not $ApiOnly -and -not(Test-Owned016B 5174 'http://127.0.0.1:5174/__016b_health')){
 if(-not $NoBuild){
  Push-Location (Join-Path $taskRoot 'client/elifoot_flutter')
  try{
   flutter build web '--dart-define=API_BASE_URL=http://127.0.0.1:5173' --pwa-strategy=none "--output=$taskArea/web" *> (Join-Path $taskArea 'local-web-build.log')
   if($LASTEXITCODE -ne 0){throw 'Build Web falhou; consultar local-web-build.log.'}
  }finally{Pop-Location}
 }
 if(-not(Test-Path -LiteralPath (Join-Path $taskArea 'web/main.dart.js'))){throw 'Build Web 016B ausente.'}
 $process=Start-Process -FilePath (Get-Command node).Source -ArgumentList @((Join-Path $PSScriptRoot 'serve-web-016b.mjs')) -WorkingDirectory $taskRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $taskArea 'web-5174.log') -RedirectStandardError (Join-Path $taskArea 'web-5174-error.log') -PassThru
 Record-Owned016B $process 5174
}
Write-Output '016B: http://127.0.0.1:5174 | API http://127.0.0.1:5173 | PostgreSQL exclusivo 55442.'
