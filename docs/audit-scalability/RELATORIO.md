# Elifoot 98 Multiplayer — auditoria de escalabilidade e armazenamento

13 de novembro de 2026 · Windows 10 x64, Xeon E5-2630 v4, 20 CPUs lógicas, 15,9 GiB de RAM, .NET 10, volume D:. Builds locais sobre `2b79c36`. A árvore de trabalho mudou durante os ensaios, portanto os resultados da API, do motor e das suítes de integração têm proveniência separada. Todos os testes de escrita usaram dados sintéticos e bancos descartáveis. Os bancos de QA foram inspecionados em cópias verificadas por hash. Esta auditoria acrescenta documentação e ferramentas de medição.

## Capacidade observada

| Cenário | Resultado |
| --- | --- |
| API + SQLite compartilhado, 10 ligas de 64 clubes | 10/10 rodadas em 12,85 s, p95 de rodada de 12,85 s |
| API + SQLite compartilhado, 50 ligas de 64 clubes | 50/50 gravações de reconexão no reteste, p95 de 4,30 s. Rodadas interrompidas após três HTTP 500 por `SQLITE_BUSY`. Nenhuma completou no reteste. |
| 100, 500 e 1.000 ligas com SQLite compartilhado | Escalada interrompida após o resultado de 50 ligas |
| API + PostgreSQL 18.4, 50 ligas de 64 clubes | 50/50 rodadas em 12,39 s, p95 de rodada de 12,31 s |
| API + PostgreSQL 18.4, 100 ligas de 64 clubes | 100/100 mundos provisionados. Leitura, navegação e escrita completas. Lote de rodadas interrompido pelo limite de RAM livre. |
| Relógio automático + SignalR, 10 ligas de 64 clubes | 10/10 rodadas com um WebSocket por mundo, repositório em memória. p95 de 25,93 s. |
| Orquestrador em memória, 1.000 ligas de 64 clubes | 1.000/1.000 rodadas em 43,82 s, até 20 tarefas concorrentes. Pico de 3,04 GiB RSS. |
| Integração de conta e SignalR | 10 testes de conta passaram com chaves de teste isoladas. O teste de ciclo com seis treinadores também passou. |

### Latência e taxa da API com SQLite

Cada leitura baixa o snapshot HTTP de uma sala. A navegação agrupa lista geral, lista de clubes e classificação. A escrita reconecta um treinador e persiste o mundo. O avanço abrange preparar, iniciar, simular os 90 minutos, confirmar eventual pausa e persistir a preparação da rodada seguinte. O gerador usa o relógio manual do modo LocalDev, requisições em loopback e uma rodada de liga com 16 jogos. As estatísticas HTTP abaixo incluem respostas bem-sucedidas. O último reteste de 50 revela o erro de banco após desabilitar o provedor Event Log do Windows, que falhava ao registrar a exceção no sandbox.

| Ligas | Fase | Operações/s | p50 / p95 / p99 HTTP (ms) | Resultado |
| ---: | --- | ---: | --- | --- |
| 10 | Leitura | 241,55 | 37 / 59 / 64 | 200/200 |
| 10 | Navegação | 64,68 | 8 / 102 / 1.099 | 200/200 navegações |
| 10 | Escrita | 6,38 | 836 / 1.564 / 1.564 | 10/10 |
| 10 | Avanço | 0,78 | 1.133 / 7.346 / 10.266 | 10/10 |
| 50 | Leitura | 227,79 | 204 / 244 / 256 | 200/200 |
| 50 | Navegação | 13,41 | 114 / 4.397 / 5.933 | 200/200 navegações |
| 50 | Escrita | 10,22 | 1.674 / 4.301 / 4.891 | 50/50 |
| 50 | Avanço | 0 | 172 / 8.225 / 10.461 | 0/50 rodadas, 3 falhas observadas pelo cliente antes de cancelar as 47 restantes |

