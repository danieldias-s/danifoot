# Multiplayer · Etapa 5 · Memória e relógio

**PARTIAL.** O relógio PostgreSQL usa fila limitada e concorrência configurável por processo. A API conserva a autoridade exclusiva por banco. As medições e a validação de recuperação desta etapa são de laboratório.

## Origem do consumo de memória

- O cache do repositório mantinha um `GameWorld` inteiro permanentemente para cada mundo carregado. A listagem materializava todos os agregados antes de montar os resumos. Históricos de jogadores, estatísticas e recibos crescem entre temporadas. O perfil anterior de 50 temporadas mediu 468 MiB de JSON e cerca de 4,7 GiB de RSS em uma execução com buffers intermediários. O reteste com heap limitado a 3,5 GiB falhou durante o perfil. [Etapa 3.1](MULTIPLAYER_ETAPA_031.md)
- Os maiores picos ocorrem quando o agregado, o JSON, buffers do codec e parâmetros do provedor coexistem. O repositório evita rastrear entidades nas leituras de mundo. A gravação ainda materializa o snapshot e lê IDs do ledger para deduplicação. A consulta passou a usar a temporada do movimento mais antigo ainda presente no agregado, aproveitando o índice existente. O ledger histórico continua íntegro.
- Cada sala inscrita mantém uma projeção recente no SignalR. A projeção é liberada após a última inscrição V2. Durante a transmissão coexistem projeções e buffers de transporte. Os bytes recebidos são medidos no cliente de carga. O heap não foi atribuído individualmente a SignalR, Npgsql ou EF Core.
- A métrica de alocação é a diferença do contador gerenciado do processo durante o lote dividida por rodadas concluídas. Inclui navegação, hidratação e runtime. O LOH corresponde à última coleta reportada. RSS inclui memória nativa e páginas mantidas pelo runtime. [Métricas do runtime .NET](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/built-in-metrics-runtime) · [LOH](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/large-object-heap)

## Cache e persistência

A listagem e a busca administrativa legada iteram os mundos um a um. O repositório mantém referências fortes aos mundos ativos, com gravação pendente ou cuja versão difere da versão durável. O cache inativo usa limite por contagem e expiração. Uma referência fraca mantém a identidade enquanto outro consumidor ainda usa o agregado, evitando criar uma segunda instância mutável só porque a retenção forte expirou. A varredura tem intervalo de até um segundo sob atividade normal. A associação com o banco continua baseada em IDs e o limite de cache não dimensiona o heap de mundos ativos.

Configuração padrão no código. `Elifoot:InactiveWorldCacheCount=16` e `Elifoot:InactiveWorldCacheSeconds=120`. Valor zero para contagem desabilita retenção forte de mundos inativos. O TTL e a quantidade são parâmetros de retenção, e a coleta de lixo decide quando a memória fica reutilizável. A coleção de locks do orquestrador segue por ID. O repositório limita os semáforos de I/O a 256 posições, com possível espera compartilhada em caso de colisão.

No Gzip, o envelope agora recebe Base64 diretamente na string de saída. Isso elimina a string Base64 intermediária, preserva o prefixo e mantém o JSON UTF-8 utilizado. A opção permanece desativada. Brotli continua restrito ao perfil da Etapa 3.1. O tamanho eliminado em caracteres depende do snapshot. A economia em pico de RSS em uma gravação integrada de 50 temporadas continua sem medição pareada.

## Relógio e falhas

Antes, uma varredura executava as salas em série. Agora a varredura ordena o trabalho pela próxima hora elegível e coloca no canal apenas IDs, timestamps e tipo de trabalho. Há no máximo uma unidade enfileirada ou executando para cada mundo. Um mundo adiado por fila cheia mantém sua posição temporal e é considerado na próxima varredura. O processamento conserva o lock por mundo, a sequência da rodada e a autoridade PostgreSQL existente. [Canais limitados .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels)

`Elifoot:ClockConcurrency` (1–10, padrão 1) limita quantas unidades de relógio são processadas ao mesmo tempo. `Elifoot:ClockQueueCapacity` (1–4096, padrão 64) limita quantas aguardam. A unidade de trabalho é tick, expiração de pausa ou recuperação. A rodada em si continua no mundo até o resultado. O limite não restringe o número de mundos em partida ao mesmo tempo nem requisições HTTP concorrentes. Essa distinção orienta o dimensionamento de RAM e conexões.

Se um tick já alterou o agregado e sua gravação falha, a API bloqueia leituras da versão pendente e repete o mesmo checkpoint, sem simular outro minuto antes de confirmá-lo. O ID do checkpoint permite reconciliar commit ambíguo. Em desligamento, a fila efêmera para de aceitar e processar trabalho. A base durável permite redescobrir as salas e reconstruir trabalho após reinício. Minutos após o último checkpoint podem ser recomputados. O código publica os resultados definitivos depois da confirmação da persistência.

