# Rankings históricos de jogadores 009

30/10/2026, America/Sao_Paulo. **STATUS: PARTIAL**.

Implementação e regressões aprovadas: **237 testes .NET e 28 Flutter**, build Release, Flutter analyze e build Web. A temporada real produziu 351 partidas, 32.520 minutos e 725 gols atribuídos, conferidos após cada uma das 22 rodadas. A etapa obrigatória de navegador real está bloqueada pela ferramenta; não há PASS nem captura de navegador. A alternativa de renderização Flutter local está documentada ao final.

[Armazenamento](PLAYER_STATISTICS_STORAGE_009.md) · [Testes](PLAYER_RANKINGS_TESTS_009.md) · [Migração](PLAYER_RANKINGS_MIGRATION_009.md) · [Premiação](SEASON_TOP_SCORER_AWARDS_009.md) · [Recuperação](PLAYER_RANKINGS_RECOVERY_009.md)

## Critérios

Geral soma as quatro ligas e todas as fases oficiais da Taça, incluindo preliminares efetivamente disputadas e prorrogações. Cada divisão considera somente a divisão persistida na fixture, nunca a divisão atual do atleta ou clube. Taça considera somente CompetitionType diferente de League. Shootout não gera gols de artilharia ou sofridos; pênalti durante o jogo é Goal e conta. Bye sem minuto disputado não dá participação nem estatística.

| Categoria | Principal | Desempates | Mínimo no recorte |
| --- | --- | --- | --- |
| Artilheiros | Mais gols | Maior gols/jogos, depois PlayerId | Nenhum; lista principal contém quem marcou |
| Goleiros (posição G no registro) | Menos sofridos/jogos | Mais jogos; menos sofridos; PlayerId | 20 jogos |
| Defensores (posição D no registro) | Menor (amarelos+3×vermelhos)/jogos | Menos vermelhos/jogos; menos amarelos/jogos; mais jogos; PlayerId | 20 jogos |

Os quocientes são comparados por produtos cruzados BigInteger, sem arredondar antes da ordenação. DTO usa decimal e UI mostra três casas; taxas diferentes podem aparecer iguais visualmente, preservando ordem correta. Com dados legados de participação incompleta, a média fica nula e G/D não entram na classificação principal. Contagem só da posição registrada naquele jogo, sem reclassificação retroativa se o cadastro for editado.

Entrou aos60: uma participação,30 minutos em jogo de90 e só gols posteriores. Saiu aos60:60 minutos e nenhum gol posterior. Entrada aos90 conta participação com zero minutos. Mesmo minuto respeita a ordem original de eventos: gol antes da troca pertence ao goleiro anterior; depois da troca ao novo. Goleiro expulso deixa de sofrer novos gols; eventual improvisado é acompanhado, sem promover sua posição cadastral a G. Banco que não entrou recebe zero.

Amarelo vale1 e vermelho3 somente neste ranking. Segundo amarelo conta mais um amarelo e um vermelho, além do primeiro amarelo; direto conta um vermelho. Não se alteraram faltas, suspensões ou probabilidades esportivas.

## Fonte e períodos

Eventos oficiais de partidas concluídas e captura inicial de jogadores/clubes/posições são a fonte. Consolidação não usa os contadores transitórios SeasonGoals/CupGoals/CareerMatches. O motor antigo incrementa SeasonGoals para todo gol, mais CupGoals na Taça; somar os dois duplicava a Taça. A rota compatível top-scorers agora deriva da estatística oficial e retorna liga/Taça separadamente, sem dupla soma. A evolução esportiva anterior que usa contadores foi preservada.

Períodos: carreira inteira, temporada atual e temporada específica. Transferência, promoção/rebaixamento, demissão de técnico, renovação e virada de temporada não apagam o histórico. O clube exibido na tabela é o atual; buckets históricos guardam o clube na partida. Perfil mostra posição, força atual, comportamento, estrela literal *, totais, divisão/Taça, temporadas e conquistas individuais.

## Execução real

Seed199801,128 clubes,2.308 jogadores no mundo (inclui livres),351 partidas e32.520 minutos, todos pelo motor real minuto a minuto. Estatísticas reconciliadas com os eventos após cada rodada; repetição não alterou os agregados. Cada consulta HTTP da instância privada foi comparada ao snapshot SQLite: **18/18 combinações categoria×competição concordaram**. [Evidência persistida](audit-009/api-persisted-validation.json).

