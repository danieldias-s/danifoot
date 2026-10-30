# Testes da tarefa 009

30/10/2026. **STATUS: PARTIAL** pela etapa visual. Todos os testes automáticos finais executados passaram.

| Suíte | Passaram | Falharam |
| --- | --- | --- |
| Domínio | 195 | 0 |
| Aplicação | 5 | 0 |
| Integração | 29 | 0 |
| Simulação | 8 | 0 |
| Total .NET | 237 | 0 |
| Flutter | 28 | 0 |

.NET SDK10.0.302, Flutter3.38.5/Dart3.10.4, Node24.20.0. Build Release:0 erros/0 avisos. Flutter analyze sem apontamentos; buildWeb com saída absoluta aprovado. Regressão completa: [log](audit-009/final-tests.log). XMLs: [domínio195](audit-009/final-tests/domain195.trx), [aplicação5](audit-009/final-tests/application5.trx), [integração29](audit-009/final-tests/integration29.trx), [simulação8](audit-009/final-tests/simulation8.trx).

O primeiro logger da solução usou o mesmo nomeTRX nas quatro suítes e sobrescreveu o arquivo. O log preserva os quatro resultados. Foram conservados os XMLs de integração/simulação antes/depois da sobrescrita e repetidos domínio/aplicação para gerar XMLs próprios. Isso não soma casos repetidos aos237. A compilação real foi executada antes e durante a rodada completa; --no-build nas duas repetições não foi usado como substituto de executar testes.

129 casos específicos de jogador009 +11 integração009 +3 simulação009 somam143 novos aos94.NET008, total237. Flutter tem18 novos além dos10 anteriores, total28. Theories expandem parâmetros. Logs originais de109/122 casos de desenvolvimento foram preservados; a recuperação terminou com129 jogadores e195 domínio geral.

## Cobertura dos requisitos

