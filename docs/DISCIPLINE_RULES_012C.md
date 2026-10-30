# Disciplina, contusões e inferioridade numérica — 012C

TASK: ELIFOOT_CLASSIC_FORCE_EVOLUTION_012C  
ADENDO: DISCIPLINE_INJURY_AND_MAN_DOWN_RULES  
STATUS: PASS

Adaptação autorizada. Mantém as fórmulas, tetos e distribuição de lesões aprovados na 012C, sem afirmar fidelidade histórica exata ao Elifoot 98. Este adendo complementa os relatórios anteriores, que permanecem preservados.

## Auditoria do comportamento anterior

ProcessTeamDisciplineTick já ponderava faltas pelo comportamento, aplicava cautela após amarelo, removia expulso e emitia segundo amarelo como um único evento SecondYellowRedCard (o segundo cartão amarelo continua contando na estatística). GetEffectivePlayerFoulWeight, probabilidades e ComputeSectorBreakdown foram preservados.

Havia lacunas: vermelho direto sempre gerava dois jogos; o contador de três amarelos era atualizado durante a partida sem recuperar o ciclo anterior quando o primeiro cartão completava três e o segundo expulsava; suspensão era descontada pela liquidação financeira, sem snapshot de elegibilidade no início; não havia histórico disciplinar vitalício; lesionado clássico deixava de contribuir por filtro, mas continuava na lista ativa para troca. O fator coletivo 0,93 por atleta ausente já existia e não foi acrescentado neste adendo.

## Regras finais

Um amarelo isolado conta um por partida oficial. Liga e Taça compartilham o ciclo. Ao terceiro, zera o ciclo e concede um jogo. Segundo amarelo na mesma partida expulsa, preserva os dois cartões nas estatísticas, restaura o ciclo de antes daquela partida e concede exatamente um jogo. Vermelho direto sorteia 1/2/3/4 jogos com pesos 50/30/15/5 somente após sua ocorrência. Se também houve terceiro amarelo, a punição final é a maior; não há soma automática. Um único registro AWARD por jogador/partida guarda a decisão final, preservando ambos os eventos de cartão.

Saldo, motivo, totais, ciclo, CardHistory e SuspensionHistory pertencem ao jogador dentro do mundo. Transferência, divisão, desemprego temporário e virada de temporada não apagam esses campos. Apenas os contadores estatísticos sazonais continuam reiniciando como antes.

Suspensão bloqueia titulares e banco desde a preparação. O clube usa somente aptos. Por escolha explícita do proprietário, pode iniciar com menos de 11; usa a quantidade máxima de aptos possível, até 11, com no máximo um goleiro natural. Se não há goleiro liberado, um atleta de linha real ocupa a função pelo cálculo existente, com eficiência de goleiro de 35%. Não há goleiro fictício. Não foi acrescentado mínimo de sete: o estresse legado encontrou cinco aptos e a escolha autorizada permite jogar reduzido. Quando não há atleta apto, a preparação fica bloqueada. Isso é uma regra do jogo adaptado, não uma afirmação sobre regulamento de futebol externo.

O saldo só cai no encerramento de partida concluída, com minutos realmente disputados, que não esteja cancelada/adiada, e para jogador suspenso no início, sem participação, ainda ligado ao clube daquele jogo. O jogo da infração não conta. Liga e Taça consomem o mesmo saldo. Folga e passagem de dias não consomem. Snapshot no início e chaves persistentes impedem repetição após retry/reinício.

Lesão retira imediatamente da lista ativa e abre vaga identificada. Somente uma troca legal, solicitada pelo treinador ou autorizada pela lógica de IA existente, pode preenchê-la. Sem quota, janela ou reserva apto, o time segue reduzido. Expulso nunca abre vaga substituível. A UI mantém a identificação do lesionado para solicitar a troca e informa a quantidade real em campo.

## Persistência e migração

DisciplineRulesVersion=1 é independente de ForceRulesVersion. SuspensionMatchesRemaining e YellowCardsInCurrentCycle são aliases persistentes dos campos antigos SuspensionRoundsRemaining e YellowCardsAccumulated, com uma única fonte de saldo/ciclo. Totais conhecidos vêm de estatísticas e eventos retidos; datas e tipos de vermelhos ausentes não são inventados. UnclassifiedHistoricalRedCards guarda vermelhos antigos cuja separação não é verificável. A API de histórico também retorna avisos de cobertura.

Partidas legadas já liquidadas recebem trava para não servir suspensão novamente. Em partida antiga em andamento, o marcador explícito de terceiro amarelo permite reconstruir ciclo anterior 2 e completar a correção BUG-006-01. A origem de eventual suspensão anterior ao snapshot pode continuar desconhecida e é sinalizada. Não há re-sorteio de punições antigas. Saves de força legada mantêm sua política de força/lesão anterior; este adendo aplica a disciplina global sem converter automaticamente seu modo de força.

