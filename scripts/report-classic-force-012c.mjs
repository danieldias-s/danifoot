import {readFileSync, writeFileSync, readdirSync, existsSync} from 'node:fs';
import {join, resolve, dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
import {gunzipSync} from 'node:zlib';
import {createHash} from 'node:crypto';
import {execFileSync} from 'node:child_process';
import assert from 'node:assert/strict';

// Rebuild the report from real test artifacts. Never manufactures passing test results.
const root=resolve(dirname(fileURLToPath(import.meta.url)),'..');
const audit=join(root,'docs/audit-012c');
const preview=process.argv.includes('--preview');
const run=join(audit,preview&&!process.argv.includes('--regression')?'targeted':'regression','classic-force-012c');
const read=p=>JSON.parse(readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const raw=p=>readFileSync(p,'utf8').replace(/^\uFEFF/,'');
const json=(p,data)=>writeFileSync(p,JSON.stringify(data,null,2)+'\n');
const lines=p=>(p.endsWith('.gz')?gunzipSync(readFileSync(p)).toString('utf8'):raw(p)).trim().split(/\r?\n/).filter(Boolean).map(JSON.parse);
const hash=p=>createHash('sha256').update(readFileSync(p)).digest('hex').toUpperCase();
const data=read(join(run,'100-seasons.json'));
const events=lines(join(run,'force-events.jsonl.gz'));
const injuries=lines(join(run,'injuries.jsonl.gz'));
const probes=lines(join(run,'force-one-probes.jsonl'));
const sample=read(join(run,'weighted-duration-sample.json'));
const replay=read(join(run,'replay.json'));
const divisions=['PrimeiraDivisao','SegundaDivisao','TerceiraDivisao','QuartaDivisao'];
const ceilings=[[50,50,50,49,48,47,46,44],[44,42,42,39,37,34,31,28],[28,26,26,23,21,18,15,12],[12,10,10,8,6,4,2,1]];
const weights=[26,26,24,24,22,20,18,12,8,6,5,4,3,2];
assert.equal(weights.reduce((a,b)=>a+b),200);
assert.equal(data.SeasonsCompleted,100);assert.equal(data.TotalMatchesCompleted,35100);
assert.equal(data.TurnSnapshots,6400);assert.equal(data.CupGrowth,0);
assert.equal(data.InvalidLineupSelectionViolations,0);assert.equal(data.DoubleSettledMatchFinancialViolations,0);
assert.equal(data.BilateralPurchaseDebitsTotal,data.BilateralSaleCreditsTotal);
assert.equal(injuries.length,data.InjuryEpisodes);
assert.equal(sample.Samples,100000);assert.equal(sample.Counts.reduce((a,b)=>a+b),100000);
assert.equal(replay.First,replay.Second);assert.equal(replay.Identical,true);
const durationSample=weights.map((w,i)=>{
  const p=w/200,expected=p*sample.Samples,sd=Math.sqrt(sample.Samples*p*(1-p));
  assert.ok(Math.abs(sample.Counts[i]-expected)<=6*sd+2);
  return {Days:i+2,WeightOutOf200:w,Percent:w/2,Count:sample.Counts[i],ObservedPercent:sample.Counts[i]/1000,Sigma:(sample.Counts[i]-expected)/sd};
});
const familyBound=weights.reduce((sum,w)=>{
  const p=w/200,variance=sample.Samples*p*(1-p),t=6*Math.sqrt(variance)+2;
  return sum+2*Math.exp(-t*t/(2*(variance+t/3)));
},0);
const reasonTotals={};const ids=new Set(), dailyKeys=new Set(), last=new Map(), perInjury=new Map(), firstCap=new Map();
for(const e of events){
  assert.ok(!ids.has(e.IdempotencyKey));ids.add(e.IdempotencyKey);
  assert.ok(e.WorldId&&e.PlayerId&&e.GameDate);
  if(e.ClubId===null)assert.equal(e.Division,null); // Injury days also run while a player is a free agent.
  assert.ok(e.StrengthAfter>=1&&e.StrengthAfter<=50);
  assert.equal(e.ForceBefore,e.StrengthBefore);assert.equal(e.ForceAfter,e.StrengthAfter);
  if(last.has(e.PlayerId))assert.equal(e.StrengthBefore,last.get(e.PlayerId));
  last.set(e.PlayerId,e.StrengthAfter);
  const bucket=reasonTotals[e.ChangeReason]??={Events:0,Points:0};
  bucket.Events++;bucket.Points+=Math.abs(e.StrengthAfter-e.StrengthBefore);
  if(e.ChangeReason==='RESERVE_PROGRESS'){
    assert.equal(e.StrengthAfter-e.StrengthBefore,1);assert.ok(e.StrengthAfter<=e.GrowthCeiling);
    if(e.StrengthAfter===e.GrowthCeiling&&!firstCap.has(e.PlayerId))firstCap.set(e.PlayerId,{
      PlayerId:e.PlayerId,Division:e.Division,Season:e.Season,Round:e.Round,Date:e.GameDate,Ceiling:e.GrowthCeiling,
      DaysSinceWorldStart:(Date.parse(e.GameDate)-Date.parse('1998-08-01'))/86400000});
  }else if(e.ChangeReason==='PLAYED_MATCH_DECLINE')assert.equal(e.StrengthBefore-e.StrengthAfter,1);
  else {
    assert.equal(e.ChangeReason,'INJURY_DAILY_LOSS');assert.ok(e.InjuryId);
    const key=e.InjuryId+e.GameDate;assert.ok(!dailyKeys.has(key));dailyKeys.add(key);
    perInjury.set(e.InjuryId,(perInjury.get(e.InjuryId)??0)+e.DailyInjuryLoss);
    assert.equal(e.DailyInjuryLoss,e.StrengthBefore-e.StrengthAfter);
  }
}
assert.equal(reasonTotals.RESERVE_PROGRESS.Events,data.ReserveEvents);
assert.equal(reasonTotals.PLAYED_MATCH_DECLINE.Events,data.PlayedLosses);
assert.equal(reasonTotals.INJURY_DAILY_LOSS.Events,data.DailyInjuryLosses);
const realDurations=Array(14).fill(0);let totalBudget=0,actualLoss=0,zeroBudget=0,maximumBudget=0;
for(const p of injuries){
  const i=p.Injury;assert.ok(i.Duration>=2&&i.Duration<=15);realDurations[i.Duration-2]++;
  const max=Math.min(i.InitialForce-1,Math.floor(i.InitialForce*i.Duration/30));
  assert.ok(i.TotalLoss>=0&&i.TotalLoss<=max);assert.ok((perInjury.get(i.Id)??0)<=i.TotalLoss);
  totalBudget+=i.TotalLoss;actualLoss+=perInjury.get(i.Id)??0;
  if(i.TotalLoss===0)zeroBudget++;if(i.TotalLoss===max)maximumBudget++;
}
const aggregate=rows=>{
  const Count=rows.reduce((n,r)=>n+r.Count,0);
  return {Count,Mean:Count?rows.reduce((n,r)=>n+r.Mean*r.Count,0)/Count:0,
    At1:rows.reduce((n,r)=>n+(r.At1??r.Histogram?.['1']??0),0),At50:rows.reduce((n,r)=>n+(r.At50??r.Histogram?.['50']??0),0),
  Minimum:rows.length?Math.min(...rows.map(r=>r.Minimum??1)):null,Maximum:rows.length?Math.max(...rows.map(r=>r.Maximum??50)):null};
};
const end=(source,season,division)=>source.Distributions.filter(r=>r.Phase==='round'&&r.Season===season&&(!division||r.Division===division));
const final=aggregate(end(data,100));
const seasons=[1,10,25,50,100].map(Season=>({Season,Divisions:Object.fromEntries([...divisions,'Distrital'].map(d=>[d,aggregate(end(data,Season,d))]))}));
const byRank=divisions.flatMap((d,di)=>Array.from({length:8},(_,i)=>({Division:d,Rank:i+1,Ceiling:ceilings[di][i],...aggregate(end(data,100,d).filter(r=>r.ClubPosition===i+1))})));
const byRole=['G','D','M','A'].map(Position=>({Position,...aggregate(end(data,100).filter(r=>r.Position===Position))}));
const usages=['Zero league minutes','Played 1 to 6 league games','Played at least 7 league games'].map(Usage=>({Usage,
  ...aggregate((data.UsageDistributions??[]).filter(r=>r.Season===100&&r.Usage===Usage))}));
const cohorts=[false,true].map(InjuredThisSeason=>({InjuredThisSeason,...aggregate((data.UsageDistributions??[]).filter(r=>r.Season===100&&r.InjuredThisSeason===InjuredThisSeason))}));
const capsByDivision=divisions.map((d,i)=>{
  const rows=[...firstCap.values()].filter(e=>e.Division===i+1||e.Division===d).sort((a,b)=>a.DaysSinceWorldStart-b.DaysSinceWorldStart);
  return {Division:d,Players:rows.length,MedianFirstCapDay:rows.length?rows[Math.floor(rows.length/2)].DaysSinceWorldStart:null,
    First:rows[0]??null,Last:rows.at(-1)??null};
});
const probeSummary=[...new Set(probes.map(p=>p.Id))].map(Id=>{
  const rows=probes.filter(p=>p.Id===Id),first=rows[0],last=rows.at(-1);
  const cap=rows.find(p=>p.Phase==='round'&&p.Strength>=p.Ceiling);
  return {Id,Position:first.Position,InitialDivision:first.Division,FinalDivision:last.Division,FinalForce:last.Strength,
    Maximum:Math.max(...rows.map(p=>p.Strength)),FirstObservedCap:cap?{Season:cap.Season,Round:cap.Round,Date:cap.Date,Ceiling:cap.Ceiling}:null};
});
const old012=read(join(root,'docs/audit-012/force-012/candidate-100-seasons.json'));
const old012b=read(join(root,'docs/audit-012b/regression/force-012/candidate-100-seasons.json'));
const legacyPath=join(audit,'regression/force-012/candidate-100-seasons.json');
let legacyPreserved=null;
if(existsSync(legacyPath)){
  const legacy=read(legacyPath);
  assert.deepEqual(legacy.Distributions,old012.Distributions);assert.deepEqual(legacy.Distributions,old012b.Distributions);
  assert.equal(legacy.FinalStateSignature,old012.FinalStateSignature);legacyPreserved=true;
}
const finalized=source=>source.Distributions.filter(r=>r.Phase==='seasonFinalized'&&r.Season===101);
const comparison={Legacy012:aggregate(finalized(old012)),Legacy012B:aggregate(finalized(old012b)),Classic012C:aggregate(finalized(data)),LegacyModeIdentical:legacyPreserved};
assert.equal(comparison.Legacy012.Count,2308);
const delayDays=data.CalendarDelays.map(d=>(Date.parse(d.ActualDate)-Date.parse(d.ScheduledDate))/86400000);
const summary={Rule:data.Mode,Version:1,Seasons:100,Matches:data.TotalMatchesCompleted,Minutes:data.RealMinutesSimulated,
  Goals:data.TotalGoals,ReasonTotals:reasonTotals,Injuries:injuries.length,RealDurationCounts:realDurations,TotalInjuryBudget:totalBudget,
  ActualInjuryPoints:actualLoss,ZeroBudgetEpisodes:zeroBudget,MaximumBudgetEpisodes:maximumBudget,
  NoDuplicateHistoryKeys:true,NoDuplicateInjuryDays:true,ContinuousPlayerHistory:true,SeasonsByDivision:seasons,FinalByRank:byRank,
  FinalByPlayerPosition:byRole,FinalByUsage:usages,FinalInjuredCohorts:cohorts,FirstGrowthCeiling:capsByDivision,Probes:probeSummary,
  FirstGrowthCeilingNote:'First observed +1 that reaches the current club ceiling; calendar days since world creation, including transfers and table changes. Not time to a fixed ceiling under permanent reserve treatment.',
  Comparison:comparison,DurationSample:durationSample,DurationSampleFamilyErrorBound:familyBound,Replay:replay,
  AiPurchases:data.AiPurchases,StrongAiPurchases:data.StrongAiPurchases,Market:data.Market,
  CalendarPostponements:{Rounds:delayDays.length,TotalDays:delayDays.reduce((a,b)=>a+b,0),MaximumDays:Math.max(0,...delayDays)}};
if(preview){json(join(audit,'balance-preview.json'),summary);console.log(JSON.stringify({...summary,Market:undefined,DurationSample:undefined,Probes:undefined},null,2));process.exit(0);}
assert.equal(legacyPreserved,true);
assert.ok(data.UsageDistributions.length>0);
const baseline=read(join(audit,'baseline/manifest.json'));
assert.equal(execFileSync('git',['rev-parse','HEAD'],{cwd:root,encoding:'utf8'}).trim(),baseline.Head);
const changed=baseline.Files.filter(f=>hash(join(root,f.Path))!==f.Sha256).map(f=>f.Path.replaceAll('\\','/'));
// Files outside the intended integration surface, including earlier reports, stay byte-identical.
const allowed=new Set(['client/elifoot_flutter/lib/main.dart','client/elifoot_flutter/lib/player_rankings.dart','client/elifoot_flutter/lib/shell_actions.dart',
  'src/Elifoot.Application/ClubAndMarketApplicationService.cs','src/Elifoot.Application/MultiplayerRoundOrchestrator.cs',
  'src/Elifoot.Contracts/Dtos.cs','src/Elifoot.Contracts/Enums.cs','src/Elifoot.Contracts/PlayerStrengthDtos.cs',
  'src/Elifoot.Domain/AiManagerBrain.cs','src/Elifoot.Domain/ClassicForceEvolutionEngine.cs','src/Elifoot.Domain/ClassicForceState.cs',
  'src/Elifoot.Domain/CompetitionAndSeasonEngine.cs','src/Elifoot.Domain/Entities.cs','src/Elifoot.Domain/MatchEngine.cs',
  'src/Elifoot.Domain/PlayerStatisticsEngine.cs','src/Elifoot.Domain/PlayerStrengthEvolutionEngine.cs','src/Elifoot.Domain/TacticsAndSquadRules.cs',
  'src/Elifoot.Domain/WorldSeedFactory.cs','tests/Elifoot.Simulation.Tests/MultiSeasonAndBenchmarkTests.cs']);
assert.deepEqual(changed.filter(p=>!allowed.has(p)),[]);
const before=raw(join(audit,'baseline/files/src/Elifoot.Domain/MatchEngine.cs'));
const after=raw(join(root,'src/Elifoot.Domain/MatchEngine.cs'));
const defense=s=>s.slice(0,s.indexOf('    public static List<MatchEventRecord> SimulateSingleMinute('));
assert.equal(defense(before),defense(after));
const testFiles=readdirSync(join(audit,'tests')).filter(f=>f.startsWith('final012c')&&f.endsWith('.trx'));
assert.equal(testFiles.length,4);
const cases=new Map();const projects=[];
for(const file of testFiles){
  const xml=raw(join(audit,'tests',file));let n=0;
  for(const tag of xml.matchAll(/<UnitTestResult\b[^>]*>/g)){
    const attrs=Object.fromEntries([...tag[0].matchAll(/(\w+)="([^"]*)"/g)].map(m=>[m[1],m[2]]));
    assert.equal(attrs.outcome,'Passed',attrs.testName);cases.set(attrs.testName,attrs.outcome);n++;
  }
  assert.ok(n>0);projects.push({File:file,Cases:n});
}
assert.equal(cases.size,324);
assert.match(raw(join(audit,'build-final.log')),/0 Erro\(s\)/);
assert.match(raw(join(audit,'flutter-analyze.log')),/No issues found!/);
const flutter=raw(join(audit,'flutter-tests.log'));
assert.match(flutter,/\+54: All tests passed!/);
summary.Status='PASS';summary.DotnetPassingCases=cases.size;summary.FlutterPassingCases=54;
summary.DotnetProjects=projects;summary.DefensiveFormulaByteIdentical=true;
summary.ChangedExistingFiles=changed;summary.PreservedBaselineFiles=baseline.Files.length-changed.length;
summary.NewFiles=['src/Elifoot.Domain/ClassicForceState.cs','src/Elifoot.Domain/ClassicForceEvolutionEngine.cs',
  'tests/Elifoot.Domain.Tests/ClassicForce012CTests.cs','tests/Elifoot.Integration.Tests/ClassicForce012CIntegrationTests.cs',
  'tests/Elifoot.Simulation.Tests/ClassicForce012CSimulationTests.cs','tests/Elifoot.Simulation.Tests/ClassicForce012CInjuryTriggerTests.cs',
  'client/elifoot_flutter/test/classic_force_012c_test.dart','scripts/report-classic-force-012c.mjs',
  ...['CLASSIC_FORCE_EVOLUTION_012C.md','FORCE_CEILINGS_012C.json','WEIGHTED_INJURIES_012C.json','INJURY_DAILY_FORCE_LOSS_012C.md','PLAYER_FORCE_HISTORY_012C.md','FORCE_BALANCE_100_SEASONS_012C.md','FORCE_REGRESSION_TESTS_012C.md','KNOWN_ISSUES_012C.md'].map(f=>'docs/'+f)];
json(join(audit,'verification.json'),summary);
json(join(root,'docs/FORCE_CEILINGS_012C.json'),{Rule:data.Mode,Version:1,HistoricalExactnessClaim:false,StrengthRange:[1,50],GrowthOnly:true,
  ProvisionalLeaguePosition:4,Positions:[1,2,3,4,5,6,7,8],Divisions:Object.fromEntries(divisions.map((d,i)=>[d,ceilings[i]])),
  DistrictPolicy:'No league fixture and no reserve growth; cup never grows strength',ExistingSaves:'LegacySeasonal unless explicitly activated in a validated new world'});
json(join(root,'docs/WEIGHTED_INJURIES_012C.json'),{Rule:data.Mode,Version:1,Condition:'After an injury occurs; occurrence probability is unchanged',
  Draw:'NextInt(0,200), exclusive upper bound, cumulative integer weights',TotalPercent:100,Days2To8Percent:80,Days9To15Percent:20,
  Durations:durationSample,StatisticalSample:{Seed:sample.Seed,Draws:sample.Samples,Tolerance:sample.Tolerance},
  Loss:{Maximum:'floor(initialForce * duration / 30)',Sampling:'Uniform integer from 0 through maximum inclusive',CumulativeAtDay:'floor(sampledTotal * elapsedDays / duration)'}});
const fmt=n=>Number(n).toLocaleString('pt-BR',{maximumFractionDigits:2});
const table=(headers,rows)=>['| '+headers.join(' | ')+' |','| '+headers.map(()=>'---').join(' | ')+' |',...rows.map(r=>'| '+r.join(' | ')+' |')].join('\n');
const save=(name,text)=>writeFileSync(join(root,'docs',name),text.trim()+'\n');
save('CLASSIC_FORCE_EVOLUTION_012C.md',`
# CLASSIC_ADAPTED_FORCE_EVOLUTION — 012C

TASK: ELIFOOT_CLASSIC_FORCE_EVOLUTION_012C  
STATUS: PASS  
Adaptação autorizada pelo proprietário; não é uma afirmação da fórmula histórica exata do Elifoot 98. Regras versão 1.

## Tetos aprovados

${table(['Posição','DIV4','DIV3','DIV2','DIV1'],Array.from({length:8},(_,i)=>[i+1,ceilings[3][i],ceilings[2][i],ceilings[1][i],ceilings[0][i]]))}

Limitam apenas ganhos. Um força 40 no líder da DIV4 permanece 40 ao ficar sem jogar; pode perder por participação ou lesão. Força sempre entre 1 e 50. Distritais disputam apenas Taça neste projeto e não recebem crescimento de liga.

## Campeonato e participação

Oito clubes/divisão, 14 jogos, 7 em cada turno. O servidor aguarda todas as partidas da rodada oficial antes de atualizar tabela e força. Usa a posição após essa atualização. A referência inicial é o 4º lugar da divisão atual, sem classificação parcial. Promoção/rebaixamento limpa as posições de referência antes da nova temporada. A posição antiga não é aplicada na divisão nova.

Snapshots persistentes em LeaguePlayed=7 e 14 guardam clube, divisão, posição, teto, data, força inicial/atual por atleta e totais separados dos três motivos. São ${fmt(data.TurnSnapshots)} snapshots em 100 temporadas. No calendário global, as rodadas de campeonato são 3,4,6,7,9,10,12 / 13,15,16,18,19,21,22; a final da Taça é a rodada 20. A posição pode alterar o teto sem desfazer ganhos anteriores.

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

${table(['Campo solicitado','Resultado'],[
 ['FORCE_CEILINGS / DIVISION_1..4','PASS — tabela acima e JSON'],['LEAGUE_POSITION / FIRST_HALF / SECOND_HALF','PASS — tabela oficial e snapshots 7/14'],
 ['RESERVE_GROWTH','PASS — '+fmt(data.ReserveEvents)+' ganhos'],['PLAYED_MATCH_DECLINE','PASS — '+fmt(data.PlayedLosses)+' perdas'],['CUP_NO_GROWTH','PASS — zero ganhos'],
 ['INJURIES / DURATION_2_TO_15 / WEIGHTED_DISTRIBUTION','PASS — pesos exatos, 100.000 sorteios'],['DAILY_FORCE_LOSS / MAXIMUM_50_PERCENT','PASS — parcelas determinísticas, orçamento sorteado ≤ força inicial × duração/30'],
 ['INJURED_RESERVE_GROWTH / RECOVERY','PASS — cenários DIV1/DIV4/DIV3 e recuperação por data'],['PERSISTENCE / HISTORY / IDEMPOTENCE','PASS — SQLite, reinício, transferências, chaves sem duplicação'],
 ['DETERMINISTIC_REPLAY','PASS — duas execuções de 2 temporadas reais, hashes iguais'],['100_SEASON_SIMULATION','PASS — '+fmt(data.TotalMatchesCompleted)+' partidas reais'],
 ['DOTNET_TESTS','PASS — '+cases.size+' casos'],['FLUTTER_TESTS','PASS — analyze e 54 testes'],['MULTIPLAYER_REGRESSION','PASS — 100 rodadas/8 técnicos, pausas, substituições, reconexão e isolamento'],
 ['FILES_CHANGED / FILES_CREATED','Listas completas abaixo'],['KNOWN_ISSUES / NEXT_RECOMMENDATIONS','Ver KNOWN_ISSUES_012C.md']])}

Arquivos existentes alterados especificamente desde o checkpoint 012C:

${changed.map(p=>'- '+p).join('\n')}

Arquivos criados na 012C (artefatos brutos adicionais em docs/audit-012c):

${summary.NewFiles.map(p=>'- '+p).join('\n')}

Checkpoint: docs/audit-012c/baseline/manifest.json, files e preexisting.patch, HEAD ${baseline.Head}. As alterações anteriores 012/012B permanecem preservadas. Nenhum reset, commit, publicação ou alteração de outro projeto. A fórmula de setores/defesa permanece byte a byte igual ao checkpoint; mercado, finanças, estádio e carreira mantêm seus algoritmos. Preços/salários são recalculados pelas funções já existentes quando a força muda.
`);
save('INJURY_DAILY_FORCE_LOSS_012C.md',`
# Perda diária por lesão — 012C

STATUS: PASS. Adaptação autorizada, versão 1.

Após a ocorrência, duração d usa os pesos exatos de 2 a 15 dias. O orçamento máximo é floor(forçaInicial × d / 30), limitado também por forçaInicial−1. Sorteia-se uniformemente um inteiro entre zero e esse máximo, incluindo os extremos. Não existe perda nominal na hora da lesão. A força inicial e o orçamento são imutáveis no episódio, inclusive após transferência ou ganho de reserva.

No dia virtual k, a perda acumulada planejada é floor(totalSorteado × min(k,d) / d). A parcela é a diferença entre acumulados consecutivos. ProcessedDays avança mesmo quando a parcela é zero ou o piso 1 impede perda efetiva; ActualLoss registra apenas pontos efetivamente retirados. Dias e datas ficam no save, sem usar relógio do computador.

Exemplo máximo: força 50, d=15, total=25. Parcelas: ${Array.from({length:15},(_,k)=>Math.floor(25*(k+1)/15)-Math.floor(25*k/15)).join(', ')}. Soma 25; força final 25 antes de eventuais ganhos. Duração 2 pode tirar no máximo 3 de força 50; duração 8 pode tirar 13. Nem todo episódio sorteia o máximo. Recuperação acontece em StartDate+d e não restaura pontos.

O calendário aplica cada parcela pendente na sua data antes da partida. Um salto de vários dias executa as parcelas intermediárias em ordem, com histórico individual. Não se permite avançar data com partida em andamento. Na mesma data, INJURY_DAILY_LOSS pode anteceder RESERVE_PROGRESS como registros separados. DIV1 líder força 50 lesionado pode terminar acima de 25 pelos ganhos; DIV4 líder força 50→25 não ganha enquanto acima do teto 12; DIV3 último força 5 pode ganhar e continuar perdendo as parcelas previstas. A perda normal PLAYED_MATCH_DECLINE é omitida para quem se lesionou naquela partida.

Testes exercitam 2/8/15 dias, total máximo, total menor, zero, piso 1, reincidência, transferência, interrupção/releitura SQLite, mesmo dia com dois motivos e repetição da liquidação. Em 100 temporadas houve ${fmt(injuries.length)} episódios, ${fmt(zeroBudget)} com orçamento zero, ${fmt(maximumBudget)} sorteados no máximo permitido (inclusive casos cujo máximo era zero). Orçamento agregado ${fmt(totalBudget)} pontos; perda efetiva ${fmt(actualLoss)} pontos, sem exceder orçamento por episódio e sem repetição de InjuryId+data. Os registros brutos de episódios são capturados na criação; os eventos comprimidos registram a execução das parcelas.

Evidência: docs/audit-012c/regression/classic-force-012c/injuries.jsonl.gz e force-events.jsonl.gz; verificação automática em scripts/report-classic-force-012c.mjs.
`);
save('PLAYER_FORCE_HISTORY_012C.md',`
# Histórico individual de força — 012C

STATUS: PASS. O histórico 012 é estendido com campos opcionais, preservando os registros antigos e a rota GET /api/worlds/{worldId}/players/{playerId}/history.

${table(['Campo','Conteúdo'],[
 ['PlayerId / WorldId','Identidade do jogador e mundo autoritativo'],['Season / Round / GameDate','Contexto esportivo e data virtual'],['ClubId / ClubName / Division','Clube no momento do fato; transferência não reescreve o passado'],
 ['LeaguePosition / GrowthCeiling','Última posição oficial completa ou referência provisória 4 e teto aplicável'],['ForceBefore / ForceAfter','Aliases de StrengthBefore / StrengthAfter para compatibilidade'],
 ['ChangeReason','RESERVE_PROGRESS, PLAYED_MATCH_DECLINE ou INJURY_DAILY_LOSS'],['InjuryId / InjuryDuration','Episódio original e duração para perdas diárias'],['DailyInjuryLoss / ReserveGrowth','Pontos efetivos, separados por motivo'],['Rule / IdempotencyKey','CLASSIC_ADAPTED_FORCE_EVOLUTION e identidade persistente do fato']])}

Só alterações efetivas criam registros; manter força não fabrica evento. OTHER_VERIFIED_REASON fica reservado a fatos futuros comprovados, sem uso artificial. A UI mostra motivo legível, data, posição, teto e forças antes/depois; registros 012 sem novos campos aparecem com traço nesses campos.

Idempotência esportiva: chave inclui WorldId, Season, Round, PlayerId, MatchId e motivo. ProcessedMatches é persistido inclusive para jogos sem alterações. ProcessedRoundPlayers impede ganho duplicado quando propriedade muda durante a rodada; participação efetiva tem prioridade sobre a relação de reserva. Seu conjunto é reciclado por rodada, mantendo ProcessedMatches e histórico. Lesão: InjuryId+data virtual, ProcessedDays e ActualLoss persistentes, inclusive em dias sem perda. Travas existentes por mundo abrangem escrita/persistência; consultas não sorteiam novamente. Troca de temporada não limpa histórico nem snapshots.

A auditoria validou ${fmt(events.length)} eventos, zero chaves repetidas, zero duplicações de InjuryId+data e continuidade antes/depois para cada jogador durante as 100 temporadas. SQLite foi fechado/reaberto entre parcelas e consultado via endpoint. Repetição do mesmo dia/rodada não altera estado. Migração de saves legados não é automática.

Snapshots de turno incluem forças inicial/atual e ganhos/perdas por clube durante a temporada. Para jogador transferido, os totais do snapshot são os eventos ocorridos por aquele clube; o histórico individual é a referência completa entre clubes. Data e teto de evento são preservados, sem reavaliação retroativa.
`);
const endMarket=data.Market.find(m=>m.Season===100&&m.Phase==='round');
const startMarket=data.Market[0];
save('FORCE_BALANCE_100_SEASONS_012C.md',`
# Balanceamento: 100 temporadas reais — 012C

STATUS: PASS nos critérios funcionais e de regressão. O equilíbrio tem as limitações observadas abaixo. Adaptação autorizada, sem pretensão histórica exata.

Seed 199801; cenário todos IA; motor minuto a minuto; ${fmt(data.TotalMatchesCompleted)} partidas, ${fmt(data.RealMinutesSimulated)} minutos, ${fmt(data.TotalGoals)} gols, ${fmt(injuries.length)} lesões. Oito atletas existentes foram ajustados para força 1 (G/A, um par por divisão) antes da primeira rodada para acompanhar evolução a partir do piso; o restante da população, economia e IA usam a configuração existente. A simulação não força esses oito a ficar na reserva.

Zero escalações inválidas, zero liquidações financeiras duplicadas, zero crescimento na Taça. ${fmt(data.TurnSnapshots)} snapshots persistentes de turno. ${fmt(data.AiPurchases)} compras com decisão da IA, ${fmt(data.StrongAiPurchases)} de atletas força ≥40. Débitos bilaterais e créditos de vendas coincidem em ${fmt(data.BilateralPurchaseDebitsTotal)}. Promoções/rebaixamentos seguem o motor existente em todas as temporadas; a distribuição abaixo é medida antes da movimentação final, com posições oficiais daquela divisão.

## Distribuições por divisão

${table(['Temporada','Divisão','Atletas','Média','Mín.','Máx.','Força 1','Força 50'],seasons.flatMap(s=>Object.entries(s.Divisions).map(([d,r])=>[s.Season,d,r.Count,fmt(r.Mean),r.Minimum,r.Maximum,r.At1,r.At50])))}

## Posição do clube na temporada 100

${table(['Divisão','Lugar','Teto','Atletas','Média','Força 1','Força 50'],byRank.map(r=>[r.Division,r.Rank,r.Ceiling,r.Count,fmt(r.Mean),r.At1,r.At50]))}

## Participação, lesão e goleiros

Grupos de uso contam partidas do campeonato com minutos positivos na temporada 100. Sete ou mais inclui titulares e substitutos frequentes, sem afirmar que todos iniciaram jogos. Zero minutos inclui reservas, fora da relação e afastados. Distritais ficam fora desses grupos porque não têm campeonato. Grupos de lesão indicam ao menos um episódio naquela temporada, sem interpretação causal da média.

${table(['Uso no campeonato','Atletas','Média','Força 1','Força 50'],usages.map(r=>[r.Usage,r.Count,fmt(r.Mean),r.At1,r.At50]))}

${table(['Lesão na temporada','Atletas de liga','Média','Força 1'],cohorts.map(r=>[r.InjuredThisSeason?'Sim':'Não',r.Count,fmt(r.Mean),r.At1]))}

Posições da população completa (incluindo distritais):

${table(['Posição','Atletas','Média','Força 1','Força 50'],byRole.map(r=>[r.Position,r.Count,fmt(r.Mean),r.At1,r.At50]))}

Alterações: ${fmt(data.ReserveEvents)} ganhos de reserva, ${fmt(data.PlayedLosses)} perdas por participação e ${fmt(data.DailyInjuryLosses)} parcelas diárias não nulas, retirando ${fmt(actualLoss)} pontos por lesões. A força máxima observada foi ${data.MaximumForce}; nenhum elenco se tornou todo força 50. Os testes controlados demonstram força 40→50 no terceiro da DIV1 e força 1→12 no líder da DIV4, sem depender de a IA escolher essa estratégia.

## Tempo até teto

${table(['Divisão do primeiro teto atingido','Jogadores com primeiro teto nessa divisão','Mediana do primeiro dia desde criação'],capsByDivision.map(r=>[r.Division,r.Players,r.MedianFirstCapDay??'—']))}

Essa medida acompanha o primeiro ganho que chega ao teto vigente, com classificação, transferências e divisões variáveis; não mede um tratamento de reserva permanente. Muitos atletas nunca atingem teto, sobretudo na DIV1, porque a IA usa jogadores fortes. A progressão controlada segue a fórmula exata: tempo esperado em rodadas elegíveis = soma de 1/p(força,teto) sobre os pontos faltantes. Exemplos: ${[[1,12],[5,12],[40,50]].map(([f,c])=>f+'→'+c+': '+fmt(Array.from({length:c-f},(_,i)=>1/(.15+.7*(c-(f+i)-1)/Math.max(1,c-2))).reduce((a,b)=>a+b))+' rodadas').join('; ')}. São expectativas, não garantias; 14 jogos/temporada e mudança de teto alteram o tempo real. Série dos oito probes: docs/audit-012c/regression/classic-force-012c/force-one-probes.jsonl; resumo em verification.json.

## Comparação 012 e 012B, mercado e interpretação

${table(['Execução após transição da temporada 100','Atletas finais','Média','Força 1','Força 50'],Object.entries(comparison).filter(([,r])=>r&&typeof r==='object').map(([name,r])=>[name,r.Count,fmt(r.Mean),r.At1,r.At50]))}

As auditorias 012/012B caracterizaram o modo sazonal antigo. A regressão 012C também executou novamente esse modo por 100 temporadas: distribuição e assinatura final são idênticas a ambas. O novo modo muda a distribuição intencionalmente; a comparação com ele é descritiva, pois oito atletas tiveram a força inicial ajustada para os probes. A fórmula defensiva e os algoritmos de mercado/carreira não foram alterados, mas resultados esportivos e valores de mercado mudam com a força.

Oferta ≥40: ${startMarket.PlayersAtLeast40} jogadores no início (${startMarket.ListedAtLeast40} listados), ${endMarket.PlayersAtLeast40} na temporada 100 (${endMarket.ListedAtLeast40} listados). Valor justo médio: ${fmt(startMarket.AverageFairValue)} → ${fmt(endMarket.AverageFairValue)}. A queda de oferta forte e a concentração no piso são consequências relevantes do uso frequente e da ausência de crescimento na Taça; distritais sem liga só podem manter/perder. No fim do campeonato 100, ${aggregate(end(data,100,'Distrital')).At1} atletas força 1 estão no Distrital e ${divisions.reduce((n,d)=>n+aggregate(end(data,100,d)).At1,0)} nas quatro divisões de liga. A média da DIV1 passa de ${fmt(aggregate(end(data,25,'PrimeiraDivisao')).Mean)} na temporada 25 para ${fmt(aggregate(end(data,100,'PrimeiraDivisao')).Mean)} na 100, indicando estabilização nessa divisão durante o ensaio. Não se ajustaram os tetos, pesos, 15% de perda nem curva 85%→15% para esconder esse efeito. Calibração mantida após os testes por satisfazer os limites e impedir inflação a 50; recomenda-se acompanhamento de estratégia humana e de distribuição por divisão antes de eventual ajuste autorizado das probabilidades. Uma seed de 100 temporadas não certifica equilíbrio de todas as estratégias.

O primeiro ensaio encontrou um elenco de 13 atletas com duas lesões sem 11 aptos. A correção de calendário adia a rodada inteira até haver elenco mínimo, sem alterar compras da IA. Na execução final houve ${delayDays.length} rodadas adiadas, ${fmt(delayDays.reduce((a,b)=>a+b,0))} dias extras e máximo de ${Math.max(0,...delayDays)} dias por adiamento. A prontidão de humanos sem jogo também foi coberta na regressão. Logs do primeiro diagnóstico permanecem em calibration-first.log e regression.log; o resultado final está em regression-final.log.

Sorteio de duração: 100.000 amostras, seed ${sample.Seed}, cada frequência dentro de 6 desvios-padrão binomiais + 2 contagens. Para uma contagem binomial, com variância v=np(1−p) e tolerância t=6√v+2, a desigualdade de Bernstein limita a probabilidade de desvio a 2×exp(−t²/[2(v+t/3)]). Somando os limites das 14 categorias, a probabilidade familiar de rejeição sob sorteios independentes com os pesos corretos é no máximo ${familyBound.toExponential(3)}. A tolerância não exige contagens exatas de RNG. Fronteiras cumulativas são testadas exaustivamente nos 200 valores, independentemente do teste estatístico.

${table(['Dias','Esperado %','Observado %','Contagens'],durationSample.map(r=>[r.Days,r.Percent,fmt(r.ObservedPercent),r.Count]))}

Replay adicional: duas execuções de 2 temporadas, 702 partidas cada, seed 199813, hash ${replay.First}. Canonização exclui identidades aleatórias de mundo/partida/episódio e horário de auditoria, mantendo jogadores, clubes, força, datas, planos de lesão e eventos. Persistência/reprocessamento do mesmo mundo é verificada separadamente com todas as identidades reais.
`);
const functional=[
 ['1–7','Força 1/50, quatro divisões/oito lugares, empates de teto, DIV1 terceiro e DIV4 líder','ApprovedCeilingsForAllEightPlaces; NonParticipantCanReachApprovedCeiling; CeilingNeverClampsAStrongNonparticipant'],
 ['8–12','Mudança de posição, turnos, promoção e rebaixamento','OfficialRoundWaitsForAllResultsAndCapturesBothHalvesBeforePromotion; 100RealSeasonsAndPopulationBalance'],
 ['13–16','Banco sem entrada, titular, substituto e perda por participação','ActualMinutesDistinguishUnusedBenchLateEntryAndZeroMinuteEntry; ParticipantCanMaintainOrLoseOneButNeverReceivesReserveGain'],
 ['17','Taça, prorrogação e pênaltis sem ganho','EveryCupStageForbidsReserveGrowth; 100RealSeasonsAndPopulationBalance'],
 ['18–23','Lesão 2/8/15, parcela diária, máximo e abaixo do máximo','DailyLossIsGradualExactAndRecoversOnVirtualDate; PartialAndZeroInjuryLossDoNotBecomeMaximumOrAutomaticRestoration; DurationAndLossLotteryCanReachMaximumAndAlsoChooseSmallerLosses'],
 ['24–26','Lesionados DIV1 líder, DIV4 líder e DIV3 último','InjuredNonparticipantStillReceivesEligibleLeagueGrowth'],
 ['27–29','Ganho/perda na mesma data, recuperação e reincidência','SameDateDailyLossPrecedesReserveGainAndReplayDoesNotRepeatEither; PartialAndZeroInjuryLossDoNotBecomeMaximumOrAutomaticRestoration'],
 ['30–32','Goleiros, transferência e histórico','100RealSeasonsAndPopulationBalance (probes G/A); InjuryTransferKeepsInitialLossBudgetAndHistoricalClubs; SqlitePersistsDailyLossDatesRecoveryAndHistoryWithoutReapplying'],
 ['33–35','Persistência, idempotência e determinismo','ReprocessingAndReloadPreserveEventsAndDisableOldSeasonalPolicy; SqlitePersistsDailyLossDatesRecoveryAndHistoryWithoutReapplying; DeterministicReplayTwoRealSeasons'],
 ['36','Multiplayer','NewRoomsUseOneAuthoritativeVersionAndConcurrentAdvanceDoesNotRepeatDays; IdleCupCoachCanBeReadyWithoutFieldingInjuredPlayersOrAdvancingTheirDays; Multiplayer_100ConsecutiveRounds_8Coaches_SyncCounter_Pauses_Substitutions_Reconnection_AndRoomIsolation']];
save('FORCE_REGRESSION_TESTS_012C.md',`
# Regressão da força adaptada — 012C

STATUS: PASS. Evidências reais, geradas localmente sem publicar em produção.

- dotnet build Elifoot.slnx -c Release: zero avisos e zero erros.
- dotnet test Elifoot.slnx -c Release --no-build: ${cases.size} testes aprovados, zero falhas/ignorados; mesmo build Release imediatamente anterior. Logs build-final.log e regression-final.log.
- flutter analyze: No issues found.
- flutter test: 54 aprovados. Inclui interface do histórico em 320/390/1280 px e treinador sem jogo com elenco lesionado.
- 100 temporadas no modo clássico: 35.100 partidas minuto a minuto; 100.000 sorteios de duração; replay real duas vezes; demais simulações e matrizes antigas também executadas pela suíte completa.

${table(['Artefato TRX final','Casos'],projects.map(r=>[r.File,r.Cases]))}

## Matriz dos 36 itens solicitados

Os intervalos abaixo agrupam itens relacionados; os métodos e parametrizações estão nos fontes de teste e nos TRX finais. Os testes novos somam 48 casos sobre os 276 existentes.

${table(['Itens','Cobertura','Testes'],functional)}

Extras: fronteiras exatas dos 200 valores cumulativos; monotonicidade da chance de reserva; bloqueio de versão desconhecida; save antigo sem modo permanece legado; bloqueio de avanço de dias com jogo ao vivo; ocorrência original da lesão e força imediata preservadas; jogador lesionado não atua nos minutos seguintes; orçamento diário mantém base inicial após transferência; reprocessamento não dispara evolução sazonal; calendário adia elenco curto; humano sem partida confirma pronto sem escalar lesionado.

A regressão existente cobre rankings/gols/artilharia/prêmios, carreira/moral/convites/demissões, mercado/economia/estádios, oito técnicos por 100 rodadas com pausa global/substituições/reconexão e isolamento de salas. O modo legado repetiu exatamente a distribuição e assinatura das 100 temporadas 012/012B. A fórmula defensiva foi comparada diretamente com o checkpoint e permanece byte a byte idêntica.

As primeiras execuções acharam dois problemas reais: elenco curto por lesões (calibration-first.log) e validação de escalação de humano sem partida (regression.log). Foram corrigidos e os ensaios finais repetidos integralmente. targeted-tests.log também preserva uma falha corrigida na preparação de um fixture de teste (busca de atacante após reduzir o elenco); não se mistura esse log com o resultado final.

Reprodução PowerShell, na raiz do projeto:

~~~powershell
$env:ELIFOOT_AUDIT_OUTPUT='D:\\elifoot_98\\docs\\audit-012c\\regression'
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
`);
save('KNOWN_ISSUES_012C.md',`
# Limites conhecidos — 012C

STATUS: PASS dos requisitos implementados/testados. Sem falha funcional conhecida pendente na suíte final.

1. É uma adaptação autorizada. As missões 012/012B continuam válidas como pesquisa/caracterização, com seus limites de evidência histórica. Não se obteve a fórmula original exata.
2. O calendário é uma convenção nova determinística: 4/3 dias, 21 de transição e adiamento global por elenco mínimo lesionado. Não reproduz um calendário civil anual completo. Houve ${delayDays.length} adiamentos em 100 temporadas. A política preserva sincronismo, confrontos e IA de mercado, mas deve ser observada em jogos humanos com elencos muito curtos. Suspensões e regras emergenciais legadas não foram redesenhadas.
3. A distribuição perde oferta de atletas fortes a longo prazo (${endMarket.PlayersAtLeast40} força ≥40 no fim do ensaio). Jogadores usados frequentemente tendem a manter/perder; distritais só disputam Taça e não têm recuperação de força por liga. Esses efeitos são apresentados no relatório de balanceamento. A regra permite crescimento até 50, confirmado em teste controlado, mas a IA não prioriza descanso para desenvolver atletas fortes. Nenhum teto/peso foi alterado.
4. Uma seed com todos IA e oito probes força 1 não cobre todas as estratégias humanas ou seeds. Recomenda-se ensaio adicional de elenco voltado à reserva e monitoramento da oferta por divisão antes de eventual ajuste autorizado da curva de ganho/perda. Não há justificativa para mudar os pesos de duração aprovados.
5. Histórico, episódios, chaves de partidas e snapshots são retidos para garantir auditoria/idempotência e crescem com temporadas. Não se implementou expurgo/paginação nova; avaliar arquivamento compatível antes de mundos de duração extrema. A política de retenção da missão 012 permanece.
6. Saves antigos e importação de pacotes preservam o modo legado; não existe migração automática nem botão para converter uma sala em andamento. Novas salas pela API usam ClassicAdapted por padrão e podem solicitar LegacySeasonal. Versão desconhecida é recusada para impedir fórmula silenciosamente diferente.
7. A UI mostra dias, data virtual, teto e motivos; a autoridade continua no servidor. Os testes multiplayer incluem requisições concorrentes, snapshots e 100 rodadas/8 técnicos. Não foi feito novo ensaio manual com oito dispositivos físicos nem publicação em produção.

NEXT_RECOMMENDATIONS: acompanhar equilíbrio em estratégia humana, avaliar retenção do histórico e só então planejar migração explícita de saves. Nada foi publicado e nenhum outro projeto foi modificado.
`);
summary.GeneratedAtUtc=new Date().toISOString();
summary.FinalFileHashes=[...changed,...summary.NewFiles].map(Path=>({Path,Sha256:hash(join(root,Path))}));
summary.EvidenceHashes=['build-final.log','regression-final.log','flutter-analyze.log','flutter-tests.log',
  'regression/classic-force-012c/100-seasons.json','regression/classic-force-012c/force-events.jsonl.gz',
  'regression/classic-force-012c/injuries.jsonl.gz','regression/classic-force-012c/replay.json',
  'regression/classic-force-012c/weighted-duration-sample.json',...testFiles.map(f=>'tests/'+f)]
  .map(Path=>({Path,Sha256:hash(join(audit,Path))}));
json(join(audit,'verification.json'),summary);
console.log(JSON.stringify({Status:summary.Status,DotnetPassingCases:cases.size,FlutterPassingCases:54,Matches:data.TotalMatchesCompleted,
  HistoryEvents:events.length,LegacyPreserved:legacyPreserved,DefensePreserved:true,Comparison:comparison,Calendar:summary.CalendarPostponements},null,2));