| Requisito | Evidência/testes |
| --- | --- |
| Artilheiros e recortes | Scopes_KeepEachDivisionAndCupIndependent; EveryOfficialCupPhase_ContributesOnceToCupAndGeneral; Scorers_TotalGoalsPrecedeAverage_ThenAveragePrecedesStableId |
| Gols oficiais/extra/shootout | RegulationAndExtraTimeGoals_Count_ShootoutAttemptsNeverCount; OfficialGoalWithVerifiedAuthor_IncludingPenaltyAndExtraTime_IsCountedExactly |
| Sem autor/contra/associação | OwnGoalAndUnknownScorer_ChargeActiveGoalkeeperWithoutInventingIndividualGoal; UnattributableOfficialGoal_PreservesScoreAndKnownKeeperWithoutInventingScorer; UnknownScorer_DoesNotDisqualifyKeeperWithTwentyVerifiedAppearances |
| Goleiros/tempo real | GoalkeeperSubstitution_AssignsOnlyGoalsConcededWhileEachKeeperWasActive; SameMinuteEvents_UseOriginalOrder; UnusedKeeper_NeverReceivesAppearanceOrGoalsConceded; SentOffGoalkeeper_DoesNotReceiveLaterGoals |
| 20 por recorte/posição | Eligibility_ChangesExactlyAt20Matches; Eligibility_IsComputedWithinDivisionCupAndSeason; TwentiethFinishedAppearance_QualifiesWithinEachScopeAcrossStatisticalBuckets; OnlyMatchesPlayedInCategoryPosition_CountTowardItsMinimum |
| Disciplina e exatidão | Dismissals_CountRealCardsAndStopMinutes; Defenders_DisciplineScorePrecedesRedRate_AndUsesActualCardCounts; RationalAverageComparison_DistinguishesValuesThatRoundToSameDisplay; Defenders_RedRateTieBreakUsesExactRationalComparison |
| Transferência/divisão/temporada | TransferAfterKickoffAndBetweenGames_PreservesOriginalClubAndDivisionBuckets; PromotionAndRelegation_NeverReclassifyHistoricalCompetition; SeasonFilters_PreserveCareerAndSelectOnlyRequestedSeason; NewSeasonInitialization_ResetsLegacyCountersButRetainsRankingHistory |
| Replay/reconexão/reinício | MatchAndPlayerGuards_PreventDuplicateStatisticsAcrossRetries; TwoDifferentMatches_AccumulateIntoSameBucketAfterJsonReload; SqliteReloadPreservesExactAwardsTitlesLedgerAndMatchIdempotency; process-restart-validation.json |
| Migração e lacunas | LegacyRecovery_ReconstructsStartingLineupAndSubstitutionTimingFromFinalLists; LegacyMissingLineup_RecoversOnlyIndividualFactsWithoutInventingAppearancesOrAverages; Existing008DatabaseMigratesFromConsistentReadOnlyBackupWithoutChangingCareerOrLedger |
| Prêmios e empates | SeasonAwards_AllTiedGoalsWinRegardlessOfMatchesOrAverage; EachWinner_CreditsFullPrizeToClosingClub_AndClubTotalsAccumulate; FreeAgentWinner_KeepsTitleWithZeroAwardWithoutFictitiousClubOrLedgerCredit; FiveDistinctSeasonTitles_PersistWithCorrectPerSeasonHistoryAndNoDuplicateTitles |
| Ledger/sem dupla premiação | AwardLedger_RecordsExactAmountBalanceSeasonAndWorldScopedIdempotencyKey; RepeatedSeasonClose_IsIdempotentAcrossJsonRestart; AwardRecordGuard_ProtectsTitleAndPaymentWhenSeasonCloseMarkerIsMissing; AwardLedgerGuard_PreventsDuplicateCashWhenAwardRecordNeedsRecovery |
| API/concorrência | HttpFiltersCoverAllCategoriesScopesAndPeriodsUsingPersistedStatistics; QueryServiceSharesTheClockLockAndReleasesItAfterCancellationOrFailure; HttpHistoryPreservesHistoricalClubsAndGetRequestsDoNotPayAwardsOrWriteSnapshots |
| 128/1024 e temporada real | PlayerRankings009_QueryLoad_128And1024Clubs; HttpRankingPerformanceIsMeasuredAfterColdSqliteReload; PlayerRankings009_RealFullSeason_ReconcilesEveryRoundAndPaysExactAwards |
| Flutter | Filtros/rotas reais de contrato via HTTP mockado; inelegíveis; histórico5; prêmios6milhões; erros/retry;390px; menu008; diálogo de encerramento uma vez. |
| Navegador real | BLOCKED; não substituído por testes mockados ou capturas geradas. |

## Temporada real e regressões

O teste PlayerRankings009_RealFullSeason_ReconcilesEveryRoundAndPaysExactAwards executou351 partidas pelo motor real,32520 minutos e725 gols individuais, em22 rodadas. A cada rodada confrontou gols/cartões/sofridos com eventos, validou seis recortes de três categorias e reprocessou sem duplicar. Encerramento produziu dois títulos e6milhões; todos os deltas de caixa conciliados. [JSON](audit-009/REAL_SEASON_009.json).

A suíte completa manteve as regressões de mercado/economia/estádio/disciplina/tática,100 temporadas, matriz de confrontos, oito humanos,API/SignalR2/4/6/8 clientes,pausa/substituições/reconexão e PostgreSQL18. A carreira008 voltou a executar50 temporadas reais: 17550 partidas,1630410 minutos, replay determinístico. Artefatos ficam em audit-009/final-regression, preservando relatórios005/006/007/008. A nova premiação altera caixa, portanto a assinatura de carreira difere da008 sem mudança na fórmula do ranking de treinadores.

Consultas128/1024 passaram tanto em domínio quanto em HTTP após SQLitefrio. São fixtures sintéticas de carga; a temporada esportiva real é128. Nenhuma temporada1024 foi alegada.

## Recuperação dos bloqueios

DLLs: API DEV4640 na5088 identificada por caminho/comando e encerrada, sem tocar outros projetos. Os novos binários deQA são copiados para artifacts/player-rankings-009/api, evitando bloquear build futuro. NuGet: assets apontavam cacheDaniel, enquanto a tentativa isolada usou perfilCodexSandboxOffline.212 referências verificadas presentes, fontes lidas e serviçoNuGet retornouHTTP200. Sem reinstalação; --no-restore tecnicamente válido.

