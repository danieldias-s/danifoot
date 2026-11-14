# Multiplayer · Etapa 4 · PostgreSQL

**Classificação: PARTIAL.** Migração e restauração comprovadas em bancos descartáveis. A API autoritativa recuperou estado com dois processos em sequência. Os dois provedores concluíram os lotes de 10, 50 e 100 mundos; o nível de 100 precisou de reteste após encerrar o perfil de compactação. A operação comercial e a cobertura ponta a ponta dos dois temas permanecem pendentes.

## Arquitetura e esquema

SQLite e PostgreSQL usam o mesmo `ElifootDbContext`. O provedor mapeia GUIDs, decimais, datas e booleanos para tipos nativos PostgreSQL. Cada save continua identificado por `ef_worlds.Id`. A coluna do snapshot é texto, elegível para TOAST no PostgreSQL. O cabeçalho contém versão, rodada e temporada. Índices únicos protegem o código de convite, a chave de idempotência do ledger por mundo e clube e o resumo por mundo e temporada. A versão da rodada é token de concorrência EF. As contas e sessões têm chaves estrangeiras; o vínculo do ledger, dos resumos e do catálogo de clubes ao save é conferido no importador.

Em modos locais, a API usa `EnsureCreated` e DDL idempotente para recursos auxiliares. O modo comercial verifica um esquema provisionado previamente. Nenhuma migration EF ou alteração de esquema foi criada. O destino da ferramenta usa o modelo EF vigente e o DDL de aparência existente. O catálogo externo e as tabelas experimentais exigem inventário próprio; a ferramenta recusa origens com tabelas de aplicativo desconhecidas.

