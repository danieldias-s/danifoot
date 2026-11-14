# Reproduzir a auditoria

Execute em PowerShell 7 no Windows, na raiz do repositório. Requer .NET SDK 10, Node 24 e, para a comparação PostgreSQL, binários PostgreSQL 18. Use diretórios novos a cada execução. Para reproduzir em outra máquina, ajuste o contador e a instância de disco em `sample-disk.ps1` conforme o idioma e a unidade. Troque as portas de API por portas livres quando necessário.

```powershell
$audit = Join-Path (Get-Location) 'artifacts/scalability-repeat'
New-Item -ItemType Directory $audit
dotnet build src/Elifoot.Api/Elifoot.Api.csproj -c Release --artifacts-path "$audit/api"
dotnet build docs/audit-scalability/EngineAudit.csproj -c Release --artifacts-path "$audit/engine"
$api = Join-Path $audit 'api/bin/Elifoot.Api/release/Elifoot.Api.dll'
$engine = Join-Path $audit 'engine/bin/EngineAudit/release/EngineAudit.dll'

$env:AUDIT_SAMPLE_DISK = '1'
node docs/audit-scalability/load.mjs $api "$audit/sqlite" 10 50 100 500 1000
Remove-Item Env:AUDIT_SAMPLE_DISK

dotnet $engine motor "$audit/motor-64" 64 10 50 100 500 1000
dotnet $engine motor "$audit/motor-128" 128 10
dotnet $engine motor "$audit/motor-1024" 1024 10
dotnet $engine seasons "$audit/seasons" 128 10
node docs/audit-scalability/compare-sqlite.mjs "$audit/file-comparison" 10 50

pwsh -File docs/audit-scalability/account-tests.ps1 -OutputDirectory "$audit/accounts"
```

Para repetir a inspeção dos arquivos de QA, o comando abaixo lê os originais como arquivos, verifica que o conjunto DB/WAL permaneceu estável e abre somente a cópia descartável. Se a origem estiver ativa, prefira uma imagem consistente fornecida pelo operador ou repita em janela sem gravações. A medição usa `quick_check`, `dbstat`, comprimentos de JSON e hashes. A inspeção da cópia é a única etapa que usa dados existentes.

```powershell
node docs/audit-scalability/storage.mjs "$audit/existing" playable-qa.db artifacts/esp-play/worlds-test.sqlite artifacts/esp-play/catalog-test.sqlite
```

Para a comparação PostgreSQL, inicialize um cluster novo e use apenas loopback. Em um terminal, execute o servidor em primeiro plano. O parâmetro de autenticação trust se destina ao cluster descartável local. Em outro terminal, crie um banco vazio por nível e execute o gerador.

```powershell
$pg = 'C:/Program Files/PostgreSQL/18/bin'
& "$pg/initdb.exe" -D "$audit/pg" -U audit -A trust --no-locale --encoding=UTF8
& "$pg/postgres.exe" -D "$audit/pg" -h 127.0.0.1 -p 54983
```

```powershell
& "$pg/psql.exe" -h 127.0.0.1 -p 54983 -U audit -d postgres -v ON_ERROR_STOP=1 -c 'CREATE DATABASE audit_load50'
$env:AUDIT_PG = 'Host=127.0.0.1;Port=54983;Database=audit_load50;Username=audit;Command Timeout=15'
node docs/audit-scalability/load.mjs $api "$audit/pg-load" 50
Remove-Item Env:AUDIT_PG
```

Para relógio real e WebSockets, selecione `AUDIT_PROVIDER=InMemory`, `AUDIT_ROUND_ONLY=1` e `AUDIT_CLOCK=1` em um shell separado. O gerador conecta um cliente SignalR por sala, verifica escopo de eventos e mede quantidade de ticks, bytes e maior intervalo. A simulação de temporadas separa custo do motor e persistência. O driver salva com o repositório EF ao entrar em PREPARING após cada rodada, permitindo medir uma carreira longa com menos amplificação de checkpoints.

Os resultados registram números brutos, método de parada e parâmetros. A API local usa timeouts de 10 s para SQLite e 30 s para requisições. O gerador interrompe ao ultrapassar limites de espaço, WAL, memória do host ou do processo, ou erros acumulados. A escalada depende do limite de p95 do nível anterior. O teste do motor usa concorrência máxima de 20 e interrompe a escalada se ultrapassar 25 s de p95 por rodada ou 180 s por lote.

O NuGet pôde ser restaurado do cache local da máquina com `--source C:/Users/Daniel/.nuget/packages -p:NuGetAudit=false` no build. O runner de conta aceita `-NuGetSource`. O runner copia os testes existentes para o diretório de saída e injeta um key ring isolado no fixture. O código de aplicação é o mesmo do build local.