Erro original de gol sem autor: teste antigo esperava zero avisos. Ao retomar, havia ajuste parcial nas asserções;122 casos009 já passavam. A investigação confirmou mistura entre ausência de autoria e ausência de participação, além de poder creditar PlayerId conhecido fora do campo/clube. Corrigido em produção e coberto por mais sete casos. [Detalhes](PLAYER_RANKINGS_RECOVERY_009.md).

## Reprodução

PowerShell em D:\elifoot_98. Use pasta nova de execução para preservar evidências:

~~~powershell
$run009 = Join-Path (Get-Location) ('artifacts\player-rankings-009\rerun-' + (Get-Date -Format yyyyMMdd-HHmmss))
New-Item -ItemType Directory -Path $run009 -Force | Out-Null
$env:ELIFOOT_AUDIT_OUTPUT = $run009
dotnet build Elifoot.slnx -c Release --no-restore
dotnet test Elifoot.slnx -c Release --no-restore --logger 'trx;LogFilePrefix=rerun009' --results-directory $run009
Push-Location client\elifoot_flutter
flutter analyze
flutter test
flutter build web --release --dart-define=API_BASE_URL=http://127.0.0.1:5089 --output=D:\elifoot_98\artifacts\player-rankings-009\web
Pop-Location
node scripts/verify-player-rankings-009.mjs
node scripts/report-player-rankings-009.mjs
~~~

Use --no-restore se project.assets.json e pacotes existirem, como nesta máquina; caso contrário, restaurar dependências antes. Não forçar exclusão de DLLs; identificar e parar somente API própria ou usar diretório de saída isolado. Verificar conflitos de porta antes de iniciar API.

## Procedimento visual pendente

Habilitar uso do navegador local nas configurações do Work. Abrir http://127.0.0.1:8089/, usar apelido **Rankings 009** e dispositivo **qa009-web**, entrar na sala RANK009 como desempregado e abrir **9. RANKINGS DE JOGADORES**. Conferir geral, quatro divisões, Taça, goleiros, defensores, período T1 e carreira; perfil de Mateus Alves* e Thiago Machado*, uma conquista e prêmio de 3 milhões para cada clube, total de 6 milhões. A temporada atual T2 começa vazia. Testar 390px e rolagem; G/D por divisão em T1 devem estar inelegíveis pois só há 14 jogos de liga.

Comparar com api-persisted-validation.json e registrar capturas reais. Para observar o diálogo automático, concluir uma temporada na sessão; painel de prêmios já mostra encerramentoT1. Não inserir títulos fictícios nesse banco para ilustrar cinco conquistas: o caso exato5 foi verificado por testeFlutter determinístico. Só então atualizar browser-validation.json e regenerar relatórios para possível PASS geral.

Estado dos processos/banco: [dev-session.json](audit-009/dev-session.json), [fixture usada](audit-009/browser-used-fixture.json). O SQLite vivo e logs de processo são mutáveis; provas estáticas são JSONs de validação, TRX e manifesto. Capturas reais009 indisponíveis devido ao bloqueio descrito.

## Validação complementar desta retomada

O navegador oficial continuou retornando o bloqueio, apesar da ativação informada pelo usuário; o inventário expôs zero navegadores. Como alternativa permitida, dois testes renderizaram os widgets de produção pelo Flutter local com 59 respostas HTTP reais capturadas da API/SQLite, reproduzindo somente o transporte em memória. Não se inventaram estatísticas. Foram geradas e inspecionadas imagens de rankings, perfil, premiação e largura 390px. Isso não é uma sessão de navegador: **WEB_BROWSER continua BLOCKED e STATUS geral PARTIAL**.

[Relatório e imagens da alternativa local](PLAYER_RANKINGS_LOCAL_VALIDATION_009.md) · [2 testes de renderização](audit-009/live-flutter-render-tests.log) · [28 regressões Flutter reexecutadas](audit-009/resumed-flutter-tests.log). As 237 regressões .NET anteriores foram mantidas, pois nenhum arquivo de produção foi alterado nesta retomada.

