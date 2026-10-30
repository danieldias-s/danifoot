import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
const root=path.resolve(import.meta.dirname,'..'), docs=path.join(root,'docs'), audit=path.join(docs,'audit-009');
const read=p=>fs.readFileSync(path.join(root,p),'utf8').replace(/^\uFEFF/,'');
const json=p=>JSON.parse(read(p));
const hash=b=>crypto.createHash('sha256').update(b).digest('hex').toUpperCase();
const slash=p=>p.replaceAll('\\','/');
const table=(head,rows)=>'| '+head.join(' | ')+' |\n| '+head.map(()=> '---').join(' | ')+' |\n'+rows.map(r=>'| '+r.join(' | ')+' |').join('\n');
const write=(name,text)=>fs.writeFileSync(path.join(docs,name),text.trim()+localRenderAppendix+'\n');
const season=json('docs/audit-009/REAL_SEASON_009.json');
const api=json('docs/audit-009/api-persisted-validation.json');
const restart=json('docs/audit-009/process-restart-validation.json');
const browser=json('docs/audit-009/browser-validation.json');
const hasLocalRender=fs.existsSync(path.join(audit,'live-flutter-render/validation.json'));
const localRender=hasLocalRender?json('docs/audit-009/live-flutter-render/validation.json'):null;
const localRenderAppendix=hasLocalRender?`\n\n## Validação complementar desta retomada\n\nO navegador oficial continuou retornando o bloqueio, apesar da ativação informada pelo usuário; o inventário expôs zero navegadores. Como alternativa permitida, dois testes renderizaram os widgets de produção pelo Flutter local com 59 respostas HTTP reais capturadas da API/SQLite, reproduzindo somente o transporte em memória. Não se inventaram estatísticas. Foram geradas e inspecionadas imagens de rankings, perfil, premiação e largura 390px. Isso não é uma sessão de navegador: **WEB_BROWSER continua BLOCKED e STATUS geral PARTIAL**.\n\n[Relatório e imagens da alternativa local](PLAYER_RANKINGS_LOCAL_VALIDATION_009.md) · [2 testes de renderização](audit-009/live-flutter-render-tests.log) · [28 regressões Flutter reexecutadas](audit-009/resumed-flutter-tests.log). As 237 regressões .NET anteriores foram mantidas, pois nenhum arquivo de produção foi alterado nesta retomada.\n`:'';
const career=json('docs/audit-009/final-regression/COACH_CAREER_50_SEASONS_008.json');
const baseline=json('docs/audit-009/baseline-manifest.json');
const sourceFiles=fs.readdirSync(path.join(audit,'final-tests')).filter(f=>/^(domain195|application5|integration29|simulation8)\.trx$/.test(f));
let count=0;for(const f of sourceFiles){const text=read('docs/audit-009/final-tests/'+f);const c=text.match(/<Counters\b[^>]+>/)[0];const a=Object.fromEntries([...c.matchAll(/(\w+)="(\d+)"/g)].map(m=>[m[1],+m[2]]));assert.equal(a.failed,0);assert.equal(a.total,a.passed);count+=a.passed;}
assert.equal(sourceFiles.length,4);assert.equal(count,237);
assert.match(read('docs/audit-009/recovery-flutter-test.log'),/\+28: All tests passed!/);
assert.match(read('docs/audit-009/recovery-flutter-analyze.log'),/No issues found!/);
assert.match(read('docs/audit-009/web-build.log'),/Built/);
if(hasLocalRender){
  assert.equal(localRender.browserUsed,false);
  assert.equal(localRender.inventedSportingDataUsed,false);
  assert.equal(localRender.httpTransportReplayed,true);
  assert.match(read('docs/audit-009/live-flutter-render-tests.log'),/\+2: All tests passed!/);
  assert.match(read('docs/audit-009/resumed-flutter-tests.log'),/\+28: All tests passed!/);
  assert.match(read('docs/audit-009/resumed-flutter-analyze.log'),/No issues found!/);
}
assert.equal(season.Matches,351);assert.equal(season.Goals,725);assert.equal(season.RoundChecks.length,22);
assert.equal(api.AllPersistedValuesMatch,true);assert.equal(restart.RealProcessRestart,'PASS');
const visual=browser.status==='PASS';
const overall=visual?'PASS':'PARTIAL';
const changes=baseline.map(b=>({...b,Path:slash(b.Path),CurrentSHA256:hash(fs.readFileSync(path.join(root,b.Path)))})).filter(x=>x.SHA256!==x.CurrentSHA256);
const protectedFiles=['src/Elifoot.Domain/CoachCareerEngine.cs','src/Elifoot.Domain/AiManagerBrain.cs','src/Elifoot.Domain/MarketAndEconomyEngine.cs','src/Elifoot.Domain/TacticsAndSquadRules.cs','src/Elifoot.Contracts/Enums.cs'].map(p=>{
  const old=baseline.find(b=>slash(b.Path)===p);assert.ok(old,p);const sha=hash(fs.readFileSync(path.join(root,p)));assert.equal(sha,old.SHA256,p);return {Path:p,SHA256:sha,Unchanged:true};
});
function sectionProof(file,start,end){const extract=t=>{const a=t.indexOf(start),b=t.indexOf(end,a+start.length);assert.ok(a>=0&&b>a,start);return t.slice(a,b).replaceAll(/\s+/g,'');};const old=extract(read('docs/audit-009/baseline/'+file)),now=extract(read(file));assert.equal(now,old,start);return {Path:file,Section:start,SHA256Normalized:hash(now),Unchanged:true};}
const sections=[sectionProof('src/Elifoot.Domain/CompetitionAndSeasonEngine.cs','public static List<CoachRankingDto> GetClassicCoachRanking','return ordered;'),
 sectionProof('src/Elifoot.Domain/CompetitionAndSeasonEngine.cs','private static void EvolvePlayersAtSeasonEnd','public static void DismissHumanCoach'),
 sectionProof('client/elifoot_flutter/lib/main.dart','Widget _buildCareerRankingAndInvitationsScreen()','if (seasonHist.isNotEmpty)')];