| Recorte T1 | Artilheiros com gol | Goleiros elegíveis | Defensores elegíveis |
| --- | --- | --- | --- |
| general | 377 | 1 | 3 |
| division1 | 50 | 0 | 0 |
| division2 | 56 | 0 | 0 |
| division3 | 54 | 0 | 0 |
| division4 | 47 | 0 | 0 |
| cup | 231 | 0 | 0 |

Cada divisão tem14 partidas de liga por temporada no formato atual. Portanto, em uma única temporada, nenhum G/D alcança20 exclusivamente naquela divisão; a seção principal fica vazia de forma correta. Carreira acumulada em duas temporadas pode qualificar. Os testes exercitam exatamente19/20 e soma entre clubes/fases dentro do mesmo recorte. Dados artificiais usados para esses limites e carga1024 estão identificados como fixtures de teste, sem alegação de temporada real1024.

Artilheiros da temporada real:

| Jogador | Gols | Jogos | Clube no fechamento | Prêmio |
| --- | --- | --- | --- | --- |
| Thiago Machado* | 10 | 20 | Real Pampulha | 3.000.000 |
| Mateus Alves* | 10 | 17 | Guarani da Serra | 3.000.000 |

Ambos ganharam apesar de jogos diferentes (20/17); a ordenação da tabela pode usar média, o título não. Cada clube recebeu3milhões. Após reiniciar o processo da API, IDs, valores e histórico permaneceram iguais: [reinício](audit-009/process-restart-validation.json).

## Interface e bloqueio visual

Menu 9. RANKINGS DE JOGADORES, três categorias, seis recortes, três períodos e seleção de temporada. Tabelas clássicas verdes com rolagem horizontal, seção AINDA NÃO ELEGÍVEIS, perfil clicável e ARTILHEIRO: X VEZES. Painel de premiação por temporada e diálogo automático ao detectar virada de temporada, com total por clube.

28 testes Flutter incluem filtros, erros/retry sem falso vazio, cinco conquistas e texto exato ARTILHEIRO: 5 VEZES, empates/prêmio6milhões, preservação do menu008 e largura390px. Esses testes usam HTTP mockado; a API real foi validada separadamente por HTTP/SQLite. **Isso não substitui inspeção no navegador real.** A tentativa de abrir a instância retornou: Local browser use is disabled in Work settings, mesmo após a ativação informada pelo usuário. [Registro](audit-009/browser-validation.json). Capturas de navegador: **não disponíveis**. As imagens adicionais do renderizador Flutter local, com dados reais capturados, estão identificadas como alternativa e não como sessão Web.

DEV privado preparado: http://127.0.0.1:8089/ e API http://127.0.0.1:5089/. Banco em browser-used-fixture.json. Esta instância usa a temporada real anteriormente preservada, já encerrada e avançada paraT2. A versão de origem e o hash estão no manifesto congelado; a regressão posterior gerou outro arquivo real-season-world com IDs novos, sem substituir o banco já utilizado na verificação. Não houve investigação Android nem publicação.

## Preservação e alterações

Sem repositório Git neste diretório; git rev-parse confirmou a ausência. Baseline009 preserva58 arquivos e SHA256; a recuperação fez novo backup de todo o código atual antes de editar. Os18 arquivos008 conferidos no início coincidiram com o manifesto008. CoachCareerEngine, AiManagerBrain, MarketAndEconomyEngine, TacticsAndSquadRules e Enums permanecem byte a byte iguais ao baseline009. A fórmula de ranking de treinadores e o bloco de moral, convites e tabela da tela008 foram comparados por seção. A tela recebeu ao final um painel adicional de prêmios de jogadores.

MatchEngine recebeu somente **duas chamadas CaptureLineup** (antes dos ticks e antes dos ajustes), sem RNG ou mutação esportiva. Removendo essas duas linhas, o texto é igual ao baseline após normalizar CRLF. Não foram reescritos defesa, gols, disciplina, mercado, estádio ou pausa. O prêmio novo muda caixa e pode influenciar trajetórias futuras de mercado/convites; a lógica008 permanece igual e todas as regressões passaram. A assinatura das50 temporadas não precisa coincidir com008 porque a economia ganhou o prêmio solicitado.

Arquivos existentes alterados:

- src/Elifoot.Infrastructure/EfWorldRepository.cs
- src/Elifoot.Domain/WorldSeedFactory.cs
- src/Elifoot.Domain/CompetitionAndSeasonEngine.cs
- src/Elifoot.Domain/Entities.cs
- src/Elifoot.Domain/MatchEngine.cs
- src/Elifoot.Api/Program.cs
- client/elifoot_flutter/lib/api_client.dart
- client/elifoot_flutter/lib/main.dart
- client/elifoot_flutter/test/widget_test.dart

