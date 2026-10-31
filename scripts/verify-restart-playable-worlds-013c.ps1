$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if($taskRoot -ne 'D:\elifoot_98_integration'){throw 'Verificador exclusivo 013C.'}
$running=Get-CimInstance Win32_Process | Where-Object {$_.Name -eq 'testhost.exe' -and $_.CommandLine -like '*elifoot_98_integration*Elifoot.Integration.Tests*Playable013C*'}
if($running){throw 'Aguardar os testes de integração 013C antes de reiniciar seu PostgreSQL.'}
. (Join-Path $PSScriptRoot 'test-playable-worlds-013c.ps1') -Action Start
function Capture([string]$phase){
 $env:PGPASSWORD=[IO.File]::ReadAllText((Join-Path $taskRoot 'artifacts/013c/pg-password.txt')).Trim()
 try {
  $result=@{}
  $queries=@{
   'elifoot_013c_013b_worlds_real'='SELECT coalesce(json_agg(row_to_json(x)), ''[]''::json) FROM (SELECT "Id" AS id, md5("SnapshotJson") AS hash FROM ef_worlds ORDER BY "Id") x;'
   'elifoot_013c_013b_catalog_real'='SELECT coalesce(json_agg(row_to_json(x)), ''[]''::json) FROM (SELECT "Id" AS id, "Hash" AS stored_hash, md5("PayloadJson") AS payload_hash FROM "wc013a_CatalogSnapshot" ORDER BY "Id") x;'
  }
  foreach($database in $queries.Keys){
   if(-not $database.StartsWith('elifoot_013c_')){throw 'Banco externo rejeitado.'}
   $rows=& 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -w -h 127.0.0.1 -p 55436 -U elifoot013c -d $database -Atc $queries[$database]
   if($LASTEXITCODE -ne 0){throw 'Falha na leitura dos snapshots próprios.'}
   $result[$database]=($rows -join '').Trim()
  }
  $result | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $taskRoot "docs/audit-013c/snapshots-$phase.json")
  return $result
 } finally {Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue}
}
$before=Capture 'before-restart'
$oldPid=(Get-Content (Join-Path $taskRoot 'artifacts/013c/pgdata/postmaster.pid') -TotalCount 1).Trim()
& (Join-Path $PSScriptRoot 'stop-playable-worlds-013c.ps1') -IncludePostgres
. (Join-Path $PSScriptRoot 'test-playable-worlds-013c.ps1') -Action Start
& (Join-Path $PSScriptRoot 'run-playable-worlds-013c.ps1') -RealCatalog -ManualClock -ApiPort 5143 -BuildDirectory Playable013C
$newPid=(Get-Content (Join-Path $taskRoot 'artifacts/013c/pgdata/postmaster.pid') -TotalCount 1).Trim()
if($oldPid -eq $newPid){throw 'PostgreSQL não reiniciou.'}
$after=Capture 'after-restart'
foreach($db in $before.Keys){if($before[$db] -ne $after[$db]){throw "Snapshot alterado: $db"}}
python (Join-Path $PSScriptRoot 'smoke-playable-worlds-013c.py') --verify-restart
if($LASTEXITCODE -ne 0){throw 'Falha na recuperação HTTP dos mundos reais.'}
@{Status='PASS';Port=55436;PreviousPostmasterPid=$oldPid;NewPostmasterPid=$newPid;WorldSnapshotsUnchanged=$true;CatalogSnapshotsUnchanged=$true;ApiRestart=$true;PostgresRestart=$true;ExternalDatabaseUsed=$false} | ConvertTo-Json | Set-Content (Join-Path $taskRoot 'docs/audit-013c/restart-verification.json')
