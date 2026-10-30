import {readFileSync,writeFileSync,readdirSync,existsSync} from 'node:fs';
import {resolve,dirname,join} from 'node:path';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
import {gunzipSync} from 'node:zlib';
import {execFileSync} from 'node:child_process';
import assert from 'node:assert/strict';

const root=resolve(dirname(fileURLToPath(import.meta.url)),'..');
const audit=join(root,'docs/audit-012c-discipline');
const preview=process.argv.includes('--preview');
const output=join(audit,preview?'regression':'verified');
const run=join(output,'classic-force-012c');
const text=p=>readFileSync(p,'utf8').replace(/^\uFEFF/,'');
const read=p=>JSON.parse(text(p));
const hash=p=>createHash('sha256').update(readFileSync(p)).digest('hex').toUpperCase();
const json=(p,v)=>writeFileSync(p,JSON.stringify(v,null,2)+'\n');
const lines=p=>gunzipSync(readFileSync(p)).toString('utf8').trim().split(/\r?\n/).filter(Boolean).map(JSON.parse);
const fmt=n=>Number(n).toLocaleString('pt-BR',{maximumFractionDigits:3});
const table=(headers,rows)=>['| '+headers.join(' | ')+' |','| '+headers.map(()=>'---').join(' | ')+' |',...rows.map(r=>'| '+r.join(' | ')+' |')].join('\n');
const data=read(join(run,'100-seasons.json'));
const cards=lines(join(run,'discipline-cards.jsonl.gz'));
const suspensions=lines(join(run,'discipline-suspensions.jsonl.gz'));
const force=lines(join(run,'force-events.jsonl.gz'));
const injuries=lines(join(run,'injuries.jsonl.gz'));
const sample=read(join(output,'direct-red-100000.json'));
const injurySample=read(join(run,'weighted-duration-sample.json'));
const replay=read(join(run,'replay.json'));
assert.equal(data.SeasonsCompleted,100);assert.equal(data.TotalMatchesCompleted,35100);
assert.equal(data.InvalidLineupSelectionViolations,0);assert.equal(data.DoubleSettledMatchFinancialViolations,0);
assert.equal(data.CupGrowth,0);assert.equal(data.TurnSnapshots,6400);
assert.equal(cards.length,data.DisciplineCardRecords);assert.equal(suspensions.length,data.SuspensionHistoryRecords);
const uniqueCards=new Set(),uniqueSuspensions=new Set(),awards=new Map();
const kinds={},reasons={},directCounts=[0,0,0,0];let served=0,awarded=0;
for(const {Id,Card:c} of cards){
  assert.ok(c.WorldId&&c.MatchId&&Id&&c.ClubId);assert.ok(!uniqueCards.has(c.Key));uniqueCards.add(c.Key);
  assert.equal(c.RecoveredFromLegacy,false);assert.ok(c.Minute>0);
  kinds[c.Card]=(kinds[c.Card]??0)+1;
  if(c.Card==='DirectRedCard'){assert.ok(c.SuspensionMatches>=1&&c.SuspensionMatches<=4);directCounts[c.SuspensionMatches-1]++;}
}
for(const {Id,Suspension:s} of suspensions){
  assert.ok(!uniqueSuspensions.has(s.Key));uniqueSuspensions.add(s.Key);
  assert.ok(s.BalanceBefore>=0&&s.BalanceAfter>=0&&s.BalanceAfter<=4);
  const key=Id+':'+s.MatchId;
  if(s.Action==='AWARD'){
    assert.ok(!awards.has(key));awards.set(key,s);
    assert.ok(s.Matches>=1&&s.Matches<=4);assert.equal(s.BalanceAfter,Math.max(s.BalanceBefore,s.Matches));
    if(s.Reason==='SECOND_YELLOW'||s.Reason==='THREE_YELLOWS')assert.equal(s.Matches,1);
    reasons[s.Reason]=(reasons[s.Reason]??0)+1;awarded+=s.Matches;
  }else{
    assert.equal(s.Action,'SERVE');assert.equal(s.Matches,1);assert.equal(s.BalanceBefore-s.BalanceAfter,1);served++;
  }
}
for(const {Id,Suspension:s} of suspensions.filter(r=>r.Suspension.Action==='SERVE'))assert.ok(!awards.has(Id+':'+s.MatchId));
const grantDates=new Map();
for(const {Id,Suspension:s} of suspensions){
  const date=s.Season*1000+s.Round;
  if(s.Action==='AWARD')grantDates.set(Id,date);
  else if(grantDates.has(Id))assert.ok(date>grantDates.get(Id),'Infraction match was counted toward its own suspension');
}
assert.equal(kinds.DirectRedCard,data.DirectRedCards);assert.equal(kinds.SecondYellowRedCard,data.SecondYellowRedCards);
assert.equal(kinds.YellowCard,data.YellowCards);assert.equal(data.DirectRedCards+data.SecondYellowRedCards,data.RedCards);
const durationRows=sample.Weights.map((weight,i)=>{
  const p=weight/100,v=sample.Samples*p*(1-p),t=6*Math.sqrt(v)+2;
  assert.ok(Math.abs(sample.Counts[i]-sample.Samples*p)<=t);
  return {Matches:i+1,Percent:weight,Count:sample.Counts[i],ObservedPercent:sample.Counts[i]*100/sample.Samples};
});
assert.equal(sample.Samples,100000);assert.equal(sample.Counts.reduce((a,b)=>a+b),100000);
assert.equal(sample.Weights.reduce((a,b)=>a+b),100);
const familyBound=sample.Weights.reduce((sum,w)=>{const p=w/100,v=sample.Samples*p*(1-p),t=6*Math.sqrt(v)+2;return sum+2*Math.exp(-t*t/(2*(v+t/3)));},0);
assert.equal(injurySample.Samples,100000);assert.equal(replay.First,replay.Second);
const forceKeys=new Set(),dailyKeys=new Set(),lastForce=new Map(),losses=new Map();
for(const e of force){
  assert.ok(!forceKeys.has(e.IdempotencyKey));forceKeys.add(e.IdempotencyKey);
  assert.ok(e.StrengthAfter>=1&&e.StrengthAfter<=50);
  if(lastForce.has(e.PlayerId))assert.equal(e.StrengthBefore,lastForce.get(e.PlayerId));lastForce.set(e.PlayerId,e.StrengthAfter);
  if(e.ReserveGrowth>0)assert.ok(e.StrengthAfter<=e.GrowthCeiling);
  if(e.ChangeReason==='INJURY_DAILY_LOSS'){
    const key=e.InjuryId+':'+e.GameDate;assert.ok(!dailyKeys.has(key));dailyKeys.add(key);
    losses.set(e.InjuryId,(losses.get(e.InjuryId)??0)+e.DailyInjuryLoss);
  }else assert.ok(e.ChangeReason==='RESERVE_PROGRESS'||e.ChangeReason==='PLAYED_MATCH_DECLINE');
}
for(const {Injury:i} of injuries){assert.ok(i.Duration>=2&&i.Duration<=15);assert.ok(i.TotalLoss<=Math.floor(i.InitialForce*i.Duration/30));assert.ok((losses.get(i.Id)??0)<=i.TotalLoss);}
const original006=read(join(root,'docs/DISCIPLINE_STATISTICS_006.json'));
const baseline006=original006.baseline005VsCandidate006Comparison.candidate006;
const legacy012=read(join(root,'docs/audit-012/force-012/candidate-100-seasons.json'));
const prior012c=read(join(root,'docs/audit-012c/regression/classic-force-012c/100-seasons.json'));
const metrics=[
  ['Faltas',baseline006.totalFouls,data.TotalFouls],['Amarelos isolados (eventos YellowCard)',baseline006.yellowCards,data.YellowCards],
  ['Vermelhos diretos',baseline006.directRedCards,data.DirectRedCards],['Expulsões por segundo amarelo',baseline006.secondYellowRedCards,data.SecondYellowRedCards],
  ['Expulsões totais',baseline006.totalRedCards,data.RedCards],['Lesões (referência legada 012)',legacy012.Injuries,data.Injuries],
  ['Gols (referência legada 012)',legacy012.TotalGoals,data.TotalGoals]
].map(([Metric,Before,After])=>({Metric,Before,After,BeforePerMatch:Before/35100,AfterPerMatch:After/35100,ChangePercent:(After/Before-1)*100}));
const summary={Rule:'DISCIPLINE_INJURY_AND_MAN_DOWN_RULES',Version:1,Seasons:100,Matches:data.TotalMatchesCompleted,Minutes:data.RealMinutesSimulated,
  CardTypes:kinds,DirectRedDurationCounts:directCounts,CardRecords:cards.length,SuspensionRecords:suspensions.length,
  AwardReasons:reasons,AwardedMatches:awarded,ServedMatches:served,NoDuplicateCards:true,NoDuplicateSuspensions:true,NoInfractionMatchServed:true,
  ShortHandedMatches:data.ShortHandedMatches,UnreplacedInjuries:data.UnreplacedInjuries,Metrics:metrics,
  Prior012C:{Goals:prior012c.TotalGoals,Injuries:prior012c.Injuries},DirectRedSample:durationRows,StatisticalFamilyErrorBound:familyBound,
  InjuryEpisodes:injuries.length,ForceEvents:force.length,Replay:replay,ReserveEvents:data.ReserveEvents,PlayedLosses:data.PlayedLosses,DailyLosses:data.DailyInjuryLosses};
