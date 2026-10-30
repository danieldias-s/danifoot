# Regressão do adendo disciplinar — 012C

STATUS: PASS. 367 casos .NET distintos aprovados: 366 na suíte completa e um replay integral adicional; 56 testes Flutter. A suíte completa inclui todas as regressões anteriores, com 100 temporadas clássicas (35.100 partidas reais) e as simulações legadas de longo prazo. Os casos novos identificados por Discipline012C somam 43.

- dotnet build Elifoot.slnx -c Release: zero avisos e erros (build-verified.log).
- dotnet test Elifoot.slnx -c Release --no-build: suíte completa aprovada (regression-verified.log, TRX verified-discipline012c).
- Replay integral adicional: teste FullFixtureReplayPreservesActualCardsSuspensionHistoryAndOneTimeSettlement, TRX closing-discipline012c e replay-verified.log. Compara o estado completo de duas execuções do mesmo snapshot, incluindo cartões, ciclos, punições, episódios e repetição da liquidação.
- flutter analyze: No issues found; flutter test: 56 aprovados, incluindo escalação reduzida em 390/1280 px, sem overflow.
- Multiplayer: quatro testes novos com 2/4/6/8 clientes HTTP independentes observando uma contusão real, a mesma contagem em campo e um único episódio; o ciclo HTTP/SignalR 2/4/6/8 e o teste de 100 rodadas/8 técnicos também foram reexecutados.
- SQLite: migração de campos antigos, fechamento/reabertura do repositório, saldo/ciclo mantidos e cumprimento idempotente por partida.
- Fechamento: setores e troca legal de goleiro em sector-verified.log; migração de liquidação pendente e de partida ao vivo em migration-verified.log. Estatística aplicada isoladamente não é prova de cumprimento já descontado.
- Estatística separada: 100.000 durações de vermelho e 100.000 durações de lesão. Limites cumulativos inteiros testados sem depender das amostras.

## Cobertura funcional

| Cenários solicitados | Evidência determinística |
| --- | --- |
| 1/2/3 amarelos em jogos distintos, reset e transferência com dois | ThreeIsolatedYellowsShareOneCycleAcrossLeagueCupAndTransfer |
| Dois amarelos no mesmo jogo, ciclos anteriores 0/1/2, expulsão e histórico estatístico | TwoYellowsRestorePriorCycleAndAwardExactlyOneMatch |
| Vermelho de 1/2/3/4 jogos e maior punição com terceiro amarelo | DirectRedCumulativeBoundariesAndMaximumOfPenalties + DirectRed100000DrawsAndExactIntegerWeights |
| Campeonato→Taça, Taça→Campeonato, saldo durante preparação e após encerramento | SuspensionIsServedAtCompletionAcrossCompetitionsAndSurvivesReload |
| Temporadas e transferências com suspensão | TransferAndSeasonChangeKeepCyclesBalancesAndCompleteHistories |
| Folga, distrital sem jogo, adiamento, cancelamento, retry | ByesPostponementCancellationAndRepeatedSettlementDoNotServe |
| 11×11,10×11,9×11,10×10 e forças setoriais | ActualPlayerCompositionDeterminesManDownSectorStrength |
| Expulsão G/D/M/A; não marcar/cometer falta/voltar/substituir | RealRedRemovesEveryRoleAndPreventsSubstitutionReturnAndFutureParticipation |
| Lesão G/D/M/A, sem trocas, troca legal, duas saídas e goleiro improvisado | RealInjuryLeavesFieldImmediatelyAndOnlyLegalSubstitutionCanRestoreItsSlot |
| Elenco apto reduzido e proibição de suspenso em emergência | SuspendedPlayersNeverStartEvenWhenSquadMustPlayShortHanded; ExtremeShortageUsesAllFiveEligibleAthletesWithoutErasingOrServingBansEarly |
| Saves antigos, dados incompletos e partida antiga em andamento | LegacyMigrationPreservesKnownTotalsWithoutInventingMissingEventsOrTypes; OldLiveThirdYellowMarkerRestoresTheTwoPriorYellowsIfSecondYellowFollows |
| Sem perda nominal por suspensão e crescimento de reserva elegível | SuspensionAloneNeverLosesForceAndRemainsEligibleForLeagueReserveGrowth |
| Idempotência de minuto, save/reload e replay completo | RepeatedMinuteAndRestoredFixtureDoNotRepeatEventsCardsOrInjuries; FullFixtureReplayPreservesActualCardsSuspensionHistoryAndOneTimeSettlement |
| Rankings, gols, prêmios, mercado, economia, estádios, carreira, pausas e substituições | Suíte completa anterior reexecutada, sem remoção de testes |

