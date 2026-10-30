# CLASSIC_ADAPTED_FORCE_EVOLUTION — 012C

TASK: ELIFOOT_CLASSIC_FORCE_EVOLUTION_012C  
STATUS: PASS  
Adaptação autorizada pelo proprietário; não é uma afirmação da fórmula histórica exata do Elifoot 98. Regras versão 1.

## Tetos aprovados

| Posição | DIV4 | DIV3 | DIV2 | DIV1 |
| --- | --- | --- | --- | --- |
| 1 | 12 | 28 | 44 | 50 |
| 2 | 10 | 26 | 42 | 50 |
| 3 | 10 | 26 | 42 | 50 |
| 4 | 8 | 23 | 39 | 49 |
| 5 | 6 | 21 | 37 | 48 |
| 6 | 4 | 18 | 34 | 47 |
| 7 | 2 | 15 | 31 | 46 |
| 8 | 1 | 12 | 28 | 44 |

Limitam apenas ganhos. Um força 40 no líder da DIV4 permanece 40 ao ficar sem jogar; pode perder por participação ou lesão. Força sempre entre 1 e 50. Distritais disputam apenas Taça neste projeto e não recebem crescimento de liga.

## Campeonato e participação

Oito clubes/divisão, 14 jogos, 7 em cada turno. O servidor aguarda todas as partidas da rodada oficial antes de atualizar tabela e força. Usa a posição após essa atualização. A referência inicial é o 4º lugar da divisão atual, sem classificação parcial. Promoção/rebaixamento limpa as posições de referência antes da nova temporada. A posição antiga não é aplicada na divisão nova.

Snapshots persistentes em LeaguePlayed=7 e 14 guardam clube, divisão, posição, teto, data, força inicial/atual por atleta e totais separados dos três motivos. São 6.400 snapshots em 100 temporadas. No calendário global, as rodadas de campeonato são 3,4,6,7,9,10,12 / 13,15,16,18,19,21,22; a final da Taça é a rodada 20. A posição pode alterar o teto sem desfazer ganhos anteriores.

Zero minutos efetivos inclui banco sem entrada, atleta fora da relação e lesionado. No campeonato, abaixo do teto, a chance de +1 é:

p = 0,15 + 0,70 × clamp((teto − força − 1) / max(1, teto − 2), 0, 1).

Chega a 85% distante do teto, diminui até 15% a um ponto e fica zero no teto/acima. Não há ganho automático nem filtro adicional para goleiros. Titular/substituto com minutos positivos não recebe esse ganho: 85% mantém, 15% perde 1, após o encerramento oficial. Entrada no apito final com zero minutos continua sendo não participação. Expulsão/lesão encerram a contagem de minutos. Na Taça, em todas as fases/prorrogação/pênaltis, não há crescimento, mas participantes podem perder. Um atleta lesionado nessa partida não recebe também a perda normal de participação desse episódio.

## Lesão, calendário e ordem

Ocorrência preservada em 0,0018 por lado/minuto, com o mesmo fluxo RNG da ocorrência. O sorteio antigo de duração continua sendo consumido para não deslocar esse fluxo. A nova duração e perda usam um fluxo determinístico separado. Pesos exatos em [WEIGHTED_INJURIES_012C.json](WEIGHTED_INJURIES_012C.json).

Data virtual inicial 01/08/1998, intervalos alternados de 4 e 3 dias entre rodadas, mais 21 dias na transição de temporada. Uma temporada de 22 rodadas ocupa 74 dias entre primeira e última partida, antes de adiamentos; o ano civil não precisa coincidir com o número da temporada. Se um clube com partida não dispõe de 1 goleiro e 10 jogadores de linha por lesões, a rodada inteira espera até a recuperação (máximo 15 dias), preservando simultaneidade e confrontos. Data planejada, real e clubes causadores ficam persistidos. Não há criação de atletas, compra forçada ou cobrança financeira por dia adicional.

Treinador sem partida na Taça confirma pronto para acompanhar mesmo com elenco incompleto; isso não avança dias, escala lesionados ou altera sua tática preferida. A próxima partida exige escalação válida. A UI reconhece esse caso e oferece PRONTO PARA ACOMPANHAR.

Ordem: preparar data → perdas diárias → disponibilidade → partidas → tabela completa → teto → ganho/perda esportiva → histórico → persistência sob a trava do mundo. O relógio virtual não avança durante jogo em curso. Recuperação libera o atleta na data final, sem restaurar força. Lesionado sem jogar pode ganhar +1 no campeonato após a parcela diária, dentro do teto.

## Saves e servidor

CreateWorldRequest cria novas salas com ClassicAdapted (1); permite LegacySeasonal (0) explícito. Campos ausentes em saves antigos assumem LegacySeasonal. Factory de caracterização e importação histórica mantêm o padrão legado. Não foi criado endpoint de conversão de saves. Versões desconhecidas falham exigindo migração explícita. Clientes exibem modo/versão/data e dados calculados pelo servidor; não calculam força. O modo clássico desativa a evolução sazonal antiga e preserva o histórico 012.

