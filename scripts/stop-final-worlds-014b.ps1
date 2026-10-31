param([switch]$IncludePostgres)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if($taskRoot -ne 'D:\elifoot_98_014B'){throw 'Runner exclusivo 014B.'}
$webRecord=Join-Path $taskRoot "artifacts/014b/web-5146-process.json"
if(Test-Path -LiteralPath $webRecord){
 $ownedWeb=Get-Content -LiteralPath $webRecord -Raw | ConvertFrom-Json
 $webProcess=Get-Process -Id $ownedWeb.Pid -ErrorAction SilentlyContinue
 if($null -ne $webProcess){
  if($webProcess.StartTime.Ticks -ne $ownedWeb.StartTicks){throw "PID da Web reutilizado; processo preservado."}
  Stop-Process -Id $webProcess.Id;Write-Output 'Web própria 014B 5146 encerrada.'
 }
}
foreach($port in 5144,5145){
 $record=Join-Path $taskRoot "artifacts\014b\api-$port-process.json"
 if(-not(Test-Path -LiteralPath $record)){continue}
 $owned=Get-Content -LiteralPath $record -Raw | ConvertFrom-Json
 $process=Get-Process -Id $owned.Pid -ErrorAction SilentlyContinue
 if($null -eq $process){continue}
 if($process.StartTime.Ticks -ne $owned.StartTicks){throw "PID reutilizado; processo $($owned.Pid) preservado."}
 Stop-Process -Id $process.Id
 Write-Output "API própria 014B $port encerrada."
}
if($IncludePostgres){& (Join-Path $PSScriptRoot 'test-final-worlds-014b.ps1') -Action Stop}