if(preview){json(join(audit,'preview.json'),summary);console.log(JSON.stringify(summary,null,2));process.exit(0);}
const checkpoint=read(join(audit,'baseline/manifest.json'));
assert.equal(execFileSync('git',['rev-parse','HEAD'],{cwd:root,encoding:'utf8'}).trim(),checkpoint.Head);
const changed=checkpoint.Files.filter(f=>hash(join(root,f.Path))!==f.Sha256).map(f=>f.Path.replaceAll('\\','/'));
const allowed=new Set(['src/Elifoot.Contracts/PlayerRankingsDtos.cs','src/Elifoot.Contracts/Dtos.cs','src/Elifoot.Domain/Entities.cs',
  'src/Elifoot.Domain/MatchEngine.cs','src/Elifoot.Domain/AiManagerBrain.cs','src/Elifoot.Domain/PlayerStatisticsEngine.cs',
  'src/Elifoot.Domain/CompetitionAndSeasonEngine.cs','src/Elifoot.Domain/MarketAndEconomyEngine.cs','src/Elifoot.Domain/TacticsAndSquadRules.cs',
  'src/Elifoot.Application/MultiplayerRoundOrchestrator.cs','src/Elifoot.Application/ClubAndMarketApplicationService.cs','src/Elifoot.Infrastructure/EfWorldRepository.cs',
  'client/elifoot_flutter/lib/main.dart','client/elifoot_flutter/lib/shell_actions.dart','tests/Elifoot.Domain.Tests/DomainEngineAndRulesTests.cs',
  'tests/Elifoot.Domain.Tests/PlayerForce012Tests.cs','tests/Elifoot.Simulation.Tests/ClassicForce012CSimulationTests.cs']);
