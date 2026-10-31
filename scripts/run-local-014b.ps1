param([switch]$NoBuild,[switch]$OpenChrome,[switch]$ManualClock)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if($taskRoot -ne 'D:\elifoot_98_014B'){throw 'Comando exclusivo do worktree 014B.'}
Set-Location -LiteralPath $taskRoot
foreach($name in 'dotnet','flutter','node'){if(-not(Get-Command $name -ErrorAction SilentlyContinue)){throw "Ferramenta ausente: $name"}}
if(-not(Test-Path -LiteralPath 'C:\Program Files\PostgreSQL\18\bin\pg_ctl.exe')){throw 'PostgreSQL 18 não encontrado no caminho documentado.'}
. (Join-Path $PSScriptRoot 'test-final-worlds-014b.ps1') -Action Start
$area=Join-Path $taskRoot 'artifacts/014b'
function Test-OwnedEndpoint([int]$port,[string]$record,[string]$url){
 if(-not(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)){return $false}
 if(-not(Test-Path -LiteralPath $record)){throw "Porta $port ocupada por serviço sem registro 014B; preservado."}
 $owned=Get-Content -LiteralPath $record -Raw | ConvertFrom-Json
 $process=Get-Process -Id $owned.Pid -ErrorAction SilentlyContinue
 if($null -eq $process -or $process.StartTime.Ticks -ne $owned.StartTicks){throw "Porta $port não corresponde ao processo próprio registrado."}
 $null=Invoke-RestMethod -Uri $url -TimeoutSec 5
 return $true
}
$apiRunning=Test-OwnedEndpoint 5145 (Join-Path $area 'api-5145-process.json') 'http://127.0.0.1:5145/api/health'
if($apiRunning){
 $apiState=Get-Content (Join-Path $area 'api-5145-process.json') -Raw | ConvertFrom-Json
 if($apiState.RealCatalog -ne $true -or $apiState.ManualClock -ne $ManualClock.IsPresent){throw 'API própria ativa com configuração diferente. Encerre com stop-final-worlds-014b.ps1 e inicie no modo desejado.'}
}
$webRunning=Test-OwnedEndpoint 5146 (Join-Path $area 'web-5146-process.json') 'http://127.0.0.1:5146/__014b_health'
if(-not $apiRunning){
 if(-not $NoBuild){
  dotnet build src/Elifoot.Api -c Release '-p:OutputPath=bin/Final014B/net10.0/' '-p:AppendTargetFrameworkToOutputPath=false' *> (Join-Path $area 'dev-api-build.log')
  if($LASTEXITCODE -ne 0){throw 'Build da API falhou; conferir artifacts/014b/dev-api-build.log.'}
 }
 if(-not(Test-Path -LiteralPath 'src/Elifoot.Api/bin/Final014B/net10.0/Elifoot.Api.dll')){throw 'API não compilada; execute sem -NoBuild.'}
 & (Join-Path $PSScriptRoot 'run-final-api-014b.ps1') -RealCatalog -ApiPort 5145 -ManualClock:$ManualClock
}
if(-not $webRunning){
 if(-not $NoBuild){
  Push-Location client/elifoot_flutter
  try{flutter build web '--dart-define=API_BASE_URL=http://127.0.0.1:5145' *> (Join-Path $area 'dev-web-build.log');if($LASTEXITCODE -ne 0){throw 'Build Web falhou; conferir artifacts/014b/dev-web-build.log.'}}finally{Pop-Location}
 }
 if(-not(Test-Path -LiteralPath 'client/elifoot_flutter/build/web/index.html')){throw 'Web não compilada; execute sem -NoBuild.'}
 $nodeProcess=Start-Process -FilePath (Get-Command node).Source -ArgumentList @((Join-Path $PSScriptRoot 'serve-final-web-014b.mjs')) -WorkingDirectory $taskRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $area 'web-5146.log') -RedirectStandardError (Join-Path $area 'web-5146-error.log') -PassThru
 @{Pid=$nodeProcess.Id;StartTicks=$nodeProcess.StartTime.Ticks;Port=5146;Root=$taskRoot} | ConvertTo-Json | Set-Content (Join-Path $area 'web-5146-process.json')
 $ready=$false
 for($attempt=0;$attempt -lt 60;$attempt++){
  if($nodeProcess.HasExited){throw 'Web 014B encerrou; conferir artifacts/014b/web-5146-error.log.'}
  try{$health=Invoke-RestMethod -Uri 'http://127.0.0.1:5146/__014b_health' -TimeoutSec 2;if($health.app -eq 'ELIFOOT_014B'){$ready=$true;break}}catch{Start-Sleep -Milliseconds 250}
 }
 if(-not $ready){throw 'Web 014B não respondeu ao health check.'}
}
Write-Output '014B pronta: http://127.0.0.1:5146 | API http://127.0.0.1:5145 | PostgreSQL próprio 55437.'
Write-Output 'Serviços existentes da 014B são reutilizados; para encerrar: .\scripts\stop-final-worlds-014b.ps1 -IncludePostgres'
if($OpenChrome){
 $chrome=@("$env:ProgramFiles\Google\Chrome\Application\chrome.exe","${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe","$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe") | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
 if(-not $chrome){throw 'Chrome não localizado. Abra manualmente http://127.0.0.1:5146.'}
 Start-Process -FilePath $chrome -ArgumentList @('--new-window','http://127.0.0.1:5146')
}