No lote de 10 rodadas, o processo consumiu 14,56 CPU-s, 509 MiB médios e 558 MiB de pico em RSS. A telemetria de processo registrou cerca de 16,8 mil leituras/s e 13,7 mil escritas/s, com 65,8 e 35,5 MiB/s. No reteste de 50, a fase abortada registrou 15,86 CPU-s, 1.215 MiB médios, pico de 1.379 MiB RSS e 28,5 mil leituras/s e 20,8 mil escritas/s. São chamadas de I/O contabilizadas pelo Windows, que incluem cache e podem incluir outros tipos de I/O. A medição de CPU e I/O dos lotes curtos tem resolução de aproximadamente 200 ms e inclui a margem de uma amostra em cada ponta. Uma janela separada de 35 amostras de 1 s durante a execução de temporadas mediu no disco D: média de 339 IOPS e 44,6 MiB/s, picos de 1.580 IOPS e 112,4 MiB/s, fila média 1,29 e pico 8. São números do dispositivo inteiro, incluindo atividade concorrente do host. O contador Windows de tempo de disco excedeu 100% sob filas e não foi tratado como utilização percentual normalizada.

### Motor, memória e relógio

| Ligas | Clubes por liga | Concorrência máxima | Tempo do lote | p50 / p95 / p99 por rodada | CPU-s/rodada | Pico de RSS |
| ---: | ---: | ---: | ---: | --- | ---: | ---: |
| 10 | 64 | 10 | 0,80 s | 0,79 / 0,80 / 0,80 s | 0,58 | 91 MiB |
| 50 | 64 | 20 | 3,02 s | 1,15 / 1,31 / 1,34 s | 0,45 | 257 MiB |
| 100 | 64 | 20 | 4,44 s | 0,87 / 1,03 / 1,11 s | 0,24 | 552 MiB |
| 500 | 64 | 20 | 18,39 s | 0,72 / 0,88 / 0,96 s | 0,19 | 1.930 MiB |
| 1.000 | 64 | 20 | 43,82 s | 0,77 / 1,10 / 3,64 s | 0,20 | 3.113 MiB |
| 10 | 128 | 10 | 1,03 s | 1,00 / 1,03 / 1,03 s | 0,81 | 107 MiB |
| 10 | 1.024 | 10 | 4,35 s | 4,34 / 4,35 / 4,35 s | 4,14 | 677 MiB |

O ensaio executa o orquestrador real e os 90 minutos, com repositório em memória e sem transporte. A concorrência é limitada a 20 tarefas. O tempo por rodada começa quando a tarefa recebe uma vaga. Todas as amostras usam 16 partidas na primeira rodada, inclusive 1.024 clubes, pois há 32 clubes nas quatro divisões nacionais e o restante começa no distrital. A Taça e seus picos de partidas exigem medição própria. Nos lotes de 500 e 1.000 mundos, o heap gerenciado após coleta completa ficou em ~2,34 MiB por mundo de 64 clubes. O pico da execução de 1.000 foi ~3,11 MiB RSS/mundo, como média do lote. Cerca de 50 MiB de alocações transitórias por rodada de 64 clubes, 61 MiB para 128 e 233 MiB para 1.024. CPU por rodada varia com aquecimento/JIT, carga do host e concorrência. O custo por liga depende também de idade do save, competição e quantidade de eventos.

Os objetos de partida pertencem ao `GameWorld`. O código revisado usa RNG baseado em seed e catálogos estáticos de regras. Os dicionários mutáveis de locks e chaves de idempotência são estáticos e indexados por mundo. O repositório EF retém mundos inteiros em cache sem política de expulsão. O relógio faz uma varredura serial, chama `ListWorldsAsync` e aguarda a simulação, o save e a publicação de cada mundo antes de passar ao próximo. A API também usa `ListWorldsAsync` para navegar e faz lock por mundo para gerar cada resumo. O caminho de tick recria mapas de clubes, jogadores e treinadores e monta um DTO completo por minuto. As temporárias são coletáveis, mas os mundos, históricos e dicionários continuam vivos depois da rodada. Histórico e eventos ampliam o objeto residente.