As métricas usam o meter `Elifoot.Multiplayer.Clock` com salas disponíveis, trabalho na fila e em execução, tempos, falhas, tentativas de recuperação, conexões SignalR e RSS. O endpoint `/api/audit/clock` exige LocalDev e configuração explícita. O log de falha do relógio limita frequência e registra somente o tipo. Histograma de espera cobre o tempo após admissão no canal. Ele exclui o tempo antes da admissão quando o canal está cheio.

## Evidências de recuperação

- 24 casos de integração passaram na seleção de SignalR, mercado, carreira e persistência. Quatro casos PostgreSQL adicionais injetaram falha antes e depois do commit no minuto 15 e no resultado. Os quatro passaram e preservaram a versão e os movimentos. [Regressão](../artifacts/stage5-run/regression.log) · [Commit](../artifacts/stage5-run/commit-final.log)
- Quatro casos SQLite passaram com rollback e recuperação de versão, cobrindo JSON e o envelope Gzip depois da redução de buffer. [Snapshots](../artifacts/stage5-run/snapshot-test.log)
- O ensaio com processos separados rejeitou a segunda API, encerrou o dono após perda da sessão e recuperou o snapshot pelo SignalR. Dois mundos avançaram de forma independente, e a repetição das confirmações conservou a versão. [Processos](../artifacts/stage5-run/restart/result.json)
- A validação Flutter com dois treinadores confirmou simultaneidade de pausas, comandos repetidos e o avanço único. [Etapa 4.1](MULTIPLAYER_ETAPA_041.md)
- Com TTL de um segundo e fila de dois itens, o cache forte chegou a zero após a rodada. A leitura seguinte recuperou o resultado durável na versão 94 e o avanço confirmou a versão 95. [Expiração e retorno](../artifacts/stage5-run/verify-ttl/results.json)

## Carga com PostgreSQL

Relógio automático, 64 clubes por mundo, versões duráveis conferidas, tráfego local, janelas de até dez rodadas ativas dirigidas pelo cliente. Os limites de trabalhadores controlam ticks concorrentes. No grupo comparável de dez mundos, cada mundo tinha um observador SignalR V2. O binário anterior não tinha métricas de fila nem alocação. A linha de base executou em LocalDev sem o lock de autoridade por transação, e as demais amostras o habilitaram. As execuções são úteis para dimensionamento inicial e variam com a atividade do host.

| Lote | Concluídas | Rodada p50 / p95 / p99 | Rodadas/min | CPU API | Pico RSS | Espera média / execução média por tick |
| --- | ---: | --- | ---: | ---: | ---: | --- |
| 10, relógio anterior | 10/10 | 59,40 / 64,12 / 64,12 s | 9,4 | 18,9 CPU-s | 454 MiB | — |
| 10, 1 trabalhador | 10/10 | 45,77 / 46,61 / 46,61 s | 12,9 | 20,5 CPU-s | 460 MiB | 210 / 30 ms |
| 10, 2 trabalhadores | 10/10 | 41,36 / 43,19 / 43,19 s | 13,9 | 17,7 CPU-s | 476 MiB | 164 / 52 ms |
| 10, 3 trabalhadores | 10/10 | 32,01 / 35,71 / 35,71 s | 16,8 | 20,7 CPU-s | 571 MiB | 58 / 45 ms |
| 10, 5 trabalhadores | 10/10 | 30,63 / 32,40 / 32,40 s | 18,5 | 21,9 CPU-s | 526 MiB | 36 / 53 ms |
| 10, 10 trabalhadores | 9/10 | 27,61 / 27,65 / 27,65 s | 19,5 | 19,9 CPU-s | 619 MiB | 0,13 / 40 ms |
| 50, 3 trabalhadores | 50/50 | 27,51 / 30,44 / 31,12 s | 21,3 | 51,4 CPU-s | 709 MiB | 21 / 23 ms |
| 100, 3 trabalhadores | 100/100 | 30,92 / 35,27 / 35,82 s | 18,9 | 71,5 CPU-s | 722 MiB | 38 / 38 ms |

Com dez trabalhadores e dez observadores, o cluster registrou `53300` por limite de conexões durante a execução compartilhada. O reteste com pool de seis, fila de dois, dois observadores e oito salas por polling concluiu 10/10 em 28,7 s e atingiu até oito trabalhos simultâneos. O reteste separa duas dimensões, quantidade de observadores e concorrência do relógio, com um padrão diferente de chamadas HTTP. Três trabalhadores completaram a amostra maior sem erro e são o limite inicial medido para aquele lote. Cinco também passaram em dez mundos. O padrão continua em um até validação em ambiente comercial. [Base](../artifacts/stage5-run/baseline-10/results.json) · [1 trabalhador](../artifacts/stage5-run/clock-10-1/results.json) · [2](../artifacts/stage5-run/clock-10-2/results.json) · [3](../artifacts/stage5-run/clock-10-3/results.json) · [5](../artifacts/stage5-run/clock-10-5/results.json) · [10](../artifacts/stage5-run/clock-10-10/results.json) · [50 mundos](../artifacts/stage5-run/clock-50-3/results.json) · [Polling](../artifacts/stage5-run/verify-poll10/results.json)

