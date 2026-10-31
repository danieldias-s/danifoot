param([switch]$IncludePostgres)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if($taskRoot -ne 'D:\elifoot_98_integration'){throw 'Runner exclusivo 014A.'}
foreach($port in 5140,5141){
 $record=Join-Path $taskRoot "artifacts\014a\api-$port-process.json"
 if(-not(Test-Path -LiteralPath $record)){continue}
 $owned=Get-Content -LiteralPath $record -Raw | ConvertFrom-Json
 $process=Get-Process -Id $owned.Pid -ErrorAction SilentlyContinue
 if($null -eq $process){continue}
 if($process.StartTime.Ticks -ne $owned.StartTicks){throw "PID reutilizado; processo $($owned.Pid) preservado."}
 Stop-Process -Id $process.Id
 Write-Output "API própria 014A $port encerrada."
}
if($IncludePostgres){& (Join-Path $PSScriptRoot 'test-integration-014a.ps1') -Action Stop}