No teste de relógio real, cada um dos 10 WebSockets recebeu 90 ticks e 93 eventos de hub da própria sala. O cliente validou a identidade da sala em cada snapshot e o estado RESULTS. Foram 17,8–18,3 MiB de mensagens recebidas por conexão na rodada e até 1,22 s entre ticks com intervalo configurado de 250 ms. A API usou 11,14 CPU-s e 173 MiB de RSS de pico. A medição usa loopback, um observador por sala e repositório em memória. Latência WAN, replicação de presença e failover entre nós continuam sem validação empírica.

O primeiro gargalo observado no SQLite compartilhado foi a escrita concorrente dos snapshots. A navegação também ficou acima de 4 s no p95 HTTP com 50 salas. No PostgreSQL, a memória livre do host limitou a próxima etapa. Para uso real, a varredura serial do relógio e o volume de broadcast são riscos de escala demonstrados pelo código e pela amostra de dez salas. No lote SQLite de dez, o custo médio foi de 1,46 CPU-s e 50,9 MiB de RSS por liga em jogo. No PostgreSQL de 50, foi de 0,86 CPU-s e 26,0 MiB de RSS por liga no processo da API. RSS por liga é a divisão do total compartilhado do processo e inclui cache, runtime e alocações temporárias.

## Persistência existente

A configuração padrão da API e o Compose usam PostgreSQL. O launcher do playtest de Espanha seleciona SQLite e separa o catálogo dos mundos. Cada instância usa uma única `DefaultConnection` para contas, sessões e todos os mundos. A linha `ef_worlds.Id` identifica o save e contém metadados e `SnapshotJson`. O ledger e os resumos de temporada também separam dados por `GameWorldId`. O catálogo usa `Catalog013A` quando configurado para banco. O catálogo offline lê `catalogos/` e seus arquivos JSON e visuais. Exports, pacotes e ZIPs são artefatos de dados. Flutter usa preferências e armazenamento de sessão local no cliente.

O inventário local registrou 1.503 caminhos de banco, sidecars, dumps, marcadores PostgreSQL e arquivos `.bak` antes dos testes. São 473 `.db`, 4 `.sqlite`, 322 WALs, 318 SHMs, 4 journals, 8 dumps, 88 `.bak` e 286 marcadores PostgreSQL. Total desses caminhos de ~6,0 GiB, incluindo fixtures de `artifacts`, `docs` e `.worktrees`. Os oito clusters locais ocupam outros 16,36 GiB, dos quais 3,75 GiB em `pg_wal`. A contagem de bytes dos clusters inclui seus marcadores. O serviço PostgreSQL instalado no Windows aponta para `C:\Program Files\PostgreSQL\18\data`. O tamanho desse serviço e de eventuais ambientes externos não foi verificado. O volume Compose é `elifoot_pgdata`. [Inventário de arquivos](inventory.csv) · [Inventário de clusters](postgres-inventory.csv)

| Arquivo existente | DB | WAL | SHM | Observação |
| --- | ---: | ---: | ---: | --- |
| `playable-qa.db` | 32,14 MiB | 61,72 MiB | 32 KiB | 8 mundos, 9.506 registros de ledger, 2 resumos de temporada |
| `artifacts/esp-play/worlds-test.sqlite` | 76,25 MiB | 144,05 MiB | 224 KiB | 3 mundos, 11 contas, 12 sessões, 3.729 registros de ledger |
| `artifacts/esp-play/catalog-test.sqlite` | 40,93 MiB | — | — | 3 rascunhos, ocupando 36,65 MiB de páginas |