Novos arquivos de código/testes/scripts:

- src/Elifoot.Contracts/PlayerRankingsDtos.cs
- src/Elifoot.Domain/PlayerStatisticsEntities.cs
- src/Elifoot.Domain/PlayerStatisticsEngine.cs
- src/Elifoot.Application/PlayerRankingsApplicationService.cs
- src/Elifoot.Api/PlayerRankingsEndpoints.cs
- client/elifoot_flutter/lib/player_rankings.dart
- client/elifoot_flutter/test/player_rankings_api_test.dart
- client/elifoot_flutter/test/player_rankings_widget_test.dart
- tests/Elifoot.Domain.Tests/PlayerRankings009Tests.cs
- tests/Elifoot.Integration.Tests/PlayerRankings009IntegrationTests.cs
- tests/Elifoot.Simulation.Tests/PlayerRankings009SimulationTests.cs
- scripts/verify-player-rankings-009.mjs
- scripts/report-player-rankings-009.mjs
- scripts/capture-player-ui-data-009.mjs
- client/elifoot_flutter/tool/qa/player_rankings_live_009_test.dart

[Prova de preservação](audit-009/preservation.json), [diff](audit-009/source-diff.patch), [manifesto final](audit-009/evidence-manifest.json). Sete documentos009, incluindo a validação local complementar, e evidências completam a entrega.

## Relatório da tarefa original e adendo

| Campo | Resultado |
| --- | --- |
| SCORER_GENERAL | PASS |
| SCORER_DIVISION_1 | PASS |
| SCORER_DIVISION_2 | PASS |
| SCORER_DIVISION_3 | PASS |
| SCORER_DIVISION_4 | PASS |
| SCORER_CUP | PASS |
| GOALKEEPER_GENERAL | PASS |
| GOALKEEPER_DIVISIONS | PASS |
| GOALKEEPER_MIN_20_MATCHES | PASS |
| GOALKEEPER_AVERAGE | PASS |
| DEFENDER_GENERAL | PASS |
| DEFENDER_DIVISIONS | PASS |
| DEFENDER_MIN_20_MATCHES | PASS |
| DEFENDER_DISCIPLINE_INDEX | PASS |
| STATISTICS_PERSISTENCE | PASS |
| CLUB_TRANSFER_HISTORY | PASS |
| SEASON_HISTORY | PASS |
| NO_DUPLICATE_STATISTICS | PASS |
| WEB_INTERFACE | PARTIAL — Flutter28 e Web build aprovados; navegador bloqueado |
| ANDROID_RESPONSIVE_LAYOUT | PASS em teste Flutter390px; runtime Android não investigado |
| DOTNET_TESTS | 237 PASS |
| FLUTTER_TESTS | 28 PASS |
| REGRESSIONS | PASS |
| SEASON_TOP_SCORER | PASS |
| MULTIPLE_WINNERS | PASS |
| SAME_CLUB_MULTIPLE_AWARDS | PASS |
| PRIZE_AMOUNT | 3.000.000 por vencedor com clube |
| FINANCIAL_INTEGRITY | PASS |
| PLAYER_AWARD_HISTORY | PASS |
| PLAYER_DETAIL_SCREEN | PASS em renderização Flutter local com dados reais; navegador BLOCKED |
| SEASON_END_SCREEN | PASS em renderização Flutter local com dados reais; navegador BLOCKED |

STATUS geral: **PARTIAL**. Limitação impeditiva do PASS geral: navegador real desabilitado; capturas e conferência visual pendentes. Limites de legado estão documentados em Migração; não há pacote NuGet ou DLL bloqueando os testes finais.

## Validação complementar desta retomada

O navegador oficial continuou retornando o bloqueio, apesar da ativação informada pelo usuário; o inventário expôs zero navegadores. Como alternativa permitida, dois testes renderizaram os widgets de produção pelo Flutter local com 59 respostas HTTP reais capturadas da API/SQLite, reproduzindo somente o transporte em memória. Não se inventaram estatísticas. Foram geradas e inspecionadas imagens de rankings, perfil, premiação e largura 390px. Isso não é uma sessão de navegador: **WEB_BROWSER continua BLOCKED e STATUS geral PARTIAL**.

[Relatório e imagens da alternativa local](PLAYER_RANKINGS_LOCAL_VALIDATION_009.md) · [2 testes de renderização](audit-009/live-flutter-render-tests.log) · [28 regressões Flutter reexecutadas](audit-009/resumed-flutter-tests.log). As 237 regressões .NET anteriores foram mantidas, pois nenhum arquivo de produção foi alterado nesta retomada.