const oldEngine=read('docs/audit-009/baseline/src/Elifoot.Domain/MatchEngine.cs').replaceAll('\r\n','\n');
const engine=read('src/Elifoot.Domain/MatchEngine.cs').replaceAll('\r\n','\n');
assert.equal(engine.replaceAll('        PlayerStatisticsEngine.CaptureLineup(match, worldPlayers);\n',''),oldEngine);
const awardsTable=table(['Jogador','Gols','Jogos','Clube no fechamento','Prêmio'],season.Awards.Awards.map(a=>[a.DisplayName,a.TotalGoals,a.TotalMatches,a.ClubNameAtClose,a.AwardAmount.toLocaleString('pt-BR')]));
const countsTable=table(['Suíte','Passaram','Falharam'],[['Domínio',195,0],['Aplicação',5,0],['Integração',29,0],['Simulação',8,0],['Total .NET',237,0],['Flutter',28,0]]);
const statuses={STATUS:overall,DOMAIN_TESTS:'PASS — 195 (129 específicos 009)',INTEGRATION_TESTS:'PASS — 29 (11 novos 009)',SIMULATION_TESTS:'PASS — 8 (3 novos 009)',FLUTTER_TESTS:'PASS — 28',GOAL_WITHOUT_AUTHOR:'PASS — placar e goleiro preservados; sem artilheiro inventado',ROOT_CAUSE:'Teste antigo não aceitava aviso; implementação confundia autoria incompleta com participação incompleta.',FIX_APPLIED:'Validação de autor/clube/campo e separação HasIncompleteGoals/HasIncompleteAppearances.',PLAYER_RANKINGS:'PASS — geral, quatro divisões, Taça e períodos',TOP_SCORER_AWARD:'PASS — empatados, 3 milhões por vencedor, clube no fechamento',GOALKEEPER_RANKING:'PASS — mínimo 20 por recorte e gols no tempo em campo',DEFENDER_RANKING:'PASS — índice racional exato, cartões reais, mínimo 20',HISTORICAL_PERSISTENCE:'PASS — transferências, temporadas, recarga e migração',FINANCIAL_AWARD_IDEMPOTENCE:'PASS — ledger, títulos e saldo preservados após repetição/reinício',WEB_BROWSER:visual?'PASS':'BLOCKED — uso do navegador local desativado nas configurações do Work',DATABASE_PERSISTENCE:'PASS — SQLite real, backup008 somente leitura e reinício do processo',REGRESSIONS:'PASS — carreira008, mercado, economia, estádio, pausa, substituições, competições e multiplayer',KNOWN_ISSUES:visual?'Lacunas de dados antigos documentadas.':'Validação visual e capturas pendentes; lacunas históricas documentadas.'};
const originalStatus={SCORER_GENERAL:'PASS',SCORER_DIVISION_1:'PASS',SCORER_DIVISION_2:'PASS',SCORER_DIVISION_3:'PASS',SCORER_DIVISION_4:'PASS',SCORER_CUP:'PASS',GOALKEEPER_GENERAL:'PASS',GOALKEEPER_DIVISIONS:'PASS',GOALKEEPER_MIN_20_MATCHES:'PASS',GOALKEEPER_AVERAGE:'PASS',DEFENDER_GENERAL:'PASS',DEFENDER_DIVISIONS:'PASS',DEFENDER_MIN_20_MATCHES:'PASS',DEFENDER_DISCIPLINE_INDEX:'PASS',STATISTICS_PERSISTENCE:'PASS',CLUB_TRANSFER_HISTORY:'PASS',SEASON_HISTORY:'PASS',NO_DUPLICATE_STATISTICS:'PASS',WEB_INTERFACE:visual?'PASS':'PARTIAL — Flutter28 e Web build aprovados; navegador bloqueado',ANDROID_RESPONSIVE_LAYOUT:'PASS em teste Flutter390px; runtime Android não investigado',DOTNET_TESTS:'237 PASS',FLUTTER_TESTS:'28 PASS',REGRESSIONS:'PASS',SEASON_TOP_SCORER:'PASS',MULTIPLE_WINNERS:'PASS',SAME_CLUB_MULTIPLE_AWARDS:'PASS',PRIZE_AMOUNT:'3.000.000 por vencedor com clube',FINANCIAL_INTEGRITY:'PASS',PLAYER_AWARD_HISTORY:'PASS',PLAYER_DETAIL_SCREEN:'PASS em testes Flutter; visual real pendente',SEASON_END_SCREEN:'PASS em testes Flutter; visual real pendente'};
statuses.WEB_BROWSER=visual?'PASS':'BLOCKED — ferramenta nega acesso, apesar da ativação informada pelo usuário';
statuses.KNOWN_ISSUES=visual?'Lacunas históricas documentadas.':'Navegador real pendente; lacunas históricas documentadas. Renderização Flutter complementar não substitui Browser Use.';
if(hasLocalRender){
  statuses.FLUTTER_TESTS='PASS — 28 regressões + 2 testes separados com dados reais capturados';
  statuses.LOCAL_FLUTTER_RENDER='PASS — 12 imagens; transporte HTTP reproduzido, sem dados esportivos inventados';
  originalStatus.PLAYER_DETAIL_SCREEN='PASS em renderização Flutter local com dados reais; navegador BLOCKED';
  originalStatus.SEASON_END_SCREEN='PASS em renderização Flutter local com dados reais; navegador BLOCKED';
}
const newFiles=['src/Elifoot.Contracts/PlayerRankingsDtos.cs','src/Elifoot.Domain/PlayerStatisticsEntities.cs','src/Elifoot.Domain/PlayerStatisticsEngine.cs','src/Elifoot.Application/PlayerRankingsApplicationService.cs','src/Elifoot.Api/PlayerRankingsEndpoints.cs','client/elifoot_flutter/lib/player_rankings.dart','client/elifoot_flutter/test/player_rankings_api_test.dart','client/elifoot_flutter/test/player_rankings_widget_test.dart','tests/Elifoot.Domain.Tests/PlayerRankings009Tests.cs','tests/Elifoot.Integration.Tests/PlayerRankings009IntegrationTests.cs','tests/Elifoot.Simulation.Tests/PlayerRankings009SimulationTests.cs','scripts/verify-player-rankings-009.mjs','scripts/report-player-rankings-009.mjs'];
newFiles.push('scripts/capture-player-ui-data-009.mjs','client/elifoot_flutter/tool/qa/player_rankings_live_009_test.dart');
const links='[Armazenamento](PLAYER_STATISTICS_STORAGE_009.md) · [Testes](PLAYER_RANKINGS_TESTS_009.md) · [Migração](PLAYER_RANKINGS_MIGRATION_009.md) · [Premiação](SEASON_TOP_SCORER_AWARDS_009.md) · [Recuperação](PLAYER_RANKINGS_RECOVERY_009.md)';
write('PLAYER_RANKINGS_009.md',`
# Rankings históricos de jogadores 009

30/10/2026, America/Sao_Paulo. **STATUS: ${overall}**.

Implementação e regressões aprovadas: **237 testes .NET e 28 Flutter**, build Release, Flutter analyze e build Web. A temporada real produziu 351 partidas, 32.520 minutos e 725 gols atribuídos, conferidos após cada uma das 22 rodadas. ${visual?'Interface conferida no navegador real.':'A etapa obrigatória de navegador real está bloqueada pela ferramenta; não há PASS nem captura de navegador. A alternativa de renderização Flutter local está documentada ao final.'}

${links}

## Critérios

Geral soma as quatro ligas e todas as fases oficiais da Taça, incluindo preliminares efetivamente disputadas e prorrogações. Cada divisão considera somente a divisão persistida na fixture, nunca a divisão atual do atleta ou clube. Taça considera somente CompetitionType diferente de League. Shootout não gera gols de artilharia ou sofridos; pênalti durante o jogo é Goal e conta. Bye sem minuto disputado não dá participação nem estatística.

${table(['Categoria','Principal','Desempates','Mínimo no recorte'],[
['Artilheiros','Mais gols','Maior gols/jogos, depois PlayerId','Nenhum; lista principal contém quem marcou'],
['Goleiros (posição G no registro)','Menos sofridos/jogos','Mais jogos; menos sofridos; PlayerId','20 jogos'],
['Defensores (posição D no registro)','Menor (amarelos+3×vermelhos)/jogos','Menos vermelhos/jogos; menos amarelos/jogos; mais jogos; PlayerId','20 jogos']])}

Os quocientes são comparados por produtos cruzados BigInteger, sem arredondar antes da ordenação. DTO usa decimal e UI mostra três casas; taxas diferentes podem aparecer iguais visualmente, preservando ordem correta. Com dados legados de participação incompleta, a média fica nula e G/D não entram na classificação principal. Contagem só da posição registrada naquele jogo, sem reclassificação retroativa se o cadastro for editado.

Entrou aos60: uma participação,30 minutos em jogo de90 e só gols posteriores. Saiu aos60:60 minutos e nenhum gol posterior. Entrada aos90 conta participação com zero minutos. Mesmo minuto respeita a ordem original de eventos: gol antes da troca pertence ao goleiro anterior; depois da troca ao novo. Goleiro expulso deixa de sofrer novos gols; eventual improvisado é acompanhado, sem promover sua posição cadastral a G. Banco que não entrou recebe zero.

Amarelo vale1 e vermelho3 somente neste ranking. Segundo amarelo conta mais um amarelo e um vermelho, além do primeiro amarelo; direto conta um vermelho. Não se alteraram faltas, suspensões ou probabilidades esportivas.

## Fonte e períodos

Eventos oficiais de partidas concluídas e captura inicial de jogadores/clubes/posições são a fonte. Consolidação não usa os contadores transitórios SeasonGoals/CupGoals/CareerMatches. O motor antigo incrementa SeasonGoals para todo gol, mais CupGoals na Taça; somar os dois duplicava a Taça. A rota compatível top-scorers agora deriva da estatística oficial e retorna liga/Taça separadamente, sem dupla soma. A evolução esportiva anterior que usa contadores foi preservada.

Períodos: carreira inteira, temporada atual e temporada específica. Transferência, promoção/rebaixamento, demissão de técnico, renovação e virada de temporada não apagam o histórico. O clube exibido na tabela é o atual; buckets históricos guardam o clube na partida. Perfil mostra posição, força atual, comportamento, estrela literal *, totais, divisão/Taça, temporadas e conquistas individuais.

## Execução real

Seed199801,128 clubes,2.308 jogadores no mundo (inclui livres),351 partidas e32.520 minutos, todos pelo motor real minuto a minuto. Estatísticas reconciliadas com os eventos após cada rodada; repetição não alterou os agregados. Cada consulta HTTP da instância privada foi comparada ao snapshot SQLite: **18/18 combinações categoria×competição concordaram**. [Evidência persistida](audit-009/api-persisted-validation.json).

${table(['Recorte T1','Artilheiros com gol','Goleiros elegíveis','Defensores elegíveis'],season.Rankings.map(r=>[r.Scope,r.Scorers.Rows.length,r.Goalkeepers.Rows.length,r.Defenders.Rows.length]))}

Cada divisão tem14 partidas de liga por temporada no formato atual. Portanto, em uma única temporada, nenhum G/D alcança20 exclusivamente naquela divisão; a seção principal fica vazia de forma correta. Carreira acumulada em duas temporadas pode qualificar. Os testes exercitam exatamente19/20 e soma entre clubes/fases dentro do mesmo recorte. Dados artificiais usados para esses limites e carga1024 estão identificados como fixtures de teste, sem alegação de temporada real1024.

Artilheiros da temporada real:

${awardsTable}

Ambos ganharam apesar de jogos diferentes (20/17); a ordenação da tabela pode usar média, o título não. Cada clube recebeu3milhões. Após reiniciar o processo da API, IDs, valores e histórico permaneceram iguais: [reinício](audit-009/process-restart-validation.json).

## Interface e bloqueio visual

Menu 9. RANKINGS DE JOGADORES, três categorias, seis recortes, três períodos e seleção de temporada. Tabelas clássicas verdes com rolagem horizontal, seção AINDA NÃO ELEGÍVEIS, perfil clicável e ARTILHEIRO: X VEZES. Painel de premiação por temporada e diálogo automático ao detectar virada de temporada, com total por clube.

28 testes Flutter incluem filtros, erros/retry sem falso vazio, cinco conquistas e texto exato ARTILHEIRO: 5 VEZES, empates/prêmio6milhões, preservação do menu008 e largura390px. Esses testes usam HTTP mockado; a API real foi validada separadamente por HTTP/SQLite. **Isso não substitui inspeção no navegador real.** A tentativa de abrir a instância retornou: Local browser use is disabled in Work settings, mesmo após a ativação informada pelo usuário. [Registro](audit-009/browser-validation.json). Capturas de navegador: **não disponíveis**. As imagens adicionais do renderizador Flutter local, com dados reais capturados, estão identificadas como alternativa e não como sessão Web.

DEV privado preparado: http://127.0.0.1:8089/ e API http://127.0.0.1:5089/. Banco em browser-used-fixture.json. Esta instância usa a temporada real anteriormente preservada, já encerrada e avançada paraT2. A versão de origem e o hash estão no manifesto congelado; a regressão posterior gerou outro arquivo real-season-world com IDs novos, sem substituir o banco já utilizado na verificação. Não houve investigação Android nem publicação.

## Preservação e alterações

Sem repositório Git neste diretório; git rev-parse confirmou a ausência. Baseline009 preserva58 arquivos e SHA256; a recuperação fez novo backup de todo o código atual antes de editar. Os18 arquivos008 conferidos no início coincidiram com o manifesto008. CoachCareerEngine, AiManagerBrain, MarketAndEconomyEngine, TacticsAndSquadRules e Enums permanecem byte a byte iguais ao baseline009. A fórmula de ranking de treinadores e o bloco de moral, convites e tabela da tela008 foram comparados por seção. A tela recebeu ao final um painel adicional de prêmios de jogadores.

MatchEngine recebeu somente **duas chamadas CaptureLineup** (antes dos ticks e antes dos ajustes), sem RNG ou mutação esportiva. Removendo essas duas linhas, o texto é igual ao baseline após normalizar CRLF. Não foram reescritos defesa, gols, disciplina, mercado, estádio ou pausa. O prêmio novo muda caixa e pode influenciar trajetórias futuras de mercado/convites; a lógica008 permanece igual e todas as regressões passaram. A assinatura das50 temporadas não precisa coincidir com008 porque a economia ganhou o prêmio solicitado.

Arquivos existentes alterados:

${changes.map(c=>'- '+c.Path).join('\n')}

Novos arquivos de código/testes/scripts:

${newFiles.map(f=>'- '+f).join('\n')}

[Prova de preservação](audit-009/preservation.json), [diff](audit-009/source-diff.patch), [manifesto final](audit-009/evidence-manifest.json). Sete documentos009, incluindo a validação local complementar, e evidências completam a entrega.

## Relatório da tarefa original e adendo

${table(['Campo','Resultado'],Object.entries(originalStatus))}

STATUS geral: **${overall}**. Limitação impeditiva do PASS geral: ${visual?'nenhuma.':'navegador real desabilitado; capturas e conferência visual pendentes.'} Limites de legado estão documentados em Migração; não há pacote NuGet ou DLL bloqueando os testes finais.
`);
write('PLAYER_STATISTICS_STORAGE_009.md',`
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
`);
write('PLAYER_RANKINGS_MIGRATION_009.md',`
# Migração de saves 009

30/10/2026. STATUS de migração: PASS nos testes executados. Sem DDL, sem reinstalação de SDK, sem reset de carreira008.

## Abertura

Os três caminhos EF (GetWorldAsync, GetWorldByInviteCodeAsync e ListWorldsAsync) mantêm CoachCareerEngine.EnsureMigrated e depois chamam PlayerStatisticsEngine.EnsureMigrated. Campo ausente PlayerStatisticsVersion inicia0; mundo novo nasce9. Migração é versionada e repetível sem novos créditos. Snapshot migrado é gravado no próximo checkpoint normal, sem escrita surpresa em GET.

Preservam-se PlayerId, ClubId, moral, empregos, convites, ranking008, pontos decimal, títulos de treinadores, saldos e ledger. O teste usa backup SQLite consistente pela API BackupDatabase com o save008 aberto somente leitura, testa os três caminhos de carga e compara carreira, dinheiro e snapshotSQL original. O banco original008 não foi atualizado pelo teste. [Auditoria somente leitura](audit-009/sqlite008-readonly-audit.json).

## O que é recuperável

Eventos Goal/cartões/substituições de partidas concluídas conservadas. Titulares iniciais podem ser reconstruídos de ativos finais + substituídos para fora + expulsos − substituídos para dentro. Só se ambas as listas tiverem11 IDs conhecidos e os eventos necessários de entrada/expulsão existirem. Banco/entradas vêm das listas e eventos. Minuto/ordem original permitem atribuição de sofridos ao goleiro daquele momento.

Snapshots antigos não guardavam posição/força no início; nesses casos usam-se os dados disponíveis na migração, com aviso explícito. Esses campos não podem ser reconstruídos exatamente após uma edição de posição ou força. O cálculo estatístico não inventa força passada nem afeta o jogo.

Se não há escalação suficiente, recupera apenas fatos individuais presentes nos eventos. Jogos correspondem a participações comprovadas, mas podem ser parciais; minutos desconhecidos não são inventados. Médias nulas e G/D inelegíveis sinalizam a lacuna. Sem eventos de gol compatíveis com placar, não se distribui o placar a atletas/goleiro. Gol com evento e minuto válidos, mas sem autor, conserva o placar e sofrido verificável; não cria artilheiro.

## O que não é reconstruído

- Fixtures/eventos podados antes da009: carreira anterior fica parcial.
- Contadores CareerGoals/CareerMatches sem origem por jogo: não são repartidos artificialmente por temporada/divisão.
- Premiações passadas: fechamentos antigos são marcados encerrados e não recebem títulos/pagamentos retroativos sem prova. Históricos antigos de um único nome de artilheiro não estabelecem todos os empatados nem clube correto no fechamento.
- Temporada atual incompleta por eventos ausentes: prêmio automático suspenso e aviso, sem recebedor inventado.

O ranking mostra fatos recuperados; não promete reproduzir integralmente toda carreira anterior. Os contadores legados são mantidos para compatibilidade esportiva. A rota antiga top-scorers agora evita somar a Taça duas vezes, derivando de registros oficiais disponíveis.

## Proteção de dados e procedimento

Faça backup consistente do SQLite (incluindo transações WAL pelo mecanismo SQLite) ou dump PostgreSQL antes de abrir um save importante. Não copie isoladamente arquivo .db em uso. Abra com a API009, consulte coverageWarnings e confirme as limitações de cobertura. Não use exportação do editor como backup de carreira.

Guardas e estatísticas persistidas sobrevivem ao reset sazonal e poda antiga. Atualizações seguintes não voltam a executar a migração9. Recarregar não duplica a partida e chamar AwardSeason de temporada já fechada não altera dinheiro/título. Testes com clube atual diferente do clube histórico e ID estável conferem isso.

Na recuperação,129 testes de jogador,29 de integração no total e reinício real da API comprovaram as rotas relevantes. A verificação visual permanece ${visual?'aprovada.':'bloqueada nas configurações do Work; isso não foi substituído por uma captura artificial.'}
`);
write('SEASON_TOP_SCORER_AWARDS_009.md',`
# Artilheiro da temporada e prêmio 009

30/10/2026. Cálculo, pagamento e persistência: PASS. Conferência visual real: ${visual?'PASS':'BLOCKED (Work desabilitou navegador local)'}.

## Regra

Somar GoalsScored de todas as ligas e fases oficiais da Taça na temporada, inclusive preliminares e prorrogação. Transferência não reinicia o total. Shootout não conta. Encerramento só avalia a temporada atual na rodada final, com fixtures existentes todos concluídos, consolidação feita e sem sinal de estatísticas incompletas. Totais zero não geram conquista nem prêmio.

Todos empatados no máximo de gols ganham. Jogos, média, força e PlayerId não retiram título de um empatado; ID serve apenas à ordem estável da apresentação. O histórico clássico de temporada, que antes tinha um nome, agora apresenta todos os vencedores e o total oficial sem dupla contagem da Taça.

Cada vencedor com clube existente no momento do fechamento gera **3.000.000**, categoria PrizeMoney. Clube de fechamento, não o clube onde marcou, recebe o valor. Dois do mesmo clube geram6milhões; três9milhões. Não se divide o prêmio. Sem clube, o jogador ganha título e registro individual com ClubId nulo/AwardAmount0 e motivo, sem crédito nem destinatário fictício.

## Idempotência

Chave: season_top_scorer:{WorldId}:{Season}:{PlayerId}. AwardId determinístico derivado da chave. Antes do pagamento, verifica registro individual; o ledger usa a chave pela rotina idempotente de economia existente. ClosedPlayerAwardSeasons impede reabrir a temporada, inclusive zero gols. Registro, contador, caixa e ledger são persistidos no checkpoint de encerramento.

Registros imutáveis: PlayerId,DisplayName,Season,TotalGoals,TotalMatches,ClubId,ClubNameAtClose,AwardAmount,AwardGrantedAt,AwardId,IdempotencyKey. Renomear, vender, expirar contrato ou avançar temporada não reescreve clube/nome/pagamento históricos. SeasonTopScorerTitles soma conquistas individuais; testes de cinco temporadas verificam exatamente cinco, sem duplicação.

## Temporada real executada

${awardsTable}

725 gols individuais,351 partidas,32.520 minutos,22 rodadas. Os dois empataram em10 gols com20/17 jogos; ambos receberam título. Ledger de duas entradas3milhões;6milhões total. Os deltas de caixa de todos os clubes foram reconciliados com todas as transações novas do encerramento, além de verificar isoladamente as entradas de prêmio.

[Temporada real](audit-009/REAL_SEASON_009.json) e [validação HTTP versus SQLite](audit-009/api-persisted-validation.json). Testes determinísticos cobrem campeão único, dois/três/cinco empatados, mesmo clube, clubes diferentes, livre, clube alterado antes de fechar, zero gols, repetição/reload e guarda do ledger. Fixtures artificiais são identificadas nos testes e não apresentadas como resultados da temporada real.

Reinício real da API: PID ${restart.OldPid}→${restart.NewPid}; IDs, valores e total6milhões iguais após reinício. [Prova](audit-009/process-restart-validation.json), [antes](audit-009/awards-before-process-restart.json), [depois](audit-009/awards-after-process-restart.json).

## Interface

Perfil: ARTILHEIRO: X VEZES, cada temporada/gols/jogos/clube/prêmio. Encerramento: todos os vencedores, premiação individual e total por clube, em tabela verde clássica. O diálogo automático aparece uma vez ao detectar avanço de temporada na sessão; o painel permite consultar encerramentos após reconexão, sem repetir pagamento.

Testes Flutter verificam ARTILHEIRO: 5 VEZES, as cinco conquistas e dois campeões do mesmo clube com total6milhões. A inspeção no navegador ficou ${visual?'aprovada.':'pendente porque a ferramenta continua negando Browser Use. Há uma alternativa de renderização Flutter local documentada ao final, sem captura de navegador.'}

Limitação: temporada com autor/eventos ausentes pode não ter máximo individual comprovável; não se premia automaticamente esse conjunto. Saves antigos não recebem títulos/prêmios retroativos inventados. [Migração](PLAYER_RANKINGS_MIGRATION_009.md).
`);
const coverage=[
['Artilheiros e recortes','Scopes_KeepEachDivisionAndCupIndependent; EveryOfficialCupPhase_ContributesOnceToCupAndGeneral; Scorers_TotalGoalsPrecedeAverage_ThenAveragePrecedesStableId'],
['Gols oficiais/extra/shootout','RegulationAndExtraTimeGoals_Count_ShootoutAttemptsNeverCount; OfficialGoalWithVerifiedAuthor_IncludingPenaltyAndExtraTime_IsCountedExactly'],
['Sem autor/contra/associação','OwnGoalAndUnknownScorer_ChargeActiveGoalkeeperWithoutInventingIndividualGoal; UnattributableOfficialGoal_PreservesScoreAndKnownKeeperWithoutInventingScorer; UnknownScorer_DoesNotDisqualifyKeeperWithTwentyVerifiedAppearances'],
['Goleiros/tempo real','GoalkeeperSubstitution_AssignsOnlyGoalsConcededWhileEachKeeperWasActive; SameMinuteEvents_UseOriginalOrder; UnusedKeeper_NeverReceivesAppearanceOrGoalsConceded; SentOffGoalkeeper_DoesNotReceiveLaterGoals'],
['20 por recorte/posição','Eligibility_ChangesExactlyAt20Matches; Eligibility_IsComputedWithinDivisionCupAndSeason; TwentiethFinishedAppearance_QualifiesWithinEachScopeAcrossStatisticalBuckets; OnlyMatchesPlayedInCategoryPosition_CountTowardItsMinimum'],
['Disciplina e exatidão','Dismissals_CountRealCardsAndStopMinutes; Defenders_DisciplineScorePrecedesRedRate_AndUsesActualCardCounts; RationalAverageComparison_DistinguishesValuesThatRoundToSameDisplay; Defenders_RedRateTieBreakUsesExactRationalComparison'],
['Transferência/divisão/temporada','TransferAfterKickoffAndBetweenGames_PreservesOriginalClubAndDivisionBuckets; PromotionAndRelegation_NeverReclassifyHistoricalCompetition; SeasonFilters_PreserveCareerAndSelectOnlyRequestedSeason; NewSeasonInitialization_ResetsLegacyCountersButRetainsRankingHistory'],
['Replay/reconexão/reinício','MatchAndPlayerGuards_PreventDuplicateStatisticsAcrossRetries; TwoDifferentMatches_AccumulateIntoSameBucketAfterJsonReload; SqliteReloadPreservesExactAwardsTitlesLedgerAndMatchIdempotency; process-restart-validation.json'],
['Migração e lacunas','LegacyRecovery_ReconstructsStartingLineupAndSubstitutionTimingFromFinalLists; LegacyMissingLineup_RecoversOnlyIndividualFactsWithoutInventingAppearancesOrAverages; Existing008DatabaseMigratesFromConsistentReadOnlyBackupWithoutChangingCareerOrLedger'],
['Prêmios e empates','SeasonAwards_AllTiedGoalsWinRegardlessOfMatchesOrAverage; EachWinner_CreditsFullPrizeToClosingClub_AndClubTotalsAccumulate; FreeAgentWinner_KeepsTitleWithZeroAwardWithoutFictitiousClubOrLedgerCredit; FiveDistinctSeasonTitles_PersistWithCorrectPerSeasonHistoryAndNoDuplicateTitles'],
['Ledger/sem dupla premiação','AwardLedger_RecordsExactAmountBalanceSeasonAndWorldScopedIdempotencyKey; RepeatedSeasonClose_IsIdempotentAcrossJsonRestart; AwardRecordGuard_ProtectsTitleAndPaymentWhenSeasonCloseMarkerIsMissing; AwardLedgerGuard_PreventsDuplicateCashWhenAwardRecordNeedsRecovery'],
['API/concorrência','HttpFiltersCoverAllCategoriesScopesAndPeriodsUsingPersistedStatistics; QueryServiceSharesTheClockLockAndReleasesItAfterCancellationOrFailure; HttpHistoryPreservesHistoricalClubsAndGetRequestsDoNotPayAwardsOrWriteSnapshots'],
['128/1024 e temporada real','PlayerRankings009_QueryLoad_128And1024Clubs; HttpRankingPerformanceIsMeasuredAfterColdSqliteReload; PlayerRankings009_RealFullSeason_ReconcilesEveryRoundAndPaysExactAwards'],
['Flutter','Filtros/rotas reais de contrato via HTTP mockado; inelegíveis; histórico5; prêmios6milhões; erros/retry;390px; menu008; diálogo de encerramento uma vez.'],
['Navegador real',visual?'PASS': 'BLOCKED; não substituído por testes mockados ou capturas geradas.']];
write('PLAYER_RANKINGS_TESTS_009.md',`
# Testes da tarefa 009

30/10/2026. **STATUS: ${overall}** pela etapa visual. Todos os testes automáticos finais executados passaram.

${countsTable}

.NET SDK10.0.302, Flutter3.38.5/Dart3.10.4, Node24.20.0. Build Release:0 erros/0 avisos. Flutter analyze sem apontamentos; buildWeb com saída absoluta aprovado. Regressão completa: [log](audit-009/final-tests.log). XMLs: [domínio195](audit-009/final-tests/domain195.trx), [aplicação5](audit-009/final-tests/application5.trx), [integração29](audit-009/final-tests/integration29.trx), [simulação8](audit-009/final-tests/simulation8.trx).

O primeiro logger da solução usou o mesmo nomeTRX nas quatro suítes e sobrescreveu o arquivo. O log preserva os quatro resultados. Foram conservados os XMLs de integração/simulação antes/depois da sobrescrita e repetidos domínio/aplicação para gerar XMLs próprios. Isso não soma casos repetidos aos237. A compilação real foi executada antes e durante a rodada completa; --no-build nas duas repetições não foi usado como substituto de executar testes.

129 casos específicos de jogador009 +11 integração009 +3 simulação009 somam143 novos aos94.NET008, total237. Flutter tem18 novos além dos10 anteriores, total28. Theories expandem parâmetros. Logs originais de109/122 casos de desenvolvimento foram preservados; a recuperação terminou com129 jogadores e195 domínio geral.

## Cobertura dos requisitos

${table(['Requisito','Evidência/testes'],coverage)}

## Temporada real e regressões

O teste PlayerRankings009_RealFullSeason_ReconcilesEveryRoundAndPaysExactAwards executou351 partidas pelo motor real,32520 minutos e725 gols individuais, em22 rodadas. A cada rodada confrontou gols/cartões/sofridos com eventos, validou seis recortes de três categorias e reprocessou sem duplicar. Encerramento produziu dois títulos e6milhões; todos os deltas de caixa conciliados. [JSON](audit-009/REAL_SEASON_009.json).

A suíte completa manteve as regressões de mercado/economia/estádio/disciplina/tática,100 temporadas, matriz de confrontos, oito humanos,API/SignalR2/4/6/8 clientes,pausa/substituições/reconexão e PostgreSQL18. A carreira008 voltou a executar50 temporadas reais: ${career.Matches} partidas,${career.Minutes} minutos, replay determinístico. Artefatos ficam em audit-009/final-regression, preservando relatórios005/006/007/008. A nova premiação altera caixa, portanto a assinatura de carreira difere da008 sem mudança na fórmula do ranking de treinadores.

Consultas128/1024 passaram tanto em domínio quanto em HTTP após SQLitefrio. São fixtures sintéticas de carga; a temporada esportiva real é128. Nenhuma temporada1024 foi alegada.

## Recuperação dos bloqueios

DLLs: API DEV4640 na5088 identificada por caminho/comando e encerrada, sem tocar outros projetos. Os novos binários deQA são copiados para artifacts/player-rankings-009/api, evitando bloquear build futuro. NuGet: assets apontavam cacheDaniel, enquanto a tentativa isolada usou perfilCodexSandboxOffline.212 referências verificadas presentes, fontes lidas e serviçoNuGet retornouHTTP200. Sem reinstalação; --no-restore tecnicamente válido.

Erro original de gol sem autor: teste antigo esperava zero avisos. Ao retomar, havia ajuste parcial nas asserções;122 casos009 já passavam. A investigação confirmou mistura entre ausência de autoria e ausência de participação, além de poder creditar PlayerId conhecido fora do campo/clube. Corrigido em produção e coberto por mais sete casos. [Detalhes](PLAYER_RANKINGS_RECOVERY_009.md).

## Reprodução

PowerShell em D:\\elifoot_98. Use pasta nova de execução para preservar evidências:

~~~powershell
$run009 = Join-Path (Get-Location) ('artifacts\\player-rankings-009\\rerun-' + (Get-Date -Format yyyyMMdd-HHmmss))
New-Item -ItemType Directory -Path $run009 -Force | Out-Null
$env:ELIFOOT_AUDIT_OUTPUT = $run009
dotnet build Elifoot.slnx -c Release --no-restore
dotnet test Elifoot.slnx -c Release --no-restore --logger 'trx;LogFilePrefix=rerun009' --results-directory $run009
Push-Location client\\elifoot_flutter
flutter analyze
flutter test
flutter build web --release --dart-define=API_BASE_URL=http://127.0.0.1:5089 --output=D:\\elifoot_98\\artifacts\\player-rankings-009\\web
Pop-Location
node scripts/verify-player-rankings-009.mjs
node scripts/report-player-rankings-009.mjs
~~~

Use --no-restore se project.assets.json e pacotes existirem, como nesta máquina; caso contrário, restaurar dependências antes. Não forçar exclusão de DLLs; identificar e parar somente API própria ou usar diretório de saída isolado. Verificar conflitos de porta antes de iniciar API.

## Procedimento visual pendente

Habilitar uso do navegador local nas configurações do Work. Abrir http://127.0.0.1:8089/, usar apelido **Rankings 009** e dispositivo **qa009-web**, entrar na sala RANK009 como desempregado e abrir **9. RANKINGS DE JOGADORES**. Conferir geral, quatro divisões, Taça, goleiros, defensores, período T1 e carreira; perfil de Mateus Alves* e Thiago Machado*, uma conquista e prêmio de 3 milhões para cada clube, total de 6 milhões. A temporada atual T2 começa vazia. Testar 390px e rolagem; G/D por divisão em T1 devem estar inelegíveis pois só há 14 jogos de liga.

Comparar com api-persisted-validation.json e registrar capturas reais. Para observar o diálogo automático, concluir uma temporada na sessão; painel de prêmios já mostra encerramentoT1. Não inserir títulos fictícios nesse banco para ilustrar cinco conquistas: o caso exato5 foi verificado por testeFlutter determinístico. Só então atualizar browser-validation.json e regenerar relatórios para possível PASS geral.

Estado dos processos/banco: [dev-session.json](audit-009/dev-session.json), [fixture usada](audit-009/browser-used-fixture.json). O SQLite vivo e logs de processo são mutáveis; provas estáticas são JSONs de validação, TRX e manifesto. Capturas reais009 indisponíveis devido ao bloqueio descrito.
`);
write('PLAYER_RANKINGS_RECOVERY_009.md',`
# Recuperação da tarefa 009

30/10/2026, America/Sao_Paulo. **STATUS: ${overall}**. Preservados código e evidências anteriores; sem reiniciar implementação, publicar ou modificar outros projetos.

## Causa e correção do caso de gol sem autor

Falha original: PlayerRankings009Tests.OwnGoalAndUnknownScorer_ChargeActiveGoalkeeperWithoutInventingIndividualGoal(ownGoal:false), Assert.Empty sobre CoverageWarnings. O evento tinha Goal,clube,minuto e placar válido,mas PrimaryPlayerId ausente. O motor gerador normal sempre associa scorer.Id; não foi encontrada falta de autor no fluxo esportivo novo. Não se mudou placar ou probabilidade para satisfazer teste.

Na retomada, o arquivo de teste já continha um ajuste parcial de expectativa e122 casos específicos passaram. A análise adicional identificou um defeito: o consolidador marcava toda participação como incompleta quando só a autoria era desconhecida, tirando desnecessariamente goleiro conhecido do ranking. Também incrementava gol antes de validar vínculo do autor ao clube/campo.

Correção: HasIncompleteGoals separado de HasIncompleteAppearances; artilharia só recebe Goal com autor verificável no clube/campo (ou fato individual recuperável quando escalação legada falta). Gol sem autor conserva placar e sofrido do goleiro ativo,emite aviso e não inventa jogador. Gol contra nunca credita artilheiro;shootout excluído. Pênalti em jogo e prorrogação contam. Se faltam eventos de gol compatíveis com placar,não se inventam minutos/autores/sofridos. Temporada de artilharia incompleta não gera prêmio automático.

Cobertura nova: IDnulo,IDinexistente,jogador no banco,IDdo adversário,goleiro com20 partidas verificadas e autoria ausente,pênalti em jogo e prorrogação.129 casos009 passaram;195 domínio total. [Log](audit-009/recovery-domain-goals.log).

## DLLs e NuGet

Identificado PID4640: dotnet D:\\elifoot_98\\src\\Elifoot.Api\\bin\\Release\\net10.0\\Elifoot.Api.dll --urls http://127.0.0.1:5088. Encerrado somente esse processoDEV, após verificar comando/caminho. NenhumaDLL apagada ou processo externo encerrado. Runtime009 agora tem cópia isolada de binários em artifacts/player-rankings-009/api. [Processo identificado](audit-009/recovery-stopped-process.json).

SDK10.0.302 existente. CacheDaniel possui os pacotes apontados pelos assets;212 referências verificadas e zero ausentes. Tentativa anterior de saída isolada rodou no perfilCodexSandboxOffline,cujo cache era diferente,e a rede não restaurou os pacotes. No contexto de recuperação,NuGet respondeu200. Compilação e execução reais com--no-restore concluíram. Não foi necessário reinstalar SDK nem depender de baixar pacote. [Dependências](audit-009/recovery-dependency-check.json),[rede/cache](audit-009/nuget-network-check.json).

Quatro falhas iniciais de integração foram isoladas: fixture de prêmio conservava128 lançamentos InitialBalance,mas asserção esperava só os1/2 prêmios;e migração008 precisava expor que posição/força iniciais não existiam no snapshotantigo. Fixture agora isola o ledger sob teste; migração emite aviso honesto. Carga também exclui atletas sem clube da geração de dados artificiais ligados a clube. As29 integrações finais passaram sem alterar valores esportivos para mascarar erros.

BuildWeb inicial falhou ao escrever shader em caminho relativo. Caminho absoluto resolveu,sem mudar Flutter/engine. [Extrato da falha](audit-009/web-build-relative-failure-excerpt.txt),[build aprovado](audit-009/web-build.log).

## Validação final

${countsTable}

Temporada real preservada:351 jogos,32520minutos,725gols,22rodadas.18 consultas comparadas aoSQLite. Dois artilheiros10gols com20/17jogos receberam títulos e3milhões cada;reinício real manteveIDs/valores. Goleiros/defensores com20,no recorte correto,ordenados por razões exatas. Carreira008/mercado/economia/estádio/pausa/multiplayer regressaram aprovados.

## Bloqueio externo restante

${visual?'Navegador real validado.':'Ao abrir a instância no navegador,a ferramenta retornou “Local browser use is disabled in Work settings”. Usuário avisado para habilitar. Não se tentou contornar essa configuração por outra automação. Teste visual e capturas reais continuam BLOCKED; API/testesFlutter não foram apresentados como PASS visual.'}

Aplicação privada pronta em http://127.0.0.1:8089/,API5089;dados reais preservados e comando de verificação em scripts/verify-player-rankings-009.mjs. [Procedimento](PLAYER_RANKINGS_TESTS_009.md). Nenhum bloqueio de DLL/NuGet/teste automático permanece.

## Relatório final de recuperação

${table(['Campo','Resultado'],Object.entries(statuses))}
`);
fs.writeFileSync(path.join(audit,'preservation.json'),JSON.stringify({BaselineFiles:baseline.length,ProtectedFiles:protectedFiles,ProtectedSections:sections,MatchEngineChanges:'Exactly two observational CaptureLineup calls; remaining normalized source identical.',ExistingFilesChanged:changes,NewCodeTestScriptFiles:newFiles},null,2)+'\n');
let diff='';for(const c of changes){const r=spawnSync('git',['diff','--no-index','--no-ext-diff','--',path.join(audit,'baseline',c.Path),path.join(root,c.Path)],{encoding:'utf8',maxBuffer:15*1024*1024});assert.ok(r.status===0||r.status===1,r.stderr);diff+=r.stdout;}
fs.writeFileSync(path.join(audit,'source-diff.patch'),diff);
const evidence=[];
function walk(dir){for(const e of fs.readdirSync(dir,{withFileTypes:true})){const p=path.join(dir,e.name);if(e.isDirectory()){if(!['baseline','recovery-baseline','sqlite','dotnet-artifacts','final-regression'].includes(e.name))walk(p);}else if(!/^(evidence-manifest\.json|dev-session\.json|browser-fixture\.json|api-(live|restarted)\.(stdout|stderr)\.log|web-live\.(stdout|stderr)\.log)$/.test(e.name))evidence.push(p);}}
walk(audit);
for(const f of [...changes.map(c=>c.Path),...newFiles,...['PLAYER_RANKINGS_009.md','PLAYER_STATISTICS_STORAGE_009.md','PLAYER_RANKINGS_TESTS_009.md','PLAYER_RANKINGS_MIGRATION_009.md','SEASON_TOP_SCORER_AWARDS_009.md','PLAYER_RANKINGS_RECOVERY_009.md','PLAYER_RANKINGS_LOCAL_VALIDATION_009.md'].map(f=>'docs/'+f)])evidence.push(path.join(root,f));
const manifest={Task:'ELIFOOT_PLAYER_RANKINGS_009_RECOVERY',GeneratedUtc:new Date().toISOString(),Status:overall,Results:statuses,OriginalTaskAndAddendum:originalStatus,DotnetTests:237,FlutterTests:28,RealSeason:{Matches:351,Minutes:32520,Goals:725},VisualValidation:browser,MutableExclusions:['Runtime process metadata/logs','Live SQLite files','Regenerated browser-fixture.json','Bulk regression/capture artifacts retained separately'],Files:[...new Set(evidence)].sort().map(f=>({Path:slash(path.relative(root,f)),Bytes:fs.statSync(f).size,SHA256:hash(fs.readFileSync(f))}))};
manifest.FlutterRealDataRenderingTests=hasLocalRender?2:0;
fs.writeFileSync(path.join(audit,'evidence-manifest.json'),JSON.stringify(manifest,null,2)+'\n');
console.log(JSON.stringify({Status:overall,Dotnet:237,Flutter:28,Visual:browser.status,ExistingChanged:changes.length,NewSourceTestScripts:newFiles.length,ManifestFiles:manifest.Files.length},null,2));