No lote de 50, com três trabalhadores, o HTTP durante as partidas teve p50/p95/p99 de 10/252/354 ms. O tráfego de WebSocket totalizou 56,9 MiB em 50 conexões abertas por lotes. A média de alocações gerenciadas foi 125,6 MiB por rodada. O LOH ao fim marcava 194 MiB com 52 coletas gen 2 cumulativas. O cache forte terminou com 16 mundos retidos. O cenário de dez mundos com dois observadores e três trabalhadores registrou 112,2 MiB alocados por rodada, 132,97 MiB no LOH ao fim e 2,33 MiB em WebSocket. O polling transferiu snapshots inteiros via HTTP, por isso a queda de volume do WebSocket não representa queda equivalente de tráfego total. O pico RSS aumentou na comparação com o relógio anterior, apesar da menor retenção de agregados após a expiração.

Com TTL acelerado, o cache forte foi de dois mundos para zero entre o fim da rodada e a amostra após 2,2 s. O RSS praticamente não variou nessa janela, de 504,4 para 504,3 MiB. Ainda havia dez referências fracas e não houve coleta adicional registrada na janela. O número de mundos retidos comprova a expiração. A janela curta é insuficiente para atribuir uma economia de RSS ao cache. RSS em repouso antes de provisionar as salas de dez mundos ficou entre 113 e 119 MiB.

No lote de 100, foram concluídas 100 rodadas e 9.000 unidades do relógio em 316,9 s, com zero erros no driver e no contador do relógio. O p50/p95/p99 HTTP foi 10/363/1.216 ms. A alocação gerenciada média foi 79,3 MiB por rodada, o LOH final 193,4 MiB e o cache forte em repouso 16 mundos. Foram 113,3 MiB em WebSocket e 486,9 MiB de tamanho final do banco. O banco de 50 mundos ocupou 281,0 MiB. Três trabalhadores foram o maior limite exercitado até 100 mundos, com no máximo dez partidas em andamento. [100 mundos](../artifacts/stage5-run/clock-100-3/results.json) · [Tamanho de 100](../artifacts/stage5-run/clock-100-3-db.txt)

Com duas rodadas por mundo em dez salas, dez fluxos concluíram 20 rodadas em 64,25 s. A janela teve pico de 451 MiB de RSS, 79,9 MiB de alocações gerenciadas por rodada e 1.807 unidades do relógio concluídas. O heap reportado pela última coleta foi de 104 para 196 MiB e o cache expirou de quatro para zero ao final. É uma amostra curta. A evolução de memória sob operação contínua com muitos saves envelhecidos permanece pendente. [Rodadas sucessivas](../artifacts/stage5-run/repeat-10/results.json)

O nível de 500 ficou fora do ensaio. Um host compartilhado já atingiu o teto de conexões em um cenário menor, e o lote de 100 verificou até dez partidas em andamento. Faltam uma janela dedicada e a medição de margem para ampliar aquela concorrência com histórico longo. O resultado de 100 não estabelece capacidade comercial de 100 partidas simultâneas.

### Limites e reprodução

A fila nunca depende de uma requisição aberta do jogador para avançar um tick. Cancelar o cliente ou fechar o WebSocket cancela a espera no transporte. A unidade de relógio pertence ao mundo. A etapa exercitou abandono por desligamento do executor. Cancelar manualmente trabalho de uma liga e recuperar um backlog grande após falha de processo ainda precisam de teste específico.

Use um cluster descartável e banco novo por execução. `AUDIT_ROUND_CONCURRENCY` controla mundos em partida dirigidos ao mesmo tempo, `AUDIT_HUB_SUBSCRIBERS` seleciona quais usam WebSocket, e `AUDIT_CLOCK_WORKERS` limita trabalhadores. `AUDIT_CLOCK_QUEUE` configura a capacidade. A telemetria de GC é ativada por `AUDIT_CLOCK_METRICS=1`. O runner interrompe se faltar RAM, disco ou se a taxa de erros exceder o limite. [Guia do cluster](audit-scalability/REPRODUZIR.md)

```powershell
$env:AUDIT_CLOCK = '1'
$env:AUDIT_SIGNALR_V2 = '1'
$env:AUDIT_HUB_NAVIGATE = '1'
$env:AUDIT_ROUND_ONLY = '1'
$env:AUDIT_ROUND_CONCURRENCY = '10'
$env:AUDIT_CLOCK_WORKERS = '3'
$env:AUDIT_CLOCK_QUEUE = '8'
$env:AUDIT_CLOCK_METRICS = '1'
$env:AUDIT_AUTHORITY = '1'
$env:AUDIT_PG = 'Host=127.0.0.1;Port=54983;Database=audit_clock50;Username=audit;Maximum Pool Size=12'
node docs/audit-scalability/load.mjs $api "$audit/clock-50" 50
```

## Escopo

Foram alterados o relógio, métricas, listagem, cache e codec em `src`, a carga em `docs/audit-scalability/load.mjs` e testes direcionados. O checkpoint verificou 65 arquivos. A etapa conserva a persistência anterior, as regras do motor e o protocolo incremental. [Manifesto SHA-256](../artifacts/stage5-checkpoint-0be1a76fbd8d/sha256.csv)
