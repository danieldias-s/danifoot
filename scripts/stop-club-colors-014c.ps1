param([switch]$IncludePostgres)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if($taskRoot -ne 'D:\elifoot_98_014C'){throw 'Runner exclusivo 014C.'}
$webRecord=Join-Path $taskRoot "artifacts/014c/web-5156-process.json"
if(Test-Path -LiteralPath $webRecord){
 $ownedWeb=Get-Content -LiteralPath $webRecord -Raw | ConvertFrom-Json
 $webProcess=Get-Process -Id $ownedWeb.Pid -ErrorAction SilentlyContinue
 if($null -ne $webProcess){
  if($webProcess.StartTime.Ticks -ne $ownedWeb.StartTicks){throw "PID da Web reutilizado; processo preservado."}
  Stop-Process -Id $webProcess.Id;Write-Output 'Web própria 014C 5156 encerrada.'
 }
}
foreach($port in 5154,5155){
 $record=Join-Path $taskRoot "artifacts\014c\api-$port-process.json"
 if(-not(Test-Path -LiteralPath $record)){continue}
 $owned=Get-Content -LiteralPath $record -Raw | ConvertFrom-Json
 $process=Get-Process -Id $owned.Pid -ErrorAction SilentlyContinue
 if($null -eq $process){continue}
 if($process.StartTime.Ticks -ne $owned.StartTicks){throw "PID reutilizado; processo $($owned.Pid) preservado."}
 Stop-Process -Id $process.Id
 Write-Output "API própria 014C $port encerrada."
}
if($IncludePostgres){& (Join-Path $PSScriptRoot 'test-club-colors-014c.ps1') -Action Stop}