As três cópias passaram no `quick_check`. `dbstat` atribui ao `ef_worlds` 24,16 MiB no QA e 46,61 MiB no playtest. O ledger usa 2,53 e 0,99 MiB de tabela e 2,41 e 0,93 MiB de índices, respectivamente. No QA, o ledger tem 9.144 linhas da temporada 1 (2.280.962 bytes de campos textuais) e 362 da temporada 2 (88.871 bytes). O playtest tem 3.729 linhas na temporada 1 (931.567 bytes de texto). Esses valores por temporada excluem campos inteiros, overhead e índices. Os arquivos principais tinham 3,01 e 27,61 MiB de páginas na freelist. Os oito snapshots QA variam de ~1,24 a 6,83 MiB. No playtest, um mundo com 128 clubes na temporada 1, rodada 14, contém 27,58 MiB de JSON. Gzip offline ficou entre 8,9% e 12,5% do tamanho bruto nas amostras QA e playtest. A medição apenas mostra potencial de compressão.

Houve atividade externa no playtest de Espanha durante a auditoria. O DB e o WAL mudaram de hash entre duas capturas estáveis, com os mesmos tamanhos, contagens e comprimento dos snapshots em ambas. Os números da tabela descrevem a primeira captura. Os hashes do DB/WAL do QA e do DB do catálogo permaneceram iguais na conferência posterior.

Nenhum grupo de snapshots idênticos byte a byte foi encontrado entre as linhas das duas bases de mundos. Há duplicação semântica. Transações e resumos de temporada constam no JSON e em tabelas relacionais. Jogos ainda presentes em `Fixtures` também aparecem como resumos em `History.Matches`. Os detalhes de partidas, auditoria e mudanças de força devem manter finalidade e granularidade definidas antes de qualquer arquivamento. Rascunhos do catálogo também persistem payloads volumosos, com expiração lógica nas prévias. Não há evidência de expurgo agendado dessas prévias, nem de limpeza automática das linhas de sessões expiradas.