## Resultado dos controles

| Campo solicitado | Resultado |
| --- | --- |
| FORCE_CEILINGS / DIVISION_1..4 | PASS — tabela acima e JSON |
| LEAGUE_POSITION / FIRST_HALF / SECOND_HALF | PASS — tabela oficial e snapshots 7/14 |
| RESERVE_GROWTH | PASS — 113.664 ganhos |
| PLAYED_MATCH_DECLINE | PASS — 126.084 perdas |
| CUP_NO_GROWTH | PASS — zero ganhos |
| INJURIES / DURATION_2_TO_15 / WEIGHTED_DISTRIBUTION | PASS — pesos exatos, 100.000 sorteios |
| DAILY_FORCE_LOSS / MAXIMUM_50_PERCENT | PASS — parcelas determinísticas, orçamento sorteado ≤ força inicial × duração/30 |
| INJURED_RESERVE_GROWTH / RECOVERY | PASS — cenários DIV1/DIV4/DIV3 e recuperação por data |
| PERSISTENCE / HISTORY / IDEMPOTENCE | PASS — SQLite, reinício, transferências, chaves sem duplicação |
| DETERMINISTIC_REPLAY | PASS — duas execuções de 2 temporadas reais, hashes iguais |
| 100_SEASON_SIMULATION | PASS — 35.100 partidas reais |
| DOTNET_TESTS | PASS — 324 casos |
| FLUTTER_TESTS | PASS — analyze e 54 testes |
| MULTIPLAYER_REGRESSION | PASS — 100 rodadas/8 técnicos, pausas, substituições, reconexão e isolamento |
| FILES_CHANGED / FILES_CREATED | Listas completas abaixo |
| KNOWN_ISSUES / NEXT_RECOMMENDATIONS | Ver KNOWN_ISSUES_012C.md |

Arquivos existentes alterados especificamente desde o checkpoint 012C:

- client/elifoot_flutter/lib/shell_actions.dart
- client/elifoot_flutter/lib/player_rankings.dart
- client/elifoot_flutter/lib/main.dart
- tests/Elifoot.Simulation.Tests/MultiSeasonAndBenchmarkTests.cs
- src/Elifoot.Application/MultiplayerRoundOrchestrator.cs
- src/Elifoot.Application/ClubAndMarketApplicationService.cs
- src/Elifoot.Contracts/PlayerStrengthDtos.cs
- src/Elifoot.Contracts/Enums.cs
- src/Elifoot.Contracts/Dtos.cs
- src/Elifoot.Domain/Entities.cs
- src/Elifoot.Domain/CompetitionAndSeasonEngine.cs
- src/Elifoot.Domain/AiManagerBrain.cs
- src/Elifoot.Domain/PlayerStatisticsEngine.cs
- src/Elifoot.Domain/MatchEngine.cs
- src/Elifoot.Domain/TacticsAndSquadRules.cs
- src/Elifoot.Domain/PlayerStrengthEvolutionEngine.cs
- src/Elifoot.Domain/WorldSeedFactory.cs

Arquivos criados na 012C (artefatos brutos adicionais em docs/audit-012c):

- src/Elifoot.Domain/ClassicForceState.cs
- src/Elifoot.Domain/ClassicForceEvolutionEngine.cs
- tests/Elifoot.Domain.Tests/ClassicForce012CTests.cs
- tests/Elifoot.Integration.Tests/ClassicForce012CIntegrationTests.cs
- tests/Elifoot.Simulation.Tests/ClassicForce012CSimulationTests.cs
- tests/Elifoot.Simulation.Tests/ClassicForce012CInjuryTriggerTests.cs
- client/elifoot_flutter/test/classic_force_012c_test.dart
- scripts/report-classic-force-012c.mjs
- docs/CLASSIC_FORCE_EVOLUTION_012C.md
- docs/FORCE_CEILINGS_012C.json
- docs/WEIGHTED_INJURIES_012C.json
- docs/INJURY_DAILY_FORCE_LOSS_012C.md
- docs/PLAYER_FORCE_HISTORY_012C.md
- docs/FORCE_BALANCE_100_SEASONS_012C.md
- docs/FORCE_REGRESSION_TESTS_012C.md
- docs/KNOWN_ISSUES_012C.md

Checkpoint: docs/audit-012c/baseline/manifest.json, files e preexisting.patch, HEAD 0c7fc1b430ea2fdc5f474c5a794409922cbcdbe3. As alterações anteriores 012/012B permanecem preservadas. Nenhum reset, commit, publicação ou alteração de outro projeto. A fórmula de setores/defesa permanece byte a byte igual ao checkpoint; mercado, finanças, estádio e carreira mantêm seus algoritmos. Preços/salários são recalculados pelas funções já existentes quando a força muda.
