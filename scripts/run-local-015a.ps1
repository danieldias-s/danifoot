param([switch]$NoBuild,[switch]$ApiOnly)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if($taskRoot -ne 'D:\elifoot_98_015A'){throw 'Runner exclusivo do worktree 015A.'}
Set-Location -LiteralPath $taskRoot
. (Join-Path $PSScriptRoot 'test-match-market-015a.ps1')
$taskArea=Join-Path $taskRoot 'artifacts/015a'
function Test-Owned015A([int]$port,[string]$url){
 if(-not(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)){return $false}
 $record=Join-Path $taskArea "service-$port.json"
 if(-not(Test-Path -LiteralPath $record)){throw "Porta $port ocupada por serviço sem registro 015A; preservado."}
 $owned=Get-Content -LiteralPath $record -Raw | ConvertFrom-Json
 $process=Get-Process -Id $owned.Pid -ErrorAction SilentlyContinue
 if($null -eq $process -or $process.StartTime.Ticks -ne $owned.StartTicks){throw "Porta $port não corresponde ao processo 015A registrado; preservado."}
 $null=Invoke-RestMethod -Uri $url -TimeoutSec 5
 return $true
}
function Record-Owned015A($process,[int]$port){
 @{Pid=$process.Id;StartTicks=$process.StartTime.Ticks;Port=$port;Root=$taskRoot} | ConvertTo-Json | Set-Content (Join-Path $taskArea "service-$port.json")
}
if(-not(Test-Owned015A 5165 'http://127.0.0.1:5165/api/health')){
 if(-not $NoBuild){
  dotnet build src/Elifoot.Api -c Release *> (Join-Path $taskArea 'local-api-build.log')
  if($LASTEXITCODE -ne 0){throw 'Falha na build API; consultar local-api-build.log.'}
 }
 $apiSource=Join-Path $taskRoot 'src/Elifoot.Api/bin/Release/net10.0'
 $apiRuntime=Join-Path $taskArea 'api'
 New-Item -ItemType Directory -Force $apiRuntime | Out-Null
 Copy-Item -Path (Join-Path $apiSource '*') -Destination $apiRuntime -Recurse -Force
 $env:ASPNETCORE_URLS='http://127.0.0.1:5165'
 $env:ASPNETCORE_ENVIRONMENT='Development'
 $env:Elifoot__DatabaseProvider='PostgreSQL'
 $env:Elifoot__AuthMode='LocalDev'
 $env:Elifoot__AutoSeedDefaultWorld='false'
 $env:Elifoot__DisableBackgroundClock='false'
 $env:Catalog013A__Enabled='true'
 $env:Catalog013A__DatabaseProvider='PostgreSQL'
 $env:Catalog013A__LoadFixtures='false'
 $env:Catalog013B__Enabled='true'
 $env:Catalog014C__OnlineColorResearch='false'
 $env:Catalog014C__ColorFile=Join-Path $taskRoot 'tools/catalog-013b/data/club-colors-014c.json'
 $env:ConnectionStrings__DefaultConnection=$env:ELIFOOT_013C_REAL_WORLD_PG
 $env:ConnectionStrings__Catalog013A=$env:ELIFOOT_013C_REAL_CATALOG_PG
 $process=Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @((Join-Path $apiRuntime 'Elifoot.Api.dll')) -WorkingDirectory (Join-Path $taskRoot 'src/Elifoot.Api') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $taskArea 'api-5165.log') -RedirectStandardError (Join-Path $taskArea 'api-5165-error.log') -PassThru
 Record-Owned015A $process 5165
 $ready=$false
 for($i=0;$i -lt 60;$i++){
  if($process.HasExited){throw 'API encerrou; consultar api-5165-error.log.'}
  try{$null=Invoke-RestMethod 'http://127.0.0.1:5165/api/health' -TimeoutSec 1;$ready=$true;break}catch{Start-Sleep -Milliseconds 250}
 }
 if(-not $ready){throw 'API não respondeu ao health check.'}
}
if(-not $ApiOnly -and -not(Test-Owned015A 5166 'http://127.0.0.1:5166/__015a_health')){
 if(-not $NoBuild){
  Push-Location (Join-Path $taskRoot 'client/elifoot_flutter')
  try{
   flutter build web '--dart-define=API_BASE_URL=http://127.0.0.1:5165' --pwa-strategy=none "--output=$taskArea/web" *> (Join-Path $taskArea 'local-web-build.log')
   if($LASTEXITCODE -ne 0){throw 'Build Web falhou; consultar local-web-build.log.'}
  }finally{Pop-Location}
 }
 if(-not(Test-Path -LiteralPath (Join-Path $taskArea 'web/main.dart.js'))){throw 'Build Web 015A ausente.'}
 $process=Start-Process -FilePath (Get-Command node).Source -ArgumentList @((Join-Path $PSScriptRoot 'serve-web-015a.mjs')) -WorkingDirectory $taskRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $taskArea 'web-5166.log') -RedirectStandardError (Join-Path $taskArea 'web-5166-error.log') -PassThru
 Record-Owned015A $process 5166
}
Write-Output '015A: http://127.0.0.1:5166 | API http://127.0.0.1:5165 | PostgreSQL exclusivo 55439.'