O WAL do lote SQLite de 10 foi de 23,4 MiB após a criação a 108,6 MiB ao fim da rodada. No reteste de 50, passou de 26,4 a 192,3 MiB e o SHM de 64 a 384 KiB. O WAL reaproveita páginas após checkpoints e pode manter o maior tamanho físico já alocado. Transações grandes ou leitores que seguram uma visão antiga podem elevar o pico. O WAL faz parte do estado persistente e o SHM é um índice reconstruível. Avaliar métricas de frames pendentes, idade dos leitores, duração de checkpoint, `freelist_count` e ocupação do volume. [SQLite WAL](https://sqlite.org/wal.html)

### Espaço e duração por temporada

Um save sintético de 128 clubes, seed 199801, jogou 10 temporadas completas. O orquestrador simulou 351 partidas por temporada. A cada rodada o repositório EF persistiu o estado de preparação seguinte em um SQLite novo. Esse método mede tamanho de carreira e transição com gravação real. O tráfego de gravação da partida ao vivo usa checkpoints adicionais.

| Temporadas concluídas | JSON | Páginas ocupadas | DB alocado | WAL na amostra | Fechamento + save |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 0 | 4,62 MiB | 4,80 MiB | 4,80 MiB | 4,86 MiB | — |
| 1 | 22,94 MiB | 25,77 MiB | 47,34 MiB | 24,25 MiB | 2,30 s |
| 2 | 41,56 MiB | 47,07 MiB | 87,39 MiB | 43,57 MiB | 4,68 s |
| 5 | 67,04 MiB | 80,80 MiB | 164,68 MiB | 78,68 MiB | 4,26 s |
| 10 | 112,48 MiB | 139,89 MiB | 270,32 MiB | 125,37 MiB | 5,19 s |

A primeira temporada acrescentou 20,97 MiB de páginas ocupadas sobre o save vazio. Da terceira à décima, o acréscimo líquido ficou entre 10,93 e 12,29 MiB por temporada. Os fechamentos duraram 2,16–5,42 s incluindo preparação e escrita. A execução acelerada de cada temporada inteira levou de 38 a 190 s no host compartilhado. Após o encerramento do processo, o arquivo principal permaneceu em 270,32 MiB e o WAL desapareceu.

| Temporadas | Espaço ocupado por save | Projeção do arquivo alocado | Projeção de arquivo + WAL em atividade |
| ---: | ---: | ---: | ---: |
| 10, medição | 139,89 MiB | 270,32 MiB | 395,70 MiB |
| 50, projeção | 0,56–0,62 GiB | 1,09–1,19 GiB | 1,59–1,75 GiB |
| 100, projeção | 1,10–1,22 GiB | 2,12–2,35 GiB | 3,10–3,44 GiB |

As projeções aplicam os extremos dos incrementos medidos da temporada 3 à 10 sobre os 139,89 MiB observados. Para alocação e WAL, aplicam os fatores de 1,93 e 2,83 observados na temporada 10. São estimativas condicionais para um mundo semelhante. O tamanho de arquivo e WAL pode variar com uso de páginas livres, tamanho das transações, checkpoints, quantidade de saves no arquivo e versão do motor.

O ledger cresceu de 5.115 a 5.233 registros por temporada, somando 51.903 ao fim do teste. Dentro do JSON da décima, `playerStrengthHistory` tinha 49.872 entradas e 31,90 MiB, `playerStatistics` tinha 41.535 entradas e 21,35 MiB, `fixtures` 926 entradas e 14,64 MiB, e `auditLogs` 17.202 entradas e 7,13 MiB. O histórico de força, estatísticas e auditoria continuam crescendo mesmo após a estabilização das fixtures e transações recentes. A tabela `ef_worlds` ocupava 112,59 MiB. O `ef_financial_ledger` usava 14,06 MiB de tabela e 13,13 MiB de índices. `ef_season_histories` ganha apenas uma linha por mundo e temporada.

### Índices, retenção e backup

- O índice único de `InviteCode` garante unicidade pelo valor armazenado. A consulta faz `UPPER(InviteCode)`, o que pede avaliação de índice funcional ou coluna normalizada com unicidade coerente. Para a lista geral, medir projeção paginada e índice `UpdatedAtUtc, Id` com `EXPLAIN`. Ordenar metadados deve ocorrer sem hidratar os saves.
- Os índices do ledger preservam idempotência por mundo, clube e chave, além da busca por temporada/rodada. A carga atual busca todos os IDs do ledger do mundo e compara em memória em PREPARING/RESULTS. Avaliar inserção incremental com restrição de unicidade e cursor durável, preservando garantias de replay. A tabela `ef_club_summaries` estava vazia nas cópias.
- O fechamento de temporada remove fixtures, transações e parte dos registros transitórios antigos, com retenção das duas temporadas encerradas mais recentes no fechamento. O ledger relacional continua acumulando e o histórico de força, estatísticas e outros agregados fica no JSON. Priorizar separar estado quente de histórico fechado, medir compactação sem perda e validar rankings, finanças, replay e recuperação. `VACUUM` recupera espaço livre quando justificado por medição e espaço para a operação.
- O script documentado como backup usa `editor/export-json`, um pacote de editor que recria um mundo novo. O código de exportação mostra um pacote de clubes/jogadores, sem estado completo de carreira, rodada, sessões e contas. Há dumps manuais em scripts de QA, mas não foi localizada política operacional versionada com agendamento, retenção, cópia externa, RPO/RTO ou restauração periódica.
- Para piloto, usar SQLite Online Backup em destino separado e validar restauração de conta, mundo, versão, ledger e histórico. Para PostgreSQL, configurar backups lógicos e/ou base backup + arquivamento de WAL conforme RPO/RTO. Exercitar restauração por instância e recuperação seletiva por mundo em staging, incluindo o catálogo quando for dependência. [SQLite Online Backup](https://sqlite.org/backup.html) · [PostgreSQL pg_dump](https://www.postgresql.org/docs/18/app-pgdump.html) · [PostgreSQL PITR](https://www.postgresql.org/docs/18/continuous-archiving.html)

### Incidente SQLite Error 10

O log histórico em `artifacts/esp-match-fix/api.log` registra oito ocorrências do erro 10 em `UPDATE ef_worlds` com `SnapshotJson` de aproximadamente 27,1 milhões de caracteres, em operações de avanço/continuação. O launcher aponta para o banco do playtest de Espanha. O código primário `SQLITE_IOERR` denota falha na camada VFS. O log antigo não identifica a operação nem o código estendido. O reteste reproduziu `SQLITE_BUSY` (5) por contenção no SQLite compartilhado. O erro 10 permanece sem causa confirmada. A cópia consistente atual passou no `quick_check`, que não explica a condição no momento da falha. Em nova ocorrência, correlacionar código estendido e operação, caminho/volume, espaço livre e quotas, identidade/ACLs, eventos Disk/NTFS, estado de WAL/SHM e possíveis bloqueios de terceiros. Checkpoint, antivírus, sincronização, permissão, storage ou falha de flush são hipóteses até obter esses dados. [Códigos de resultado SQLite](https://sqlite.org/rescode.html)

## Multiplayer e isolamento

- `worldId` delimita carregamento, locks, estado persistente e grupos SignalR. A identidade HTTP vem da sessão de conta, com token opaco armazenado como hash, validade de 12 horas, `accountId` e treinador selecionado. O hub verifica posse da identidade e presença do treinador no mundo antes do ingresso. `sessionId` identifica uma sessão de autenticação e revogação. Um mesmo treinador pode ter duas sessões válidas no mesmo assento.
- O middleware AccountTest cruza ator com conta e associação ao mundo para comandos e exige propriedade do clube em finanças. Operações de edição têm política de host. Join por convite e troca de clube têm regras próprias. A listagem geral e leituras comuns são acessíveis a contas autenticadas, inclusive sem associação ao mundo, conforme a rota. A leitura e descoberta de saves privados exigem uma decisão explícita de produto e autorização correspondente.
- Os testes de integração verificaram contas distintas, posse de treinador e clube, duas sessões no mesmo assento, reconexão, logout e revogação do hub, persistência após restart e um ciclo de jogo com seis treinadores. `AccountTest` é bloqueado em ambiente Production. A checagem de segredo na inicialização de outros modos não constitui um fluxo de autenticação comercial. Validar a configuração de autenticação real antes de expor a API.
- Comandos do mesmo mundo geralmente entram no lock da sala. Locks, cache, presença, relógio e parte das chaves de idempotência são locais ao processo. Há tokens de concorrência e confirmações de checkpoint que reduzem riscos específicos, mas uma gravação comum relê a versão do banco antes de anexar o registro e não garante que o snapshot mutável se originou daquela revisão. Isso permite risco de perda de atualização com dois processos. Leituras sem lock também podem observar o objeto durante mutação. Revisar cobertura de transações, publicação após commit, idempotência durável por ator/ação e repetição depois de falha.
- Operar com múltiplas instâncias exige um dono ativo por mundo com lease/fencing ou fila particionada, controle de versão vinculado ao estado lido, chaves de Data Protection compartilhadas, transporte SignalR com backplane/serviço, presença e revogação distribuídas, observabilidade do relógio e teste de failover. Um banco compartilhado por si só não coordena o relógio nem replica as conexões. [Escala SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/scale?view=aspnetcore-10.0) · [ASP.NET Core em web farm](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/web-farm?view=aspnetcore-10.0)

## Comparação de banco

| Opção | Concorrência e desempenho | Backup, migração e operação |
| --- | --- | --- |
| SQLite global atual | Um escritor por arquivo. Bom para poucos saves ativos e carga baixa. Snapshots completos e checkpoints geram filas sob rajadas. WAL local permite leitura concorrente. | Backup online simples do arquivo inteiro. Restaurar um único save exige extração coerente. Menor custo operacional. |
| SQLite por mundo | Um escritor por arquivo de mundo, isolando a contenção entre ligas. O disco e o processo permanecem compartilhados. | Roteamento por `worldId`, catálogo/contas centrais, controle de handles, backups por arquivo e coordenação de versões. Requer alterar persistência e planejar migração futura. |
| PostgreSQL central | MVCC e concorrência de escrita entre linhas de mundos distintos. TOAST pode comprimir valores grandes, mas snapshots ainda são serializados e substituídos. Autovacuum, WAL, pool e I/O continuam relevantes. | Provedor já integrado. Exige operação e monitoramento, restauração testada e, se adotado, migração validada por mundo. Isolamento na aplicação pode ser reforçado com políticas de banco compatíveis com o pool. [PostgreSQL MVCC](https://www.postgresql.org/docs/18/mvcc-intro.html) |

No microbenchmark SQL isolado, cada escrita substituiu um texto sintético de 2,65 MB em WAL. Com 10 ligas, arquivo único fez 23,1 gravações/s, p95 942 ms, contra 56,7/s e 236 ms com arquivos separados. Com 50 ligas e 20 workers, 27,6/s e 1.672 ms contra 62,7/s e 335 ms. Todas as 30/150 gravações de cada cenário completaram. O teste isola o custo do arquivo e usa payload artificial. A arquitetura por mundo não foi implementada nem executada pela API.

No PostgreSQL 18.4 descartável, com `fsync` e `synchronous_commit` habilitados, o primeiro ensaio com 50 ligas foi interrompido abaixo de 2 GiB livres no host. O reteste após o término da execução de temporadas concluiu todas as fases. A API consumiu 43,19 CPU-s no lote de rodadas, RSS médio de 1.299 MiB e pico de 1.570 MiB. Essas métricas incluem apenas o processo da API. O PostgreSQL rodou no mesmo host. A mistura de atividades concorrentes variou entre os lotes.

| PostgreSQL | Operações/s | p50 / p95 / p99 HTTP (ms) | Resultado |
| --- | ---: | --- | --- |
| 50 · Leitura | 227,79 | 190 / 239 / 255 | 200/200 |
| 50 · Navegação | 70,37 | 10 / 1.375 / 2.578 | 200/200 |
| 50 · Escrita | 49,16 | 958 / 1.009 / 1.014 | 50/50 |
| 50 · Avanço | 4,04 | 2.182 / 8.781 / 9.106 | 50/50 |
| 100 · Leitura | 272,29 | 324 / 461 / 499 | 400/400 |
| 100 · Navegação | 188,95 | 13 / 97 / 1.711 | 400/400 |
| 100 · Escrita | 27,77 | 838 / 2.885 / 2.951 | 100/100 |
| 100 · Avanço | — | — | Parada preventiva com 1,84 GiB livres no host. 50 tarefas em voo canceladas. |

Nos 38 segundos amostrados do teste de 50, o disco D: registrou 87 IOPS em média e pico de 468, 27,5 MiB/s em média e pico de 132,2 MiB/s, e fila média de 2 com pico de 8. Inclui todas as fases e I/O de outros processos. Após o teste, o banco ocupava 279,0 MiB. A relação `ef_worlds` com TOAST e índices ocupava 265,7 MiB. Comparações de tamanho entre provedores devem controlar a etapa do teste, páginas livres e versões ainda mantidas por MVCC. O PostgreSQL de 100 não avançou para 500/1.000 sob o limite de segurança adotado.

Para o piloto inicial, o SQLite compartilhado serve a um grupo pequeno, de processo único, com poucas rodadas em paralelo e backups testados. Dez rodadas em rajada completaram com cauda de 12,85 s. Para dezenas de mundos gravando juntos, necessidade de PITR ou separação de servidor e banco, PostgreSQL é a opção recomendada para validação. Antes de aumentar a concorrência de escrita, medir o atraso dos ticks e a capacidade do relógio após introduzir controle de propriedade do mundo.

## Dimensionamento inicial e condições para lançar

Dimensionamento proposto para validar com carga de produção em staging. Premissas de quatro pessoas conectadas por sala, 40% das salas em rodada no pico, mundos de ~128 clubes, round ativo de 25–60 s, SSD NVMe e banco separado. Centenas de salas ociosas devem sair da memória. Saves longevos e jogos de Taça precisam de ensaio específico. Com 1 treinador por liga ou 100% das salas jogando ao mesmo tempo, recalcular proporcionalmente e distribuir mais workers.

| Usuários simultâneos | Mundos conectados / jogando | Ponto de partida para validação |
| ---: | --- | --- |
| 100 | 25 / 10 | API+worker com 4–8 vCPU e 8–16 GiB, PostgreSQL com 2–4 vCPU e 8 GiB, 100 GiB de NVMe |
| 500 | 125 / 50 | 2 nós de API/worker, cada um com 8 vCPU e 16 GiB. PostgreSQL com 8 vCPU e 16–32 GiB, 250–500 GiB NVMe |
| 1.000 | 250 / 100 | 3–4 nós de API/worker, cada um com 8 vCPU e 16–32 GiB. PostgreSQL com 8–16 vCPU e 32–64 GiB, 500 GiB–1 TiB NVMe |

CPU estimada do motor = ligas em jogo × CPU-s por rodada ÷ duração de uma rodada, com orçamento separado para serialização HTTP/SignalR, autenticação e banco. Em 100 salas jogando, 0,81 CPU-s / 25 s indica ~3,2 vCPU só para o orquestrador em cenário de 128 clubes. Para rede, os WebSockets receberam cerca de 19 MB por conexão por rodada de 26 s no ensaio. Se esse payload se mantiver, a 40% de clientes em jogo, 100/500/1.000 usuários poderiam gerar cerca de 29/146/292 MB/s em mensagens antes de overhead de rede e eventual compressão. Reduzir o broadcast por minuto com deltas e snapshot inicial, além de medir perda, reconexão e p95 de propagação em WAN.

Prioridades de lançamento, na ordem de dependência:

1. Autenticação comercial, política de visibilidade de mundo e testes de autorização por conta/treinador/clube.
2. Um único executor ativo por mundo e fila concorrente limitada para o relógio. Gravar e publicar com versão e idempotência duráveis.
3. Paginar a listagem via metadados. Carregar mundos sob demanda com cache limitado e política de descarte de locks e chaves.
4. Reduzir volume de escrita e leitura do ledger. Separar histórico frio de estado quente com reconciliação e recuperação equivalentes.
5. Reduzir a carga de ticks SignalR, medir fan-out e habilitar reconexão/ressincronização por versão.
6. Testar backup/restauração e perda de worker, além de cargas com a mistura real de leitura, escrita e jogos, monitorando atraso do tick, disco, GC, banco e erro por endpoint.

## Evidências

- `artifacts/scalability-audit/shared-wal-v2/results.json` e `sqlite-confirm50/results.json` para API/SQLite. O log `sqlite-confirm50/50/stdout.log` contém o código 5.
- `artifacts/scalability-audit/postgresql-final50/results.json` e `postgresql-final100/results.json` para PostgreSQL.
- `artifacts/scalability-audit/motor-{64,128,1024}/results.json` para custos do orquestrador.
- `artifacts/scalability-audit/season-engine-128/results.json`, `final-season-storage/storage.json` e `season-disk.csv` para a evolução do save e o disco.
- `artifacts/scalability-audit/hub-clock-final10.log`, `accounts-isolated/accounts.trx` e `integration/account-signalr-final.trx` para relógio e testes de integração. O último TRX contém o teste aprovado de seis treinadores e a tentativa com o key ring padrão indisponível.
- `artifacts/scalability-audit/existing/storage.json`, `existing-followup/storage.json` e `sqlite-file-comparison/results.json` para as cópias e o microbenchmark.

[Como reproduzir](REPRODUZIR.md)
