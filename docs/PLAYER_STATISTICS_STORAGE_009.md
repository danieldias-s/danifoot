# Armazenamento de estatísticas 009

30/10/2026. Persistência validada com SQLite real, reload e reinício de processo; schemaSQL existente preservado.

## Modelo autoritativo

GameWorld.PlayerStatistics contém PlayerCompetitionStatistics agrupada por (PlayerId,Season,CompetitionType,Division,ClubId,Position). Campos: MatchesPlayed,GoalsScored,GoalsConceded,YellowCards,RedCards,MinutesPlayed,HasIncompleteAppearances,HasIncompleteGoals e AppliedMatchIds. Cup usa Division nula; as oito fases oficiais mantêm CompetitionType próprio. Relatórios agregam fases/clube conforme filtro, preservando o registro original. Não usar divisão atual para compor chave histórica.

MatchFixture.StatisticsInitialLineup captura IDs, clube, posição, força e titular/banco antes do primeiro tick ou ajuste. Uma vez válida, não muda com substituições, transferências ou editor. Não consome RNG. A captura exige11 titulares identificados de cada lado, para não inventar partidas em escalação ausente.

## Consolidação e idempotência

Somente IsFinished e CurrentMinute>0. Inicialmente produz deltas por jogador sem tocar nos agregados. Eventos ordenados por minuto e índice original, preservando ordem no mesmo minuto. ActiveIds controlam entradas/saídas; minutos acumulam intervalos entre eventos. Goal é o único tipo que gera gol individual e sofrido, exceto IsOwnGoal, que só gera sofrido. ClubId de Goal indica o beneficiário do gol, incluindo gol contra.

Guardas persistidas: AppliedPlayerStatisticsMatches para o fixture e AppliedMatchIds dentro de cada agregado. Chave lógica de aplicação: (WorldId,MatchId,PlayerId). Uma partida é indivisível na consolidação; bucket guarda cada MatchId. Replay, chamada duplicada e reload não duplicam. O snapshotEF é gravado junto das alterações de ledger em um SaveChanges; não há nova tabela divergente para estatísticas.

HasIncompleteAppearances informa escalação/minutos ou eventos de placar insuficientes: médias nulas, G/D fora do principal. HasIncompleteGoals informa autor individual não verificável: não invalida a presença do goleiro conhecido nem altera placar. Temporada com dados incompletos não concede prêmio automático, pois não há garantia sobre o maior total individual. Avisos de cobertura persistem e são expostos em API/UI. Não se apagam fatos válidos para disfarçar a lacuna.

## Índices e consultas

PlayerStatisticsIndex [JsonIgnore] é Dictionary da chave completa para bucket; reconstruído no reload e usado na consolidação. HashSets dão verificação de aplicação. Consultas agrupam por PlayerId e filtram temporada/tipo/divisão; jogadores são indexados por ID na consulta. São construídos DTOs completos sob o mesmo lock por mundo do relógio/mercado, por PlayerRankingsApplicationService. GET não encerra temporada nem paga prêmio.

Não se criaram índices SQL fictícios sobre dados que permanecem no JSON. Índices existentes de ef_worlds, ef_financial_ledger e ef_season_histories foram preservados; ledger tem unicidade de (GameWorldId,ClubId,IdempotencyKey). EfWorldRepository.EnsureCreated não migra schema existente, razão para manter a extensão009 aditiva no snapshot. [Auditoria de armazenamento](audit-009/API_PERSISTENCE_RECOMMENDATION.md).

18 consultas de domínio com1.024 clubes e18.436 jogadores: [carga](audit-009/QUERY_LOAD_1024_009.json). Consulta HTTP após recarga SQLite fria: [128](audit-009/api-performance-128.json) e [1024](audit-009/api-performance-1024.json). São cargas determinísticas sintéticas identificadas, separadas da temporada real128. Os limites de fumaça adotados foram10s/18 consultas de domínio e30s HTTP; não são promessa de SLA universal.

## Histórico e retenção

Os agregados, guardas e conquistas ficam fora da poda de fixtures/transações antigas no snapshot. InitializeSeasonFixtures zera contadores legados, mas não essas coleções. Prêmios persistem como registros imutáveis SeasonTopScorerAward e contador SeasonTopScorerTitles no Player. ChavesClosedPlayerAwardSeasons impedem reabertura e pagamento após poda do ledger transitório. O ledgerSQL conserva entradas anteriores.

Partidas/eventos antigos podem ter sido removidos antes da009. Só fatos recuperáveis são reconstruídos; veja [Migração](PLAYER_RANKINGS_MIGRATION_009.md). Backup de carreira deve usar o banco/snapshot completo, não o pacote do editor de clubes/jogadores.

## Endpoints

- GET /api/worlds/{worldId}/player-rankings?category=scorers|goalkeepers|defenders&scope=general|division1|division2|division3|division4|cup&period=career|current|season&season=N.
- GET /api/worlds/{worldId}/players/{playerId}/history.
- GET /api/worlds/{worldId}/season-top-scorers?season=N (omissão usa último fechamento registrado).

season só é aceito com period=season no ranking; parâmetros duplicados/vazios/desconhecidos ou temporada futura dão400; mundo/jogador ausente404. Temporada válida sem estatística retorna lista vazia com aviso quando houver, sem inventar dados.

[Testes](PLAYER_RANKINGS_TESTS_009.md) · [Premiações](SEASON_TOP_SCORER_AWARDS_009.md).

## Validação complementar desta retomada

O navegador oficial continuou retornando o bloqueio, apesar da ativação informada pelo usuário; o inventário expôs zero navegadores. Como alternativa permitida, dois testes renderizaram os widgets de produção pelo Flutter local com 59 respostas HTTP reais capturadas da API/SQLite, reproduzindo somente o transporte em memória. Não se inventaram estatísticas. Foram geradas e inspecionadas imagens de rankings, perfil, premiação e largura 390px. Isso não é uma sessão de navegador: **WEB_BROWSER continua BLOCKED e STATUS geral PARTIAL**.

[Relatório e imagens da alternativa local](PLAYER_RANKINGS_LOCAL_VALIDATION_009.md) · [2 testes de renderização](audit-009/live-flutter-render-tests.log) · [28 regressões Flutter reexecutadas](audit-009/resumed-flutter-tests.log). As 237 regressões .NET anteriores foram mantidas, pois nenhum arquivo de produção foi alterado nesta retomada.

