# Elifoot 98 Multiplayer — Etapa 2: otimização SignalR

## Resultado

**PARTIAL.** A redução de tráfego foi de **93,6%** no cenário comparável de dez mundos, acima da meta de 80%. O protocolo incremental mantém o snapshot completo para início, transições e recuperação. Cinquenta mundos concluíram no cenário controlado em memória. Faltam validação de rede lenta com clientes Flutter distribuídos, medição de FPS e RAM no cliente e invalidação imediata específica para todas as operações de mercado, contratos e carreira.

O checkpoint anterior à alteração contém 44 arquivos com SHA-256, status e patches da árvore local. Os testes usaram contas e mundos sintéticos, repositório em memória ou um SQLite descartável. O protocolo acrescenta metadados de DTO e um marcador transitório de persistência. O formato em disco, as regras do motor e a base existente ficaram fora da intervenção. [Checkpoint](../artifacts/stage2-checkpoint-9351a8b985e7/sha256.csv)

## Auditoria das mensagens

O maior custo vinha de `ClockTick`: a cada minuto virtual, o servidor projetava todas as partidas visíveis com placares, elencos, bancos, metadados e a lista acumulada de eventos. Quase todo esse conteúdo era igual ao do tick anterior. O mesmo payload é enviado a cada conexão inscrita. A etapa mediu bytes de mensagens na recepção e CPU agregada da API. A contagem de chamadas internas ao serializador por conexão não foi instrumentada.

| Mensagem no protocolo anterior | Momento e conteúdo | Comportamento em V2 |
| --- | --- | --- |
| Resultado de `JoinRoom` | Snapshot de entrada, com presença | `JoinRoomV2` inclui época, sequência e versão persistida quando disponível |
| `ClockTick` | Snapshot completo por minuto, inclusive no minuto final | `RoundPatchV2` para ticks compatíveis. `RoundSyncV2` no resultado confirmado ou quando a base muda |
| `RoundStateChanged` | Snapshot de início, retomada, ajustes e preparação seguinte | `RoundSyncV2`, também para recuperar qualquer cliente atrasado |
| `GlobalPause` | Janela de pausa e snapshot, com parte dos dados repetida | `RoundSyncV2` inclui a janela ativa |
| `ReadyCounterUpdated` | Prontidão e presença em entradas, saídas e comandos | Mantido em ambos os protocolos. Mudanças nos participantes durante o relógio também podem aparecer no patch |
| `RoundCheckpointPending` | Identidade do mundo e versão candidata no fim da rodada | Mantido. O cliente exibe gravação pendente e aguarda o resultado confirmado |
| `MarketUpdated` | Invalidação breve com mensagem para os clientes da sala | Mantido. Dados de mercado e finanças são consultados pela API autorizada |

As mensagens completas são projeções públicas **da rodada**, com até 32 partidas selecionadas pelas regras existentes. O JSON persistido do mundo, extratos e contratos particulares ficam fora do broadcast. A API continua a servir classificação, mercado, carreira e finanças. A transição confirmada invalida os dados dependentes. Uma reconciliação completa periódica atualiza as leituras visíveis fora da partida, inclusive quando uma operação altera dados sem publicar evento próprio.

## Formato e recuperação

