# Regressão da força adaptada — 012C

> **EVIDÊNCIA HISTÓRICA DA ETAPA 012C.** Contagens, nomes de testes, métricas e comandos abaixo pertencem àquela execução. Referências à antiga chance de crescimento, participação por minutos e regras anteriores da Taça não especificam o comportamento atual. Foram superadas pelos ajustes de classificação/Liga de `8e9ee25` e `9af02bc`. Preserve os artefatos originais; regras vigentes em [ELIFOOT98_REGRAS_FORCAS_E_INTEGRACAO_V11.md](catalogos/ELIFOOT98_REGRAS_FORCAS_E_INTEGRACAO_V11.md). Esta correção documental não reexecutou essas suítes.

STATUS: PASS. Evidências reais, geradas localmente sem publicar em produção.

- dotnet build Elifoot.slnx -c Release: zero avisos e zero erros.
- dotnet test Elifoot.slnx -c Release --no-build: 324 testes aprovados, zero falhas/ignorados; mesmo build Release imediatamente anterior. Logs build-final.log e regression-final.log.
- flutter analyze: No issues found.
- flutter test: 54 aprovados. Inclui interface do histórico em 320/390/1280 px e treinador sem jogo com elenco lesionado.
- 100 temporadas no modo clássico: 35.100 partidas minuto a minuto; 100.000 sorteios de duração; replay real duas vezes; demais simulações e matrizes antigas também executadas pela suíte completa.

| Artefato TRX final | Casos |
| --- | --- |
| final012c_net10.0_20261030155545.trx | 5 |
| final012c_net10.0_20261030155552.trx | 266 |
| final012c_net10.0_20261030155728.trx | 34 |
| final012c_net10.0_20261030160536.trx | 19 |

## Matriz dos 36 itens solicitados

Os intervalos abaixo agrupam itens relacionados; os métodos e parametrizações estão nos fontes de teste e nos TRX finais. Os testes novos somam 48 casos sobre os 276 existentes.

| Itens | Cobertura | Testes |
| --- | --- | --- |
| 1–7 | Força 1/50, quatro divisões/oito lugares, empates de teto, DIV1 terceiro e DIV4 líder | ApprovedCeilingsForAllEightPlaces; NonParticipantCanReachApprovedCeiling; CeilingNeverClampsAStrongNonparticipant |
| 8–12 | Mudança de posição, turnos, promoção e rebaixamento | OfficialRoundWaitsForAllResultsAndCapturesBothHalvesBeforePromotion; 100RealSeasonsAndPopulationBalance |
| 13–16 | Banco sem entrada, titular, substituto e perda por participação | ActualMinutesDistinguishUnusedBenchLateEntryAndZeroMinuteEntry; ParticipantCanMaintainOrLoseOneButNeverReceivesReserveGain |
| 17 | Taça, prorrogação e pênaltis sem ganho | EveryCupStageForbidsReserveGrowth; 100RealSeasonsAndPopulationBalance |
| 18–23 | Lesão 2/8/15, parcela diária, máximo e abaixo do máximo | DailyLossIsGradualExactAndRecoversOnVirtualDate; PartialAndZeroInjuryLossDoNotBecomeMaximumOrAutomaticRestoration; DurationAndLossLotteryCanReachMaximumAndAlsoChooseSmallerLosses |
| 24–26 | Lesionados DIV1 líder, DIV4 líder e DIV3 último | InjuredNonparticipantStillReceivesEligibleLeagueGrowth |
| 27–29 | Ganho/perda na mesma data, recuperação e reincidência | SameDateDailyLossPrecedesReserveGainAndReplayDoesNotRepeatEither; PartialAndZeroInjuryLossDoNotBecomeMaximumOrAutomaticRestoration |
| 30–32 | Goleiros, transferência e histórico | 100RealSeasonsAndPopulationBalance (probes G/A); InjuryTransferKeepsInitialLossBudgetAndHistoricalClubs; SqlitePersistsDailyLossDatesRecoveryAndHistoryWithoutReapplying |
| 33–35 | Persistência, idempotência e determinismo | ReprocessingAndReloadPreserveEventsAndDisableOldSeasonalPolicy; SqlitePersistsDailyLossDatesRecoveryAndHistoryWithoutReapplying; DeterministicReplayTwoRealSeasons |
| 36 | Multiplayer | NewRoomsUseOneAuthoritativeVersionAndConcurrentAdvanceDoesNotRepeatDays; IdleCupCoachCanBeReadyWithoutFieldingInjuredPlayersOrAdvancingTheirDays; Multiplayer_100ConsecutiveRounds_8Coaches_SyncCounter_Pauses_Substitutions_Reconnection_AndRoomIsolation |

Extras: fronteiras exatas dos 200 valores cumulativos; monotonicidade da chance de reserva; bloqueio de versão desconhecida; save antigo sem modo permanece legado; bloqueio de avanço de dias com jogo ao vivo; ocorrência original da lesão e força imediata preservadas; jogador lesionado não atua nos minutos seguintes; orçamento diário mantém base inicial após transferência; reprocessamento não dispara evolução sazonal; calendário adia elenco curto; humano sem partida confirma pronto sem escalar lesionado.

A regressão existente cobre rankings/gols/artilharia/prêmios, carreira/moral/convites/demissões, mercado/economia/estádios, oito técnicos por 100 rodadas com pausa global/substituições/reconexão e isolamento de salas. O modo legado repetiu exatamente a distribuição e assinatura das 100 temporadas 012/012B. A fórmula defensiva foi comparada diretamente com o checkpoint e permanece byte a byte idêntica.

As primeiras execuções acharam dois problemas reais: elenco curto por lesões (calibration-first.log) e validação de escalação de humano sem partida (regression.log). Foram corrigidos e os ensaios finais repetidos integralmente. targeted-tests.log também preserva uma falha corrigida na preparação de um fixture de teste (busca de atacante após reduzir o elenco); não se mistura esse log com o resultado final.

Reprodução PowerShell, na raiz do projeto:

~~~powershell
$env:ELIFOOT_AUDIT_OUTPUT='D:\elifoot_98\docs\audit-012c\regression'
$env:FORCE012_MODE='candidate'
dotnet build Elifoot.slnx -c Release
dotnet test Elifoot.slnx -c Release --no-build --logger 'trx;LogFilePrefix=final012c' --results-directory docs/audit-012c/tests
Set-Location client/elifoot_flutter
flutter analyze
flutter test
Set-Location ../..
node scripts/report-classic-force-012c.mjs
~~~

O gerador valida TRX, logs, hashes, continuidade do histórico, unicidade, orçamentos e comparação legada antes de escrever PASS. Para repetir a geração usando os mesmos artefatos, basta o último comando. Para uma nova execução completa, arquivar os quatro TRX final012c anteriores e registrar novamente os logs indicados, preservando a evidência anterior.
