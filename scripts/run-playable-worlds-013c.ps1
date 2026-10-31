param([switch]$RealCatalog,[switch]$Fixtures,[switch]$ManualClock,[ValidateSet(5142,5143)][int]$ApiPort=5142,[ValidateSet("Release","IntegrationFinal","Playable013C")][string]$BuildDirectory="Playable013C")
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if($taskRoot -ne 'D:\elifoot_98_integration'){throw 'Runner exclusivo 013C.'}
if($RealCatalog -and $Fixtures){throw 'Fixtures não são carregadas no catálogo real.'}
if(Get-NetTCPConnection -LocalPort $ApiPort -State Listen -ErrorAction SilentlyContinue){throw "Porta $ApiPort ocupada; serviço existente preservado."}
. (Join-Path $PSScriptRoot 'test-playable-worlds-013c.ps1') -Action Start
$env:ASPNETCORE_ENVIRONMENT='LocalDev'
$env:ASPNETCORE_URLS="http://127.0.0.1:$ApiPort"
$env:Elifoot__DatabaseProvider='PostgreSQL'
$env:ConnectionStrings__DefaultConnection=if($RealCatalog){$env:ELIFOOT_013C_REAL_WORLD_PG}else{$env:ELIFOOT_013C_WORLD_PG}
$env:ConnectionStrings__Catalog013A=if($RealCatalog){$env:ELIFOOT_013C_REAL_CATALOG_PG}else{$env:ELIFOOT_013C_CATALOG_PG}
$env:Catalog013A__Enabled='true'
$env:Catalog013B__Enabled='true'
$env:Catalog013A__LoadFixtures=$Fixtures.IsPresent.ToString()
$env:Elifoot__AutoSeedDefaultWorld='false'
$env:Elifoot__DisableBackgroundClock=$ManualClock.IsPresent.ToString()
$env:Elifoot__AuthMode='LocalDev'
$apiDll=Join-Path $taskRoot "src\Elifoot.Api\bin\$BuildDirectory\net10.0\Elifoot.Api.dll"
$area=Join-Path $taskRoot 'artifacts\013c'
$process=Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @($apiDll) -WorkingDirectory (Join-Path $taskRoot 'src\Elifoot.Api') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $area "api-$ApiPort.log") -RedirectStandardError (Join-Path $area "api-$ApiPort-error.log") -PassThru
@{Pid=$process.Id;StartTicks=$process.StartTime.Ticks;Port=$ApiPort;Dll=$apiDll;RealCatalog=$RealCatalog.IsPresent} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $area "api-$ApiPort-process.json")
$ready=$false
for($attempt=0;$attempt -lt 60;$attempt++){
 if($process.HasExited){throw "API 013C $ApiPort encerrou; consultar artifacts/013c/api-$ApiPort-error.log."}
 try{$null=Invoke-RestMethod -Uri "http://127.0.0.1:$ApiPort/api/health" -TimeoutSec 2;$ready=$true;break}catch{Start-Sleep -Milliseconds 250}
}
if(-not $ready){throw "API 013C $ApiPort não respondeu ao health check."}
Write-Output "API 013C iniciada em loopback $ApiPort, PID $($process.Id)."