- `RoundPatchV2` identifica mundo, época do processo, sequência anterior e atual, revisão anterior e atual e última versão durável conhecida. O patch inclui minuto, fase, pausa e pênalti pendente. Cada partida alterada é identificada pelo índice na projeção base. A posição só é reutilizada quando os IDs e a ordem das partidas são iguais.
- A projeção acrescenta somente os novos eventos do motor durante o tick. Placares, formação, substituições e limites da partida acompanham a mudança de placar/estado. Elenco titular ou banco segue como uma lista substitutiva somente quando aquele conjunto muda. Isso preserva os dados de cartões, lesões, gols e substituições que o cliente já apresenta.
- Transições não periódicas, resultado confirmado, mudança incompatível de metadados ou revisão sem continuidade geram `RoundSyncV2`. O servidor mantém uma única projeção dos mundos com assinantes V2 durante a partida. Em preparação e resultados guarda só a sequência. Ao sair o último assinante, remove a entrada.
- O Flutter descarta duplicatas e atualizações antigas. Só aplica o patch quando época, sequência anterior e revisão anterior correspondem à base local. Uma lacuna, erro de leitura ou reconexão aciona uma leitura autorizada de snapshot. A consulta condicional pode retornar 204 com base atual. O watchdog faz uma leitura completa periódica para mudanças sem publicação.
- Sequência e época são metadados de transporte. `persistedRoundVersion` vem do repositório quando houve escrita durável ou recarga. Ao trocar a época por reinício, o Flutter aceita a revisão recuperada mesmo se ela for menor que o último tick visual, invalida a apresentação anterior e volta ao estado do servidor. O SQLite de teste confirmou que três ticks posteriores ao último checkpoint não foram tratados como gravados.
- O cliente preserva objetos de partidas sem mudança e usa o mesmo fluxo de apresentação nos temas clássico e atual. A aplicação do tick ainda passa pelo shell. Esta etapa não mediu re-renderização por widget nem demonstrou ganho de FPS.

## Assinaturas e compatibilidade

`JoinRoom` e `JoinRoomV2` compartilham a validação de sessão, posse de treinador e vínculo ao mundo da Etapa 1. Cada conexão fica em um único grupo de sala, na versão escolhida. Ao trocar de sala ou protocolo, sai do grupo anterior. O Flutter também valida o mundo de cada mensagem e invalida callbacks de conexões anteriores. A revogação pelo logout foi testada com um cliente V2 e um legado. O projeto não tem comando de expulsão de treinador da sala. Caso seja introduzido, deve remover a conexão do grupo no momento da revogação.