Dois testes antigos tiveram preparação corrigida: a reincidência agora usa fixture novo por seed, pois repetir o mesmo minuto é idempotente; o teste de cumprimento de suspensão passou a indicar CurrentMinute=90 para representar uma partida realizada. Suas verificações esportivas foram preservadas. A instrumentação da simulação clássica apenas acrescentou exportação de cartões/suspensões e métricas; não mudou sua seed, probes ou motor.

## Auditoria de integridade

85.151 cartões e 63.397 registros de suspensão, sem chaves repetidas. 32.826 jogos de cumprimento, sempre retirando um e nunca no mesmo jogo da infração. Uma decisão AWARD por jogador/partida, com motivo final: SECOND_YELLOW=2.251; DIRECT_RED=3.125; THREE_YELLOWS=25.195. A soma de durações concedidas não é comparada cegamente a jogos servidos, pois jogos de fim do ensaio podem deixar saldo pendente e a regra aplica máximos, sem empilhar punições incompatíveis.

248.311 alterações de força verificadas com continuidade por atleta, sem duplicação de chaves ou parcelas diárias; forças dentro de 1..50, tetos respeitados nos ganhos, zero crescimento na Taça, 6.400 snapshots de turno. Replay de duas temporadas reais repetido: AE2367AEE99E5D28D448097F9D960EFC4B28E57005257C2FFE772541AC3B2CC5. A fórmula defensiva, pesos de comportamento e chances de ocorrência estão preservados no fonte. A tabela de tetos, pesos de lesão e motor de evolução 012C permanecem byte a byte iguais ao checkpoint deste adendo.

## Limitações conhecidas

1. Históricos legados podem não conter todos os eventos, datas ou tipos de vermelho. Os totais verificáveis são preservados, o restante é sinalizado em DisciplineCoverageWarnings e UnclassifiedHistoricalRedCards; não há reconstrução fictícia. Em snapshot antigo de partida ao vivo, o ciclo pode ser inferido de marcadores explícitos, mas a origem de uma suspensão anterior pode permanecer desconhecida.
2. IsCancelled/IsPostponed impedem simulação e cumprimento de suspensão enquanto ativos. Não foi criado novo fluxo administrativo de cancelamento/remarcação nem endpoint para anular cartões; os testes cobrem o contrato de elegibilidade desses estados. Adiamento por lesão já existente na 012C continua apenas avançando dias, sem servir suspensão.
3. A escolha autorizada permite começar com todos os aptos disponíveis, inclusive cinco em estresse extremo. Não foi imposto mínimo de sete nem criado atleta artificial. Zero aptos bloqueia preparação. Esta é uma adaptação de design, a acompanhar em sessões humanas.
4. Histórico/chaves são retidos e crescem com a duração do mundo; arquivamento/paginação nova permanece fora deste adendo. Foram usados clientes HTTP/SignalR automatizados, sem alegação de playtest com oito dispositivos físicos.
5. Os relatórios antigos não foram reescritos. Seus números descrevem a versão auditada naquela etapa. Este adendo é a referência das alterações disciplinares atuais; diferenças estatísticas frente a 006 incluem as regras de força 012C e disponibilidade corrigida.

As primeiras execuções preservadas em domain-first.log, targeted-verified.log e regression-final.log registram ajustes de fixtures/escopo de serviço e o limite adicional de sete posteriormente removido. flutter-layout-diagnostic.log identifica um viewport lógico/físico inconsistente do teste, corrigido usando a dimensão física correta; a execução Flutter final passou. A evidência conclusiva está em regression-verified.log, replay-verified.log e nos TRX verificados.

Reprodução, na raiz do projeto:

~~~powershell
$env:ELIFOOT_AUDIT_OUTPUT='D:\elifoot_98\docs\audit-012c-discipline\verified'
$env:FORCE012_MODE='candidate'
dotnet build Elifoot.slnx -c Release
dotnet test Elifoot.slnx -c Release --no-build
Set-Location client/elifoot_flutter
flutter analyze
flutter test
Set-Location ../..
node scripts/report-discipline-012c.mjs
~~~

Para reconstruir somente os documentos, o último comando usa os artefatos existentes, valida chaves, saldos, tetos, hashes, TRX e logs antes de escrever PASS. Ao repetir a captura de testes, arquivar os TRX verificados anteriores e usar os prefixos/documentos de log indicados. Nenhuma publicação em produção ou alteração em outro projeto.
