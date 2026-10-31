$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if($taskRoot -ne 'D:\elifoot_98_integration'){throw 'Runner exclusivo 014A.'}
. (Join-Path $PSScriptRoot 'test-integration-014a.ps1') -Action Start
$old=Get-Content -LiteralPath (Join-Path $taskRoot 'docs\audit-014a\old-worlds.json') -Raw | ConvertFrom-Json
$databases=@('elifoot_014a_013b_worlds','elifoot_014a_013b_worlds_real')+@($old | ForEach-Object {$_.Database})
function SnapshotEvidence([string]$phase){
 $query='SELECT coalesce(json_agg(row_to_json(x)), ''[]''::json) FROM (SELECT "Id" AS id, md5("SnapshotJson") AS snapshot_md5 FROM ef_worlds ORDER BY "Id") x;'
 $result=@{}
 $env:PGPASSWORD=[IO.File]::ReadAllText((Join-Path $taskRoot 'artifacts\014a\pg-password.txt')).Trim()
 try{
  foreach($database in $databases){
   if(-not $database.StartsWith('elifoot_014a_',[StringComparison]::Ordinal)){throw 'Banco externo rejeitado.'}
   $rows=& 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -w -h 127.0.0.1 -p 55435 -U elifoot014a -d $database -Atc $query
   if($LASTEXITCODE -ne 0){throw 'Falha ao ler snapshots 014A.'}
   $result[$database]=($rows -join '').Trim()
  }
 }finally{Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue}
 $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskRoot "docs\audit-014a\snapshots-$phase.json")
 return $result
}
$before=SnapshotEvidence 'before-restart'
$priorPid=(Get-Content -LiteralPath (Join-Path $taskRoot 'artifacts\014a\pgdata\postmaster.pid') -TotalCount 1).Trim()
& (Join-Path $PSScriptRoot 'stop-integration-014a.ps1') -IncludePostgres
. (Join-Path $PSScriptRoot 'test-integration-014a.ps1') -Action Start
& (Join-Path $PSScriptRoot 'run-integration-014a.ps1') -ApiPort 5140 -BuildDirectory IntegrationFinal
& (Join-Path $PSScriptRoot 'run-integration-014a.ps1') -RealCatalog -ApiPort 5141 -BuildDirectory IntegrationFinal
$nextPid=(Get-Content -LiteralPath (Join-Path $taskRoot 'artifacts\014a\pgdata\postmaster.pid') -TotalCount 1).Trim()
if($priorPid -eq $nextPid){throw 'PostgreSQL não foi reiniciado.'}
$after=SnapshotEvidence 'after-restart'
foreach($database in $databases){if($before[$database] -ne $after[$database]){throw "Snapshot alterado após reinício: $database"}}
python (Join-Path $PSScriptRoot 'smoke-integration-014a.py') --verify-restart
if($LASTEXITCODE -ne 0){throw 'Falha no replay HTTP após reinício.'}
dotnet run --no-build --project (Join-Path $taskRoot 'tools\catalog-013b\importer') -c Release '-p:OutputPath=bin/IntegrationFinal/net10.0/' '-p:AppendTargetFrameworkToOutputPath=false' -- 014a-verify-old-worlds (Join-Path $taskRoot 'docs\audit-014a')
if($LASTEXITCODE -ne 0){throw 'Falha nos saves históricos após reinício.'}
@{Status='PASS';Port=55435;PreviousPostmasterPid=$priorPid;NewPostmasterPid=$nextPid;Databases=$databases;SnapshotsUnchanged=$true;ApiRestart=$true;OldWorldsPreserved=$true;LoanReplayIdempotent=$true;ExternalDatabasesUsed=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskRoot 'docs\audit-014a\restart-verification.json')
Write-Output 'PASS: PostgreSQL e APIs 014A reiniciados; snapshots e finanças preservados.'