No modo Account, uma sessão dedicada toma o advisory lock exclusivo por banco. As gravações de mundo usam uma transação com lock compartilhado de escrita e verificação da sessão dona. Troca de executor aguarda as escritas em voo. Presença, runtime das partidas, grupos SignalR e relógio continuam ligados à API autoritativa. Os recibos continuam no snapshot e o ledger mantém unicidade por mundo, clube e chave. PostgreSQL permite transações concorrentes de mundos independentes atendidos pelo mesmo processo. Réplicas simultâneas para escrita exigiriam coordenação, afinidade e integração de presença entre processos. [TOAST](https://www.postgresql.org/docs/18/storage-toast.html) · [Locks](https://www.postgresql.org/docs/18/functions-admin.html) · [Etapa 1](MULTIPLAYER_ETAPA_01.md)

## Procedimento de migração e restauração

1. Produzir uma cópia SQLite consistente via Online Backup ou exportação física com a API parada e snapshot durável confirmado. Fechar a cópia, verificar que ela está sem arquivos auxiliares e registrar SHA-256. O laboratório usou apenas fixtures próprios.
2. Provisionar um banco descartável novo no PostgreSQL, com porta e diretório separados. A ferramenta aceita apenas loopback e banco `audit_*` em porta dedicada. Durante a importação, manter a API do destino desligada. O importador adquire a autoridade e verifica chaves e conteúdo antes do commit. Um destino inicializado precisa ser substituído por outro banco vazio para repetir o ensaio.
3. Executar o importador a partir da raiz. O resultado só é emitido após verificar todos os conjuntos de linhas e confirmar a transação.

```powershell
$audit = 'artifacts/stage4-repeat'
dotnet build docs/audit-scalability/EngineAudit.csproj -c Release --artifacts-path "$audit/tool"
$engine = "$audit/tool/bin/EngineAudit/release/EngineAudit.dll"
$env:STAGE4_PG = 'Host=127.0.0.1;Port=54983;Database=audit_novo;Username=audit'
$copy = (Resolve-Path "$audit/fixture-fechada.db").Path
dotnet $engine sqlite-to-postgres $copy (Get-FileHash -LiteralPath $copy).Hash
```

O importador conserva IDs, associação entre contas e treinadores, preferências, versões, contratos, transferências, estatísticas e histórico. Copia o JSON legado e decodifica Gzip de forma exata para o texto do PostgreSQL, sem reserializar o agregado. As datas das colunas relacionais são convertidas para UTC com precisão de microssegundo, com contagem de perdas abaixo dessa precisão. Os timestamps no JSON mantêm o texto original. A continuidade de cookies entre ambientes depende da configuração e das chaves de Data Protection. O teste validou autenticação por credenciais após a migração.

A fixture mista continha dois saves, incluindo um Gzip de dez temporadas, duas contas, uma preferência de aparência, dez resumos e 51.967 lançamentos no total. A versão 21062 e o SHA-256 do JSON histórico foram preservados. A API PostgreSQL respondeu a login, listagem, snapshot, mercado, carreira e classificação. O SQLite de origem manteve o SHA-256 durante o ensaio. A abertura imutável do importador foi validada em cópia fechada, sem criar WAL ou SHM na origem. `pg_dump` em formato custom e `pg_restore` para outro banco preservaram contagens, IDs, versões e hashes de snapshots. [Migração mista](../artifacts/stage4-run/final-verified/result.json) · [Leitura imutável](../artifacts/stage4-run/immutable-migration.json) · [Verificação de restauração](../artifacts/stage4-run/restore-verified.txt)

As verificações negativas recusaram a repetição em um destino inicializado e a origem com SHA-256 divergente. [Resultado](../artifacts/stage4-run/guard-final-result.txt) · [Destino](../artifacts/stage4-run/guard-final-target.log) · [Origem](../artifacts/stage4-run/guard-final-sha.log)

## Executor e compatibilidade

- O teste real de dois processos rejeitou o concorrente em 0,82 s e recuperou a API em 3,40 s. O novo processo apresentou outra época SignalR e recuperou a versão durável 4. Dois mundos diferentes avançaram com sucesso e um replay concorrente conservou a versão. Após terminar deliberadamente a sessão de autoridade do cluster descartável, o processo perdeu a autoridade e encerrou em 0,86 s. [Medições](../artifacts/stage4-run/authority/result.json)
- Seis testes passaram na seleção de persistência. Incluem dois casos PostgreSQL com interceptação antes e imediatamente depois do commit transacional, recuperação concorrente, pênalti e substituição após reinício em SQLite e PostgreSQL. O teste comercial PostgreSQL passou com HTTPS simulado pelo TestServer, CSRF e cookie Secure. Um diretório explícito de chaves para AccountTest isolou a fixture de migração do DPAPI do perfil Windows. [Commit e reinício](../artifacts/stage4-run/pg-tests.log) · [Conta comercial](../artifacts/stage4-run/account-verified.log)
- Os 63 testes Flutter selecionados passaram, com cobertura das interfaces atual e clássica, lobby, conta, contratos, navegação e fluxo incremental. Usam fixtures locais de Flutter. A seleção de mercado e contratos passou em 13 casos, incluindo disputa de compra e idempotência após reinício em SQLite e PostgreSQL. O SignalR com PostgreSQL foi observado separadamente no teste de mudança de processo. A cobertura manual de ambos os temas conectados ao PostgreSQL, incluindo todos os fluxos de carreira e leilões, segue aberta. [Flutter](../artifacts/stage4-run/flutter.log) · [Mercado](../artifacts/stage4-run/market-verified.log)

## Desempenho

API .NET 10 e PostgreSQL 18.4 no mesmo host Windows, em loopback. Cada mundo tinha 64 clubes. A rodada usa prontidão, ticks em lotes e avanço, com persistência real. Os testes de carga usaram um processo em LocalDev, com relógio automático e verificação de autoridade por transação desativados, para que cada provedor receba a mesma sequência HTTP. A autoridade em PostgreSQL foi verificada no ensaio separado de duas APIs. A primeira execução de 50 usou 50 escritas simultâneas. Em SQLite, 37 de 50 terminaram e 13 falharam ou foram canceladas, incluindo respostas HTTP 503. O mesmo PostgreSQL completou 50 de 50. O reteste limitado a quatro leituras/escritas e três rodadas por vez completou os dois lotes sem erro. [Rajada SQLite](../artifacts/stage4-run/sqlite-50/results.json) · [Rajada PostgreSQL](../artifacts/stage4-run/pg-50/results.json)

| Mundos e provedor | Navegação HTTP p50/p95/p99 | Escrita HTTP p50/p95/p99 | Rodada p95 | CPU API na fase de rodada | Pico RSS API na fase | Rodadas concluídas |
| --- | --- | --- | ---: | ---: | ---: | ---: |
| 10 SQLite | 6 / 109 / 881 ms | 1.238 / 3.604 / 3.604 ms | 6,80 s | 13,6 CPU-s | 461 MiB | 10/10 |
| 10 PostgreSQL | 6 / 19 / 134 ms | 793 / 815 / 815 ms | 5,00 s | 11,0 CPU-s | 565 MiB | 10/10 |
| 50 SQLite limitado | 12 / 274 / 305 ms | 165 / 2.160 / 2.790 ms | 5,06 s | 53,4 CPU-s | 631 MiB | 50/50 |
| 50 PostgreSQL limitado | 4 / 10 / 22 ms | 174 / 1.269 / 1.271 ms | 5,27 s | 33,7 CPU-s | 687 MiB | 50/50 |
| 100 SQLite limitado, reteste | 6 / 486 / 966 ms | 185 / 1.436 / 2.536 ms | 17,45 s | 110,1 CPU-s | 797 MiB | 100/100 |
| 100 PostgreSQL limitado, reteste | 4 / 10 / 14 ms | 183 / 671 / 1.121 ms | 5,88 s | 45,6 CPU-s | 796 MiB | 100/100 |

No primeiro lote de 100, a proteção de RAM interrompeu SQLite após 67 rodadas e PostgreSQL após 400 leituras. No reteste, com o mesmo limite de quatro leituras/escritas e três rodadas simultâneas, ambos concluíram sem erros. SQLite levou 196,5 s no lote de rodadas e PostgreSQL 100,3 s. As latências HTTP p50/p95/p99 dentro da fase de rodadas foram 374/4.877/16.216 ms e 174/3.868/4.510 ms. Com cache aquecido, os snapshots tiveram 12/23/28 ms e 12/26/38 ms. Os resultados são de uma rodada por mundo e uma amostra por configuração. [10 SQLite](../artifacts/stage4-run/sqlite-10/results.json) · [10 PostgreSQL](../artifacts/stage4-run/pg-10/results.json) · [50 SQLite](../artifacts/stage4-run/sqlite-bounded-50/results.json) · [50 PostgreSQL](../artifacts/stage4-run/pg-bounded-50/results.json) · [100 SQLite](../artifacts/stage4-run/sqlite-retry-100/results.json) · [100 PostgreSQL](../artifacts/stage4-run/pg-retry-100/results.json)

Após 50 rodadas, o SQLite registrava 180,4 MiB no arquivo principal e 86,0 MiB em WAL. O PostgreSQL registrava 281,0 MiB no banco, dos quais 268,2 MiB em `ef_worlds` com TOAST e índices. O tamanho de banco PostgreSQL exclui o WAL compartilhado. As fases têm páginas livres e versões MVCC distintas. O coletor Windows mede o dispositivo inteiro e o I/O da API inclui cache e transporte. As consultas de PostgreSQL usam deltas de `pg_stat_database`, amostras de sessões e log de consultas acima de 250 ms, com valores de parâmetros omitidos. As gravações de snapshots e leituras de snapshots grandes apareceram entre os comandos lentos. A CPU/RAM da tabela é da API; o processo principal de PostgreSQL e `pg_stat_database` foram coletados separadamente. [Estatísticas PostgreSQL 50](../artifacts/stage4-run/pg-bounded-50-after.json) · [I/O do host 50](../artifacts/stage4-run/pg-bounded-50/50/disk.jsonl) · [Estatísticas PostgreSQL](https://www.postgresql.org/docs/18/monitoring-stats.html)

No PostgreSQL limitado de 50, os deltas foram 3.619 commits, zero rollbacks, zero deadlocks, 2.695 blocos lidos, 54 ms acumulados de leitura e 1.302 ms de escrita contabilizados no banco. No reteste de 100, 166 amostras registraram até seis conexões, quatro ativas e uma sessão em espera de I/O. Não houve lock ou bloqueio nas amostras, nem deadlocks no contador do banco. O PostgreSQL registrava 552,4 MiB de banco e 535,2 MiB na relação de mundos. SQLite registrava 357,7 MiB de arquivo principal e 298,3 MiB em WAL no fim da carga. A amostragem por intervalo pode perder esperas curtas. O processo pai não representa soma de CPU/RAM de todos os backends PostgreSQL. [Contadores](../artifacts/stage4-run/pg-retry-100-after.json) · [Amostras de sessões](../artifacts/stage4-run/pg-retry-100-connections.jsonl)

No reteste de 100, o volume D: registrou em média 169 IOPS e 20,9 MiB/s durante o lote SQLite, e 40 IOPS e 12,3 MiB/s durante PostgreSQL. O pico de fila foi 10 e 7, respectivamente. Inclui as fases de preparação e navegação, registros e qualquer outra atividade no volume. [I/O SQLite](../artifacts/stage4-run/sqlite-retry-100/100/disk.jsonl) · [I/O PostgreSQL](../artifacts/stage4-run/pg-retry-100/100/disk.jsonl)

## Configuração e operação para revisão

Exemplo de configuração para homologação, com valores a provisionar pelo operador. A API deve receber HTTPS como esquema efetivo da requisição. Antes de instalar um proxy, validar o encaminhamento do esquema com a configuração da versão escolhida do ASP.NET Core e restringir confiança ao proxy. O repositório atual não registra explicitamente o middleware de forwarded headers.

| Chave | Valor de referência |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` para exercitar as salvaguardas comerciais |
| `Elifoot__AuthMode` | `Account` |
| `Elifoot__DatabaseProvider` | `PostgreSQL` |
| `Elifoot__CompressNewSqliteSnapshots` | `false` |
| `Accounts__AllowedOrigins__0` | URL HTTPS exata do cliente homologado |
| `Accounts__KeyDirectory` | Caminho absoluto persistente, com acesso restrito à identidade da API |
| `Accounts__KeyCertificate` | Caminho absoluto do certificado de proteção das chaves. Senha e chave privada vêm do armazenamento de segredos |
| `ConnectionStrings__DefaultConnection` | Conexão por segredo com verificação de TLS, CA provisionada, `Maximum Pool Size=8`, `Timeout=5` e `Command Timeout=20` para repetir o ensaio limitado |

- Usar `Elifoot__AuthMode=Account`, `Elifoot__DatabaseProvider=PostgreSQL`, origens HTTPS explícitas e certificado do proxy ou Kestrel. A conexão Npgsql deve verificar CA e nome do servidor com `SSL Mode=VerifyFull`. O teste foi local com TLS de banco fora do escopo. [Kestrel](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/security-considerations) · [Npgsql](https://www.npgsql.org/doc/connection-string-parameters)
- Injetar a conexão e o material criptográfico via armazenamento de segredos. Reservar conexão direta para o lock de autoridade. Orçar `Maximum Pool Size` junto com a capacidade do PostgreSQL e conexões de backup e monitoramento. O pool máximo de 8 no reteste, com quatro requisições de leitura/escrita e três rodadas simultâneas, é um ponto de partida experimental. Manter o executor único por banco.
- Automatizar backups com retenção e cópia externa definidas pelos objetivos RPO/RTO. Incluir chaves de Data Protection e catálogo se aplicável. Testar restauração de uma versão recente, dos vínculos de conta e da primeira rodada após a recuperação. Para PITR, configurar base backup e WAL arquivado em ensaio separado. [pg_dump](https://www.postgresql.org/docs/18/app-pgdump.html) · [PITR](https://www.postgresql.org/docs/18/continuous-archiving.html)
- Monitorar saúde da autoridade, duração das rodadas, 5xx, RSS da API, conexões ativas, espera e bloqueios, checkpoints, crescimento de snapshot/TOAST, WAL e disponibilidade de disco. Aplicar limite de memória do processo e limitar concorrência pelo p95/p99 observado. Medir a carga comercial sustentada com relógio real, SignalR, TLS e mundos envelhecidos.
- Para rollback de um corte futuro, parar a escrita, preservar os dois bancos, restaurar o backup validado no ambiente anterior e conferir versão, histórico e ledger. Gravações posteriores ao corte exigem reconciliação ou perda assumida dentro do RPO. O ensaio validou restauração de PostgreSQL e preservou a cópia SQLite. Migração reversa SQLite ficou fora do procedimento.

Arquivos da etapa incluem o importador `SqliteToPostgres.cs`, a fixture `stage4-migration-smoke.mjs`, os runners de carga, telemetria e autoridade em `docs/audit-scalability`, `AccountLogin.cs` e os fixtures de integração de conta e commit. O checkpoint contém 59 arquivos conferidos. [Manifesto](../artifacts/stage4-checkpoint-801a5c9e3622/sha256.csv)

## Reprodução da carga limitada

Use o cluster descartável do guia e um banco PostgreSQL novo por nível. Execute os provedores em sequência, sem o perfil de compactação no mesmo host. Rode SQLite com `AUDIT_PG` ausente e PostgreSQL com a conexão de teste. O prefixo `audit_` e a porta 54983 são verificados pelo runner. [Guia do cluster](audit-scalability/REPRODUZIR.md)

```powershell
$env:AUDIT_IO_CONCURRENCY = '4'
$env:AUDIT_ROUND_CONCURRENCY = '3'
$env:AUDIT_SAMPLE_DISK = '1'
Remove-Item Env:AUDIT_PG -ErrorAction SilentlyContinue
node docs/audit-scalability/load.mjs $api "$audit/sqlite-100" 100
$env:AUDIT_PG = 'Host=127.0.0.1;Port=54983;Database=audit_carga_100;Username=audit;Maximum Pool Size=8;Command Timeout=20'
node docs/audit-scalability/load.mjs $api "$audit/pg-100" 100
```