## Resultado solicitado

| Campo | Resultado |
| --- | --- |
| INJURY_PROBABILITY | PASS — 0,0018 por equipe/minuto, inalterada |
| INJURED_PLAYER_SELECTION | PASS — seleção uniforme entre atletas efetivamente ativos |
| YELLOW_PROBABILITY | PASS — gatilho 0,0128 × peso médio e comportamento preservados |
| DIRECT_RED_PROBABILITY | PASS — taxas condicionais 0,005/0,012/0,025/0,048/0,085 preservadas |
| SECOND_YELLOW_DISMISSAL | PASS — saída imediata, dois amarelos históricos e ciclo anterior preservado |
| THREE_YELLOW_SUSPENSION | PASS — três jogos distintos, um jogo de suspensão, reset do ciclo |
| DIRECT_RED_1_TO_4_MATCHES | PASS — pesos exatos e 100.000 amostras |
| CROSS_COMPETITION_SUSPENSION | PASS — saldo único Liga/Taça |
| SEASON_CARRYOVER | PASS — saldo/ciclo/histórico retidos |
| TRANSFER_CARRYOVER | PASS — acompanhamento pelo jogador |
| MAN_DOWN_STRENGTH_IMPACT | PASS — composição real e fórmula anterior |
| INJURY_NO_SUBSTITUTION | PASS — desfalque real, sem substituto automático |
| SAVE_COMPATIBILITY | PASS com lacunas históricas sinalizadas |
| DETERMINISM | PASS — replay integral, duas temporadas reais repetidas e ticks idempotentes |
| MULTIPLAYER | PASS — 2/4/6/8 clientes HTTP e ciclo SignalR existente |
| TESTS | 367 casos .NET, 56 Flutter, 35.100 partidas na calibração clássica |
| KNOWN_ISSUES | Histórico legado incompleto; escalação extremamente reduzida é política adaptada; conferir DISCIPLINE_REGRESSION_012C.md |

Arquivos existentes alterados desde o checkpoint deste adendo:

- tests/Elifoot.Simulation.Tests/ClassicForce012CSimulationTests.cs
- tests/Elifoot.Domain.Tests/PlayerForce012Tests.cs
- tests/Elifoot.Domain.Tests/DomainEngineAndRulesTests.cs
- client/elifoot_flutter/lib/shell_actions.dart
- client/elifoot_flutter/lib/main.dart
- src/Elifoot.Infrastructure/EfWorldRepository.cs
- src/Elifoot.Application/MultiplayerRoundOrchestrator.cs
- src/Elifoot.Application/ClubAndMarketApplicationService.cs
- src/Elifoot.Contracts/PlayerRankingsDtos.cs
- src/Elifoot.Contracts/Dtos.cs
- src/Elifoot.Domain/AiManagerBrain.cs
- src/Elifoot.Domain/Entities.cs
- src/Elifoot.Domain/CompetitionAndSeasonEngine.cs
- src/Elifoot.Domain/MarketAndEconomyEngine.cs
- src/Elifoot.Domain/MatchEngine.cs
- src/Elifoot.Domain/PlayerStatisticsEngine.cs
- src/Elifoot.Domain/TacticsAndSquadRules.cs

Arquivos criados:

- src/Elifoot.Contracts/PlayerDisciplineDtos.cs
- src/Elifoot.Domain/DisciplineEngine.cs
- tests/Elifoot.Domain.Tests/Discipline012CTests.cs
- tests/Elifoot.Integration.Tests/Discipline012CIntegrationTests.cs
- tests/Elifoot.Simulation.Tests/Discipline012CSimulationTests.cs
- client/elifoot_flutter/test/discipline_012c_test.dart
- scripts/report-discipline-012c.mjs
- docs/DISCIPLINE_RULES_012C.md
- docs/RED_CARD_SUSPENSION_012C.md
- docs/YELLOW_CARD_ACCUMULATION_012C.md
- docs/MAN_DOWN_MATCH_IMPACT_012C.md
- docs/INJURY_SELECTION_AUDIT_012C.md
- docs/DISCIPLINE_REGRESSION_012C.md

Checkpoint seguro: docs/audit-012c-discipline/baseline/manifest.json, files e preexisting.patch; HEAD 0c7fc1b430ea2fdc5f474c5a794409922cbcdbe3. Os checkpoints 012, 012B e 012C anteriores permanecem intactos. Nada foi publicado e nenhum outro projeto foi modificado.