assert.deepEqual(changed.filter(p=>!allowed.has(p)),[]);
const before=text(join(audit,'baseline/files/src/Elifoot.Domain/MatchEngine.cs')),after=text(join(root,'src/Elifoot.Domain/MatchEngine.cs'));
const extract=(s,name,next)=>s.slice(s.indexOf(name),s.indexOf(next,s.indexOf(name)));
for(const [start,end] of [
  ['    public static SectorBreakdown ComputeSectorBreakdown(','    public static double GetEffectivePlayerRating('],
  ['    public static double GetBehaviorFoulWeight(','    /// <summary>\n    /// Computes a player'],
  ['    public static double GetEffectivePlayerFoulWeight(','    /// <summary>\n    /// Simulates a single']])assert.equal(extract(before,start,end),extract(after,start,end));
for(const literal of ['0.095 * avgWeight','0.0128 * avgWeight','if (rng.NextDouble() >= 0.0018) return;'])assert.ok(before.includes(literal)&&after.includes(literal));
const cases=new Map(),trxFiles=readdirSync(join(audit,'tests')).filter(f=>/^(verified|closing)-discipline012c.*\.trx$/.test(f));
assert.ok(trxFiles.filter(f=>f.startsWith('verified')).length===4);
for(const file of trxFiles){
  for(const tag of text(join(audit,'tests',file)).matchAll(/<UnitTestResult\b[^>]*>/g)){
    const attrs=Object.fromEntries([...tag[0].matchAll(/(\w+)="([^"]*)"/g)].map(m=>[m[1],m[2]]));
    assert.equal(attrs.outcome,'Passed',attrs.testName);cases.set(attrs.testName,attrs.outcome);
  }
}
assert.equal(cases.size,367);
for(const count of [2,4,6,8])assert.ok([...cases.keys()].some(n=>n.includes('IndependentHttpClientsSeeTheSameRealInjury')&&n.includes(`count: ${count}`)));
assert.match(text(join(audit,'build-verified.log')),/0 Erro\(s\)/);
assert.match(text(join(audit,'flutter-analyze.log')),/No issues found!/);
assert.match(text(join(audit,'flutter-tests.log')),/\+56: All tests passed!/);
summary.Status='PASS';summary.DotnetCases=cases.size;summary.FlutterCases=56;summary.TrxFiles=trxFiles;
summary.DefensiveFormulaPreserved=true;summary.BehaviorFoulWeightsPreserved=true;summary.OccurrenceProbabilityConstantsPreserved=true;
summary.ChangedFiles=changed;summary.PreservedCheckpointFiles=checkpoint.Files.length-changed.length;
summary.CreatedFiles=['src/Elifoot.Contracts/PlayerDisciplineDtos.cs','src/Elifoot.Domain/DisciplineEngine.cs',
  'tests/Elifoot.Domain.Tests/Discipline012CTests.cs','tests/Elifoot.Integration.Tests/Discipline012CIntegrationTests.cs',
  'tests/Elifoot.Simulation.Tests/Discipline012CSimulationTests.cs','client/elifoot_flutter/test/discipline_012c_test.dart','scripts/report-discipline-012c.mjs',
  ...['DISCIPLINE_RULES_012C.md','RED_CARD_SUSPENSION_012C.md','YELLOW_CARD_ACCUMULATION_012C.md','MAN_DOWN_MATCH_IMPACT_012C.md','INJURY_SELECTION_AUDIT_012C.md','DISCIPLINE_REGRESSION_012C.md'].map(p=>'docs/'+p)];
const save=(name,body)=>writeFileSync(join(root,'docs',name),body.trim()+'\n');
const metricsTable=table(['Indicador','Referência','Atual','Por jogo antes','Por jogo atual','Variação %'],metrics.map(r=>[r.Metric,r.Before,r.After,fmt(r.BeforePerMatch),fmt(r.AfterPerMatch),fmt(r.ChangePercent)]));
save('DISCIPLINE_RULES_012C.md',`
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

${table(['Campo','Resultado'],[
 ['INJURY_PROBABILITY','PASS — 0,0018 por equipe/minuto, inalterada'],['INJURED_PLAYER_SELECTION','PASS — seleção uniforme entre atletas efetivamente ativos'],
 ['YELLOW_PROBABILITY','PASS — gatilho 0,0128 × peso médio e comportamento preservados'],['DIRECT_RED_PROBABILITY','PASS — taxas condicionais 0,005/0,012/0,025/0,048/0,085 preservadas'],
 ['SECOND_YELLOW_DISMISSAL','PASS — saída imediata, dois amarelos históricos e ciclo anterior preservado'],['THREE_YELLOW_SUSPENSION','PASS — três jogos distintos, um jogo de suspensão, reset do ciclo'],
 ['DIRECT_RED_1_TO_4_MATCHES','PASS — pesos exatos e 100.000 amostras'],['CROSS_COMPETITION_SUSPENSION','PASS — saldo único Liga/Taça'],['SEASON_CARRYOVER','PASS — saldo/ciclo/histórico retidos'],['TRANSFER_CARRYOVER','PASS — acompanhamento pelo jogador'],
 ['MAN_DOWN_STRENGTH_IMPACT','PASS — composição real e fórmula anterior'],['INJURY_NO_SUBSTITUTION','PASS — desfalque real, sem substituto automático'],['SAVE_COMPATIBILITY','PASS com lacunas históricas sinalizadas'],
 ['DETERMINISM','PASS — replay integral, duas temporadas reais repetidas e ticks idempotentes'],['MULTIPLAYER','PASS — 2/4/6/8 clientes HTTP e ciclo SignalR existente'],['TESTS',cases.size+' casos .NET, 56 Flutter, 35.100 partidas na calibração clássica'],
 ['KNOWN_ISSUES','Histórico legado incompleto; escalação extremamente reduzida é política adaptada; conferir DISCIPLINE_REGRESSION_012C.md']])}

Arquivos existentes alterados desde o checkpoint deste adendo:

${changed.map(p=>'- '+p).join('\n')}

Arquivos criados:

${summary.CreatedFiles.map(p=>'- '+p).join('\n')}

Checkpoint seguro: docs/audit-012c-discipline/baseline/manifest.json, files e preexisting.patch; HEAD ${checkpoint.Head}. Os checkpoints 012, 012B e 012C anteriores permanecem intactos. Nada foi publicado e nenhum outro projeto foi modificado.
`);
save('RED_CARD_SUSPENSION_012C.md',`
# Vermelho direto e suspensão — 012C

STATUS: PASS.

A chance de expulsão não foi alterada. Após um gatilho de cartão, o atleta é escolhido pelo peso efetivo de falta. Só então a chance de vermelho direto depende do comportamento: Cordeirinho 0,5%, Cavalheiro 1,2%, Caneleiro 2,5%, Caceteiro 4,8%, Sarrafeiro 8,5%; FairPlay não entra nos candidatos a faltas intencionais. Esses percentuais são condicionais à decisão de cartão e escolha do infrator, não por minuto nem por partida.

Depois de ocorrer o vermelho, o RNG separado usa seed do mundo, temporada, rodada, índice determinístico da partida, minuto do cartão e token estável do jogador (salt 21021). NextInt(0,100), limite superior exclusivo, mapeia 0..49→1; 50..79→2; 80..94→3; 95..99→4 jogos. Não consome sorteio adicional do fluxo principal de faltas, gols e lesões. A duração sorteada fica no evento e no histórico, sem re-sorteio no encerramento ou reload.

${table(['Jogos','Esperado %','Amostras','Observado %'],durationRows.map(r=>[r.Matches,r.Percent,r.Count,fmt(r.ObservedPercent)]))}

100.000 sorteios, seed ${sample.Seed}; cada frequência dentro de 6 desvios-padrão binomiais +2 contagens. Usando Bernstein e soma dos quatro limites de cauda, a probabilidade familiar de rejeição sob sorteios independentes corretos é limitada por ${familyBound.toExponential(3)}. Os 100 valores de entrada também são testados exaustivamente, cobrindo todos os limites.

Na simulação real, ${data.DirectRedCards} vermelhos diretos produziram durações [1,2,3,4] em contagens [${directCounts.join(', ')}]. O teste estatístico separado controla os pesos; a quantidade real de cartões depende das escalações/comportamentos e não é forçada à proporção exata de duração.

Expulsão remove o jogador imediatamente. Não participa de jogadas posteriores, não volta ao campo e não pode ser substituído. Um amarelo isolado anterior no mesmo jogo continua histórico; se completou o terceiro acumulado, a punição final é max(1,duraçãoDoVermelho), sem soma. O próprio jogo não serve a punição. Jogos seguintes oficiais do clube atual consomem um por encerramento, compartilhando Liga e Taça, inclusive após transferência/virada de temporada.
`);
save('YELLOW_CARD_ACCUMULATION_012C.md',`
# Amarelos globais — 012C

STATUS: PASS. Contador por jogador/mundo, compartilhado entre Campeonato e Taça.

${table(['Ciclo antes','Primeiro amarelo no jogo','Se houver segundo amarelo no mesmo jogo','Punição final pela dupla'],[
 [0,'1','Restaurar 0',1],[1,'2','Restaurar 1',1],[2,'0 e suspensão provisória por terceiro','Restaurar 2; substituir motivo por SECOND_YELLOW',1]])}

Três amarelos isolados em três jogos diferentes completam o ciclo, concedem um jogo e reiniciam o ciclo em zero. O total vitalício não zera. Transferir com dois, receber um na estreia e cumprir no próximo jogo do novo clube é coberto em teste com transferência real de mercado.

BUG-006-01 já evitava contar duas vezes o segundo amarelo quando o ciclo anterior era 0 ou 1. Permanecia uma falha quando o anterior era 2: o primeiro amarelo acionava o terceiro e apagava o ciclo; o ramo do segundo não o recuperava. MatchPlayerDiscipline agora guarda CycleBefore e a decisão é reconstruída a partir desse valor. O segundo amarelo restaura exatamente o ciclo anterior e gera um jogo. CardHistory conserva ambos os cartões; SeasonYellows e rankings contam dois amarelos e um vermelho. Não há dois registros AWARD independentes para a mesma partida/jogador.

Um terceiro amarelo durante o jogo não retira o atleta; ele continua elegível até eventual expulsão/lesão. O saldo é para a partida seguinte. Primeiro amarelo pode ser contabilizado provisoriamente durante o jogo e revertido pelo segundo, sem apagar o evento histórico do cartão. Se houver vermelho direto depois de um amarelo que completou três, usa a maior punição.

TotalYellowCards, YellowCardsInCurrentCycle, TotalDirectRedCards, TotalSecondYellowDismissals, saldo, motivo e históricos seguem no jogador. SeasonYellows/SeasonReds seguem sendo estatísticas sazonais. Nenhuma punição disciplinar retira força nominal; quando não participa por suspensão no Campeonato, o atleta segue elegível ao ganho de reserva da 012C dentro do teto. Na Taça não cresce.

Na migração, só totais mínimos verificáveis e eventos retidos são recuperados. O ciclo antigo é mantido; partidas antigas em andamento com marcador explícito de terceiro permitem reconstruir o valor anterior 2. Sem prova, não se inventam datas, cartões nem tipos de vermelho. As lacunas são expostas por avisos de cobertura, e punições antigas não são sorteadas novamente.
`);
save('MAN_DOWN_MATCH_IMPACT_012C.md',`
# Impacto de expulsões e contusões — 012C

STATUS: PASS. ComputeSectorBreakdown, GetEffectivePlayerRating e fórmula defensiva preservados exatamente no código do checkpoint.

O motor usa a lista real de jogadores ativos em cada minuto. Expulso sai definitivamente. Lesionado sai imediatamente, mantendo apenas uma identificação de vaga para eventual troca legal. Substituído deixa de estar exposto a faltas/contusões. Antes do cálculo seguinte, listas de expulsos/lesionados também removem qualquer resíduo de estado antigo.

A fórmula existente redistribui os setores pela formação, reduz vagas quando há menos atletas e usa os pesos posicionais anteriores: 1,00 no setor natural, 0,65 improvisado em outro setor de linha e 0,35 no gol improvisado. O fator coletivo 0,93 elevado ao número de atletas ausentes já existia nas auditorias 005/006. Foi preservado, sem novo desconto adicional. A defesa permanece (Defesa + 0,45×Meio) × mando × identidade × fator coletivo; ataque usa (Ataque + 0,50×Meio), com modificador clássico 5-0-5 inalterado.

Exemplo controlado 4-4-2, força 30, sem estrelas, sem mando ou bônus de identidade:

${table(['Composição','Atletas','Força efetiva de defesa','Força efetiva de ataque','Força do goleiro'],[
 ['11 completos',11,174,120,30],['Sai um atacante',10,161.82,83.7,27.9],['Saem dois atacantes',9,138.81345,55.78605,25.947],
 ['Sai um defensor',10,147.66075,78.8175,27.9],['Sai um meio-campista',10,157.42575,78.8175,27.9],['Sai o goleiro; linha improvisa',10,147.66075,78.8175,9.765]
].map(r=>r.map((v,i)=>i>1?fmt(v):v)))}

São consequências da composição e fórmula existente, não um percentual novo de penalização. A matriz de testes compara 11×11, 10×11, 9×11 e 10×10; testes de expulsão real cobrem G/D/M/A, proibição de retorno/substituição e ausência em eventos posteriores. A escolha do improvisado no gol retira aquele mesmo atleta das contribuições de linha: nenhum atleta gratuito é acrescentado.

Limites de troca preservados: 5 jogadores/3 janelas em 90 minutos; 6/4 na prorrogação; intervalos gratuitos conforme regra existente. Sem quota/janela/reserva apto, a vaga permanece vazia. Duas lesões podem deixar nove. A IA continua usando sua lógica aprovada; a retirada por lesão não realiza troca sozinha. O treinador humano escolhe se fará uma troca legal.

HomePlayersOnPitch/AwayPlayersOnPitch e IsOnPitch refletem a composição real na API. O lesionado e o expulso permanecem identificáveis na projeção, com IsOnPitch=false; apenas a vaga de lesão aceita substituição. Banco esgotado não é reconstruído a partir da preferência antiga. A UI usa RequiredStarterCount do servidor para permitir início reduzido quando faltam aptos e exibe a quantidade em campo.

Em 100 temporadas reais, ${fmt(data.ShortHandedMatches)} partidas terminaram com algum lado abaixo de 11 e ${fmt(data.UnreplacedInjuries)} vagas por lesão ficaram sem preenchimento no encerramento. Esses números também podem incluir equipes que já começaram reduzidas, conforme escolha do proprietário. Não se impôs mínimo de sete; o estresse legado contém casos com cinco aptos. Um elenco sem nenhum apto bloqueia a preparação; não se inventa goleiro/atleta para liberar o jogo.
`);
save('INJURY_SELECTION_AUDIT_012C.md',`
# Seleção do atleta contundido — 012C

STATUS: PASS.

A ocorrência continua em 0,0018 (0,18%) por equipe/minuto elegível. Se não há ativo, não se sorteia. Depois da ocorrência, PickOne usa NextInt(0,quantidadeDeAtivos) sobre a lista efetivamente em campo. Não existem pesos de posição, força, comportamento, idade, tempo acumulado nem duração da lesão na escolha do atleta. Cada jogador ativo possui exposição de 1/N condicionada à ocorrência daquele lado/minuto. Mais minutos em campo aumentam a exposição por mais oportunidades, sem fator adicional.

Ordem mantida: disciplina/remoção de expulsos → lista atualizada → ocorrência de lesão → escolha uniforme → retirada do lesionado → recálculo de setores → jogadas. Quem já saiu, foi expulso ou ainda não entrou não pode ser escolhido. O goleiro faz parte da mesma população elegível. Os testes selecionam contextos determinísticos para atingir cada posição, sem mudar a probabilidade do motor.

A duração é um sorteio separado após a ocorrência. No modo ClassicAdapted continua exatamente a distribuição 012C, com 2..15 dias, 80% em 2..8 e 20% em 9..15. O sorteio legado de duração ainda é consumido no fluxo principal para preservar seu encadeamento; duração/perda clássica usam fluxo separado. O adendo não alterou ClassicForceEvolutionEngine.cs, ClassicForceState.cs, FORCE_CEILINGS_012C.json ou WEIGHTED_INJURIES_012C.json. Saves de modo LegacySeasonal mantêm seu calendário de lesão antigo até migração explícita de força, conforme compatibilidade anterior.

Retirada não é perda nominal imediata. No clássico, a força segue caindo por dias virtuais, com orçamento máximo floor(forçaInicial×duração/30), inclusive máximo 25 para força 50/duração 15; crescimento de não participação permanece possível no Campeonato após a parcela diária, dentro do teto. Suspensão não é lesão e não cria parcelas diárias.

Na calibração final: ${fmt(data.Injuries)} lesões e ${fmt(data.RealMinutesSimulated)} minutos. Os ${fmt(injuries.length)} episódios mantiveram duração 2..15, perda sorteada dentro do máximo e parcelas acumuladas sem exceder o orçamento; zero chaves de parcela diária duplicadas. Os 100.000 sorteios de duração já exigidos pela 012C foram repetidos pela suíte, com seed ${injurySample.Seed}, e continuam dentro da tolerância definida.

## Comparação com 006

${metricsTable}

Cartões usam o JSON preservado DISCIPLINE_STATISTICS_006.json, bloco candidate006. A prosa antiga da 006 contém números diferentes de seu JSON; o quadro usa o JSON e explicita essa escolha. Gols/lesões vêm da reprodução legada preservada da 012 (seed 199801, 100 temporadas), porque o JSON disciplinar 006 não contém esses totais. A execução clássica final usa oito atletas existentes ajustados para força 1, como na auditoria 012C; demais condições da calibração mantidas. Logo, a comparação com 006 é descritiva, não um teste isolado de causalidade das novas durações de suspensão.

Comparação adicional com a 012C imediatamente anterior: gols ${prior012c.TotalGoals}→${data.TotalGoals}, lesões ${prior012c.Injuries}→${data.Injuries}. As constantes de chance e pesos de falta foram comparadas no fonte e permaneceram iguais. Mudanças agregadas são esperadas pela disponibilidade real, elenco ativo e força evoluindo por regras clássicas, sem retunar chances para obter contagens iguais.
`);
const newCases=[...cases.keys()].filter(n=>n.includes('Discipline012C')).length;
save('DISCIPLINE_REGRESSION_012C.md',`
# Regressão do adendo disciplinar — 012C

STATUS: PASS. ${cases.size} casos .NET distintos aprovados: 366 na suíte completa e um replay integral adicional; 56 testes Flutter. A suíte completa inclui todas as regressões anteriores, com 100 temporadas clássicas (35.100 partidas reais) e as simulações legadas de longo prazo. Os casos novos identificados por Discipline012C somam ${newCases}.

- dotnet build Elifoot.slnx -c Release: zero avisos e erros (build-verified.log).
- dotnet test Elifoot.slnx -c Release --no-build: suíte completa aprovada (regression-verified.log, TRX verified-discipline012c).
- Replay integral adicional: teste FullFixtureReplayPreservesActualCardsSuspensionHistoryAndOneTimeSettlement, TRX closing-discipline012c e replay-verified.log. Compara o estado completo de duas execuções do mesmo snapshot, incluindo cartões, ciclos, punições, episódios e repetição da liquidação.
- flutter analyze: No issues found; flutter test: 56 aprovados, incluindo escalação reduzida em 390/1280 px, sem overflow.
- Multiplayer: quatro testes novos com 2/4/6/8 clientes HTTP independentes observando uma contusão real, a mesma contagem em campo e um único episódio; o ciclo HTTP/SignalR 2/4/6/8 e o teste de 100 rodadas/8 técnicos também foram reexecutados.
- SQLite: migração de campos antigos, fechamento/reabertura do repositório, saldo/ciclo mantidos e cumprimento idempotente por partida.
- Fechamento: setores e troca legal de goleiro em sector-verified.log; migração de liquidação pendente e de partida ao vivo em migration-verified.log. Estatística aplicada isoladamente não é prova de cumprimento já descontado.
- Estatística separada: 100.000 durações de vermelho e 100.000 durações de lesão. Limites cumulativos inteiros testados sem depender das amostras.

## Cobertura funcional

${table(['Cenários solicitados','Evidência determinística'],[
 ['1/2/3 amarelos em jogos distintos, reset e transferência com dois','ThreeIsolatedYellowsShareOneCycleAcrossLeagueCupAndTransfer'],
 ['Dois amarelos no mesmo jogo, ciclos anteriores 0/1/2, expulsão e histórico estatístico','TwoYellowsRestorePriorCycleAndAwardExactlyOneMatch'],
 ['Vermelho de 1/2/3/4 jogos e maior punição com terceiro amarelo','DirectRedCumulativeBoundariesAndMaximumOfPenalties + DirectRed100000DrawsAndExactIntegerWeights'],
 ['Campeonato→Taça, Taça→Campeonato, saldo durante preparação e após encerramento','SuspensionIsServedAtCompletionAcrossCompetitionsAndSurvivesReload'],
 ['Temporadas e transferências com suspensão','TransferAndSeasonChangeKeepCyclesBalancesAndCompleteHistories'],
 ['Folga, distrital sem jogo, adiamento, cancelamento, retry','ByesPostponementCancellationAndRepeatedSettlementDoNotServe'],
 ['11×11,10×11,9×11,10×10 e forças setoriais','ActualPlayerCompositionDeterminesManDownSectorStrength'],
 ['Expulsão G/D/M/A; não marcar/cometer falta/voltar/substituir','RealRedRemovesEveryRoleAndPreventsSubstitutionReturnAndFutureParticipation'],
 ['Lesão G/D/M/A, sem trocas, troca legal, duas saídas e goleiro improvisado','RealInjuryLeavesFieldImmediatelyAndOnlyLegalSubstitutionCanRestoreItsSlot'],
 ['Elenco apto reduzido e proibição de suspenso em emergência','SuspendedPlayersNeverStartEvenWhenSquadMustPlayShortHanded; ExtremeShortageUsesAllFiveEligibleAthletesWithoutErasingOrServingBansEarly'],
 ['Saves antigos, dados incompletos e partida antiga em andamento','LegacyMigrationPreservesKnownTotalsWithoutInventingMissingEventsOrTypes; OldLiveThirdYellowMarkerRestoresTheTwoPriorYellowsIfSecondYellowFollows'],
 ['Sem perda nominal por suspensão e crescimento de reserva elegível','SuspensionAloneNeverLosesForceAndRemainsEligibleForLeagueReserveGrowth'],
 ['Idempotência de minuto, save/reload e replay completo','RepeatedMinuteAndRestoredFixtureDoNotRepeatEventsCardsOrInjuries; FullFixtureReplayPreservesActualCardsSuspensionHistoryAndOneTimeSettlement'],
 ['Rankings, gols, prêmios, mercado, economia, estádios, carreira, pausas e substituições','Suíte completa anterior reexecutada, sem remoção de testes']])}

Dois testes antigos tiveram preparação corrigida: a reincidência agora usa fixture novo por seed, pois repetir o mesmo minuto é idempotente; o teste de cumprimento de suspensão passou a indicar CurrentMinute=90 para representar uma partida realizada. Suas verificações esportivas foram preservadas. A instrumentação da simulação clássica apenas acrescentou exportação de cartões/suspensões e métricas; não mudou sua seed, probes ou motor.

## Auditoria de integridade

${fmt(cards.length)} cartões e ${fmt(suspensions.length)} registros de suspensão, sem chaves repetidas. ${fmt(served)} jogos de cumprimento, sempre retirando um e nunca no mesmo jogo da infração. Uma decisão AWARD por jogador/partida, com motivo final: ${Object.entries(reasons).map(([r,n])=>r+'='+fmt(n)).join('; ')}. A soma de durações concedidas não é comparada cegamente a jogos servidos, pois jogos de fim do ensaio podem deixar saldo pendente e a regra aplica máximos, sem empilhar punições incompatíveis.

${fmt(force.length)} alterações de força verificadas com continuidade por atleta, sem duplicação de chaves ou parcelas diárias; forças dentro de 1..50, tetos respeitados nos ganhos, zero crescimento na Taça, ${fmt(data.TurnSnapshots)} snapshots de turno. Replay de duas temporadas reais repetido: ${replay.First}. A fórmula defensiva, pesos de comportamento e chances de ocorrência estão preservados no fonte. A tabela de tetos, pesos de lesão e motor de evolução 012C permanecem byte a byte iguais ao checkpoint deste adendo.

## Limitações conhecidas

1. Históricos legados podem não conter todos os eventos, datas ou tipos de vermelho. Os totais verificáveis são preservados, o restante é sinalizado em DisciplineCoverageWarnings e UnclassifiedHistoricalRedCards; não há reconstrução fictícia. Em snapshot antigo de partida ao vivo, o ciclo pode ser inferido de marcadores explícitos, mas a origem de uma suspensão anterior pode permanecer desconhecida.
2. IsCancelled/IsPostponed impedem simulação e cumprimento de suspensão enquanto ativos. Não foi criado novo fluxo administrativo de cancelamento/remarcação nem endpoint para anular cartões; os testes cobrem o contrato de elegibilidade desses estados. Adiamento por lesão já existente na 012C continua apenas avançando dias, sem servir suspensão.
3. A escolha autorizada permite começar com todos os aptos disponíveis, inclusive cinco em estresse extremo. Não foi imposto mínimo de sete nem criado atleta artificial. Zero aptos bloqueia preparação. Esta é uma adaptação de design, a acompanhar em sessões humanas.
4. Histórico/chaves são retidos e crescem com a duração do mundo; arquivamento/paginação nova permanece fora deste adendo. Foram usados clientes HTTP/SignalR automatizados, sem alegação de playtest com oito dispositivos físicos.
5. Os relatórios antigos não foram reescritos. Seus números descrevem a versão auditada naquela etapa. Este adendo é a referência das alterações disciplinares atuais; diferenças estatísticas frente a 006 incluem as regras de força 012C e disponibilidade corrigida.

As primeiras execuções preservadas em domain-first.log, targeted-verified.log e regression-final.log registram ajustes de fixtures/escopo de serviço e o limite adicional de sete posteriormente removido. flutter-layout-diagnostic.log identifica um viewport lógico/físico inconsistente do teste, corrigido usando a dimensão física correta; a execução Flutter final passou. A evidência conclusiva está em regression-verified.log, replay-verified.log e nos TRX verificados.

Reprodução, na raiz do projeto:

~~~powershell
$env:ELIFOOT_AUDIT_OUTPUT='D:\\elifoot_98\\docs\\audit-012c-discipline\\verified'
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
`);
summary.GeneratedAtUtc=new Date().toISOString();
summary.FileHashes=[...changed,...summary.CreatedFiles].map(Path=>({Path,Sha256:hash(join(root,Path))}));
summary.EvidenceHashes=['build-verified.log','regression-verified.log','replay-verified.log','sector-verified.log','migration-verified.log','flutter-analyze.log','flutter-tests.log',
  ...trxFiles.map(f=>'tests/'+f),'verified/classic-force-012c/100-seasons.json','verified/classic-force-012c/discipline-cards.jsonl.gz',
  'verified/classic-force-012c/discipline-suspensions.jsonl.gz','verified/direct-red-100000.json']
  .map(Path=>({Path,Sha256:hash(join(audit,Path))}));
json(join(audit,'verification.json'),summary);
console.log(JSON.stringify({Status:summary.Status,DotnetCases:cases.size,FlutterCases:56,Matches:data.TotalMatchesCompleted,
  CardRecords:cards.length,SuspensionRecords:suspensions.length,PreservedCheckpointFiles:summary.PreservedCheckpointFiles,Metrics:metrics},null,2));