Clientes antigos continuam recebendo `ClockTick`, `GlobalPause` e `RoundStateChanged`. O Flutter atualizado tenta V2 e retorna a `JoinRoom` se o servidor anterior não reconhecer o método. Grupos mistos preservam as duas representações. Não há log de deltas ou fila de retransmissão própria. Foi mantido o backpressure do transporte SignalR, sem configurar compressão, sem descartar eventos do motor para caber em uma mensagem e sem aumentar os limites padrão. A entrega dos snapshots maiores a conexões lentas ainda precisa de teste. [Configuração do transporte SignalR](https://learn.microsoft.com/aspnet/core/signalr/configuration)

## Medições

Mesmo Windows e .NET 10, loopback, 64 clubes por mundo, primeira rodada de liga, seed 199801 e subsequentes, intervalo de 250 ms, um observador por WebSocket e repositório em memória. Comparação de 10 mundos contra o build final da Etapa 1. As amostras não medem TLS, navegador, fluxo de rede real nem escrita em PostgreSQL. Bytes contam texto SignalR recebido em UTF-8, incluindo separadores e mensagens de controle. Tempo de classificação é a leitura HTTP após receber o resultado. Tempo de encerramento é a janela entre o aviso de checkpoint e a entrega do resultado confirmado.

| Métrica | 10 mundos, legado | 10 mundos, V2 | 50 mundos, V2 |
| --- | ---: | ---: | ---: |
| Rodadas concluídas | 10/10 | 10/10 | 50/50 |
| Bytes recebidos no lote | 189.334.949 | 12.092.475 | 59.650.236 |
| Bytes por conexão/rodada p50 | 18.940.185 | 1.215.531 | 1.193.870 |
| Eventos de hub por conexão | 94 | 94 | 94 |
| CPU do processo (CPU-s) | 11,78 | 7,72 | 22,47 |
| RAM do processo (pico RSS MiB) | 177,0 | 186,6 | 419,9 |
| Rodada p50 / p95 / p99 (s) | 25,28 / 25,60 / 25,60 | 26,65 / 26,95 / 26,95 | 28,33 / 29,50 / 29,57 |
| Intervalo entre ticks p50 / p95 / p99 (ms) | 269 / 363 / 899 | 259 / 327 / 1.129 | 285 / 798 / 1.061 |
| Encerramento p50 / p95 / p99 (ms) | 6,06 / 7,54 / 7,54 | 5,98 / 7,91 / 7,91 | 5,72 / 6,92 / 8,71 |
| Classificação p50 / p95 / p99 (ms) | 10,65 / 17,67 / 17,67 | 10,90 / 17,34 / 17,34 | 9,70 / 17,68 / 20,13 |

Por tipo, no lote de 10 mundos:

| Tipo | Mensagens no lote | Bytes totais | Maior mensagem |
| --- | ---: | ---: | ---: |
| `ClockTick` legado | 900 | 185.633.795 | 239.766 |
| `RoundStateChanged` legado | 10 | 1.842.487 | 184.346 |
| `RoundPatchV2` | 890 | 6.052.479 | 183.265 |
| `RoundSyncV2` | 20 | 4.180.279 | 239.875 |
| Retorno de ingresso legado / V2 | 10 / 10 | 1.845.717 / 1.846.767 | 184.669 / 184.774 |
| `ReadyCounterUpdated`, por protocolo | 20 | 11.580 | 583 |
| `RoundCheckpointPending`, por protocolo | 10 | 1.230 | 123 |

O primeiro tick pode carregar vários elencos inicializados de uma vez. Mesmo em V2 há uma cauda próxima de 180 kB em patches e 240 kB em sincronizações. A quantidade de mensagens ficou estável. A CPU caiu 34,5% e o pico de RSS subiu 9,6 MiB no lote comparável. O tempo de toda a rodada e o p99 entre ticks foram maiores nessa amostra. Em um lote separado com navegação durante a partida, as dez salas concluíram o calendário, clubes e classificação sem erros, com p95 de rodada de 25,53 s. As amostras são curtas e variam com a carga local. Nenhuma conexão observada recebeu evento de outro mundo. [Dados por conexão e tipo](../artifacts/stage2-comparison.json)

## Verificação e pendências

- Integração SignalR com 2 e 8 treinadores. O primeiro pronto aguardou os demais. Clientes V2 e legado coexistiram no mesmo mundo, outro mundo ficou isolado e o cliente desconectado recuperou minuto e versão no reingresso. A sequência detectou um tick deliberadamente omitido. O snapshot seguinte foi aceito e a consulta condicional retornou 204.
- O teste de conta manteve rejeição de treinador alheio, duas sessões no mesmo assento e revogação após logout. A seleção de integração e checkpoint passou em seis testes, incluindo falha antes e depois do commit. A seleção de recuperação passou em quatro, incluindo reinício com SQLite descartável, nova época e replay da operação de prontidão. As seleções têm casos em comum. [Integração e checkpoint](../artifacts/stage2-build/results/stage2-final.trx) · [Recuperação](../artifacts/stage2-build/results/stage2-recovery.trx)
- 53 testes Flutter selecionados passaram. Incluem reconstrução do patch, duplicata, lacuna, mensagem fora de ordem, reinício, convite privado, apresentações clássico/atual e larguras de desktop e mobile. Análise Dart sem diagnósticos. [Testes Flutter](../artifacts/stage2-flutter-verified.log)
- A perda e inversão dos eventos foram verificadas no reconstruidor e no contrato de recuperação. Falta exercitar atraso, perda, retorno após várias rodadas e navegação com WebSockets reais no Flutter sob rede degradada. Também faltam amostras isoladas para Taça, pausa longa, finanças e transferências por evento, além de testes de filas com clientes lentos.
- SQL e esquema ficaram fora da etapa. O ganho de tráfego foi medido em memória e ainda precisa de comparação antes/depois com 50 mundos persistidos. Permanecem as oito falhas de linha de base do Flutter documentadas na Etapa 1. [Etapa 1](MULTIPLAYER_ETAPA_01.md)

### Reprodução

Use `docs/audit-scalability/load.mjs` com `AUDIT_PROVIDER=InMemory`, `AUDIT_CLOCK=1`, `AUDIT_ROUND_ONLY=1` e `AUDIT_HUB_STANDINGS=1`. Execute 10 mundos com o build da Etapa 1. Para a Etapa 2, adicione `AUDIT_SIGNALR_V2=1` e execute 10 ou 50 mundos em uma pasta nova. `AUDIT_HUB_NAVIGATE=1` habilita leituras no vigésimo tick. `results.json` registra métricas da API, bytes por conexão e por mensagem, tempos de classificação e encerramento. [Guia do ambiente](audit-scalability/REPRODUZIR.md)
