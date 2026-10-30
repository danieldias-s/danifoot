import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';

// Derive the delivered reports from retained test artifacts, never synthetic results.
const root=path.resolve(import.meta.dirname,'..');
const docs=path.join(root,'docs');
const audit=path.join(docs,'audit-008');
const read=p=>fs.readFileSync(path.join(root,p),'utf8').replace(/^\uFEFF/,'');
const json=p=>JSON.parse(read(p));
const hash=b=>crypto.createHash('sha256').update(b).digest('hex').toUpperCase();
const slash=p=>p.replaceAll('\\','/');
const write=(name,text)=>fs.writeFileSync(path.join(docs,name),text.trim()+'\n');
const table=(headers,rows)=>'| '+headers.join(' | ')+' |\n| '+headers.map(()=> '---').join(' | ')+' |\n'+rows.map(r=>'| '+r.join(' | ')+' |').join('\n');
const s=json('docs/audit-008/career-final-evidence/COACH_CAREER_50_SEASONS_008.json');
const prior=json('docs/audit-008/final-regression/COACH_CAREER_50_SEASONS_008.json');
const web=json('docs/audit-008/web-validation.json');
const baseline=json('docs/audit-008/baseline-manifest.json');
assert.equal(s.Signature,prior.Signature);
assert.equal(s.Matches,17550);assert.equal(s.Titles.length,250);assert.equal(s.ReplayIdentical,true);
assert.equal(web.ServerRestart,'PASS');assert.equal(web.Points,13);assert.equal(web.Morale,71);

const trx=fs.readdirSync(path.join(audit,'final-tests')).filter(f=>f.endsWith('.trx')).map(f=>{
  const text=read('docs/audit-008/final-tests/'+f);
  const counters=text.match(/<Counters\b[^>]*>/)[0];
  const attrs=Object.fromEntries([...counters.matchAll(/(\w+)="(\d+)"/g)].map(m=>[m[1],+m[2]]));
  assert.equal(attrs.failed,0);assert.equal(attrs.total,attrs.passed);
  return {file:f,...attrs};
});
assert.equal(trx.reduce((n,t)=>n+t.passed,0),94);
for(const [p,n] of [['closure-domain-tests/career32-final.trx',32],['closure-integration-tests/career7-final.trx',7],['closure-simulation-tests/career50-final.trx',1]]){
  const t=read('docs/audit-008/'+p);assert.match(t,new RegExp(`<Counters total="${n}" executed="${n}" passed="${n}" failed="0"`));
}
assert.match(read('docs/audit-008/flutter-tests.log'),/\+10: All tests passed!/);
assert.match(read('docs/audit-008/flutter-analyze.log'),/No issues found!/);

const changes=baseline.map(e=>({...e,Path:slash(e.Path),CurrentSHA256:hash(fs.readFileSync(path.join(root,e.Path)))})).filter(e=>e.SHA256!==e.CurrentSHA256);
const protectedPaths=['src/Elifoot.Domain/MatchEngine.cs','src/Elifoot.Domain/AiManagerBrain.cs','src/Elifoot.Domain/MarketAndEconomyEngine.cs','src/Elifoot.Domain/TacticsAndSquadRules.cs','src/Elifoot.Contracts/Enums.cs'];
const protectedFiles=protectedPaths.map(p=>{
  const old=baseline.find(e=>slash(e.Path)===p);assert.ok(old,p);
  const current=hash(fs.readFileSync(path.join(root,p)));assert.equal(current,old.SHA256,p);
  return {Path:p,SHA256:current,Unchanged:true};
});
function sectionProof(file,start,end){
  const extract=text=>{const a=text.indexOf(start);assert.ok(a>=0);const b=text.indexOf(end,a+start.length);assert.ok(b>a);return text.slice(a,b).replaceAll(/\s+/g,'');};
  const old=extract(read('docs/audit-008/baseline/'+file));const current=extract(read(file));
  assert.equal(current,old,start);return {Path:file,Section:start,SHA256Normalized:hash(current),Unchanged:true};
}
const protectedSections=[
  sectionProof('src/Elifoot.Domain/Entities.cs','public class Player','public class AiManager'),
  sectionProof('src/Elifoot.Domain/Entities.cs','public class Stadium','public class Player'),
  sectionProof('src/Elifoot.Domain/CompetitionAndSeasonEngine.cs','private static void AwardDivisionPlacementQuotas','private static void EvolvePlayersAtSeasonEnd'),
  sectionProof('src/Elifoot.Domain/CompetitionAndSeasonEngine.cs','private static void EvolvePlayersAtSeasonEnd','public static void DismissHumanCoach'),
  sectionProof('src/Elifoot.Api/Program.cs','app.MapGet("/api/worlds/{worldId:guid}/top-scorers"','app.MapGet("/api/worlds/{worldId:guid}/ranking"')
];
const newFiles=['src/Elifoot.Domain/CoachCareerEngine.cs','tests/Elifoot.Domain.Tests/CoachCareer008Tests.cs','tests/Elifoot.Application.Tests/CoachCareer008ReadinessTests.cs','tests/Elifoot.Integration.Tests/CoachCareer008IntegrationTests.cs','tests/Elifoot.Simulation.Tests/CoachCareer008SimulationTests.cs','client/elifoot_flutter/test/career_api_test.dart','scripts/report-coach-career-008.mjs'];
const descriptions={
  'src/Elifoot.Domain/Entities.cs':'Estado persistente de carreira, moral, títulos, vagas e pontos decimal.',
  'src/Elifoot.Domain/CompetitionAndSeasonEngine.cs':'Liquidação idempotente, tabela completa antes da moral, títulos das quatro divisões e ranking.',
  'src/Elifoot.Domain/WorldSeedFactory.cs':'Inicializa a versão 008 no mundo novo.',
  'src/Elifoot.Application/MultiplayerRoundOrchestrator.cs':'Snapshot no início, exclusividade, estado pronto e validação final da escalação.',
  'src/Elifoot.Application/ClubAndMarketApplicationService.cs':'Responder e consultar carreira sob o lock da sala; configurar moral.',
  'src/Elifoot.Api/Program.cs':'Recusa HTTP e configuração de faixas; consulta de carreira.',
  'src/Elifoot.Infrastructure/EfWorldRepository.cs':'Migração após desserializar snapshots legados.',
  'src/Elifoot.Contracts/Dtos.cs':'Pontos decimal e dados de moral/projeto nos contratos.',
  'client/elifoot_flutter/lib/main.dart':'Moral, histórico, propostas e botões ACEITAR/RECUSAR.',
  'client/elifoot_flutter/lib/api_client.dart':'Cliente HTTP da recusa.',
  'client/elifoot_flutter/test/widget_test.dart':'As três faixas no fluxo existente de interface.',
  'tests/Elifoot.Simulation.Tests/MultiSeasonAndBenchmarkTests.cs':'Atribuição ao técnico inicial e revalidação das escalações após o mercado de todos os agentes.'
};
const dismissals={};for(const d of s.Dismissals)dismissals[d.Description]=(dismissals[d.Description]??0)+1;
const examples=[s.Decisions.find(d=>d.IsAccepted&&d.IsAiCandidate),s.Decisions.find(d=>d.IsRejected&&d.IsAiCandidate),s.Decisions.find(d=>d.IsAccepted&&!d.IsAiCandidate),s.Decisions.find(d=>d.IsRejected&&!d.IsAiCandidate)];
const rankNames=new Map(s.Ranking.map(r=>[r.CoachId,r.Name]));
const sampleRows=examples.map(d=>[d.IsAiCandidate?'IA':'Humano',rankNames.get(d.CoachId)??d.CoachId,`T${d.CreatedAtSeason} R${d.CreatedAtRound}`,d.IsAccepted?'ACEITOU':'RECUSOU',d.Explanation.replaceAll('|','/'),d.Id]);
const statuses={STATUS:'PASS',MORALE:'PASS',RED_MORALE_DISMISSAL:'PASS',DISTRICT_DISMISSAL:'PASS',UNEMPLOYED_RECOVERY:'PASS',RANKING:'PASS',LEAGUE_TITLES_PRIORITY:'PASS',CUP_TITLES_PRIORITY:'PASS',CAREER_POINTS:'PASS',DIVISION_POINTS:'PASS',CUP_PHASE_POINTS:'PASS',INVITATIONS:'PASS',RANK_EVALUATION:'PASS',MORALE_REQUIREMENT:'PASS',CLUB_ATTRACTIVENESS:'PASS',HUMAN_ACCEPT_REJECT:'PASS',AI_ACCEPT_REJECT:'PASS',NEXT_CANDIDATE_AFTER_REFUSAL:'PASS',AI_MANAGER_TRANSFERS:'PASS',CAREER_PERSISTENCE:'PASS',MULTIPLAYER_REGRESSION:'PASS',DATABASE_MIGRATION:'PASS — JSON versionado; sem DDL',WEB_INTERFACE:'PASS — navegador real, partida aos 90 e reinício real',TESTS:'94 .NET + 10 Flutter; analyze/build aprovados',FILES_CHANGED:`${changes.length} arquivos existentes; 7 arquivos novos de código/testes/relatório; 5 documentos finais e evidências`,KNOWN_ISSUES:'Limites de migração histórica e heurística de projeto documentados; sem bloqueio aberto da tarefa 008.'};
const links='[Moral](COACH_MORALE_RULES_008.md) · [Convites e IA](COACH_INVITATION_AI_008.md) · [Ranking](COACH_RANKING_008.md) · [Testes e reprodução](COACH_CAREER_TESTS_008.md)';

write('COACH_CAREER_AUDIT_008.md',`
# Auditoria de carreira 008

30/10/2026 — D:\\elifoot_98 — DEV privado. STATUS: **PASS**.

Carreira de humanos e IA implementada e executada: moral, demissão, desemprego, convites, mudança de clube, pontos fracionários exatos e títulos. Regressão: **94 testes .NET e 10 Flutter aprovados**. Foram simuladas **50 temporadas reais, ${s.Matches} partidas e ${s.Minutes} minutos**, com 128 clubes e oito humanos. O navegador executou uma partida completa e recuperou a mesma carreira após reinício do processo da API.

${links}

## Antes das alterações

O registro [BASELINE_RULES.md](audit-008/BASELINE_RULES.md) foi criado antes da implementação. Não há repositório Git neste diretório. Foram preservados 70 arquivos e seus SHA256 no [manifesto inicial](audit-008/baseline-manifest.json), com cópias em audit-008/baseline.

${table(['Área','Regra encontrada','Regra final'],[
['Moral','Não existia.','0–100, três faixas configuráveis e histórico.'],
['Demissão','Humano ao cair para Distrital; IA rebaixada rotacionava.','Moral vermelha no início + derrota; Distrital independente da moral; IA rebaixada abre vaga.'],
['Pontos','Inteiros por mando: vitória 2/3, empate 1/2, inclusive Taça.','decimal por divisão e fase; crédito ao treinador registrado no início.'],
['Ranking','Campeonatos > Taças > pontos; depois vitórias e humano.','Mesma prioridade principal; desempate final exclusivamente por CoachId.'],
['Títulos','Só Primeira; Taça podia usar campeão da liga como fallback.','Quatro divisões; Taça somente por final válida; conquista com dono e chave única.'],
['Convites','Desempregados, até três, sem moral, recusa ou projeto.','Empregados/desempregados verdes, projeto e experiência; recusa busca próximo; interino mantém jogos.'],
['Vínculos','Aceite podia deixar humano e IA simultâneos e histórico aberto.','Exclusividade e fechamento do emprego anterior sob lock da sala.'],
['Interface','Ranking e ASSUMIR CLUBE.','Moral e histórico visíveis; comparação da proposta; ACEITAR/RECUSAR.']])}

## Resultado medido

${table(['Métrica','Execução final'],[
['Seed / temporadas / clubes / humanos',`${s.Seed} / ${s.Seasons} / ${s.Clubs} / ${s.Humans}`],
['Partidas / minutos',`${s.Matches} / ${s.Minutes}`],
['Convites humanos aceitos / recusados',`${s.HumanAccepts} / ${s.HumanRefusals}`],
['Convites IA aceitos / recusados',`${s.AiAccepts} / ${s.AiRefusals}`],
['Demissões por moral vermelha + derrota',dismissals.RED_MORALE_LOSS],
['Humanos rebaixados da Quarta para Distrital',dismissals['Demitido por rebaixamento da 4ª Divisão para Distrital']],
['Demissões IA por rebaixamento',dismissals.AI_RELEGATION],
['Total de registros de demissão',s.Dismissals.length],
['Títulos',`${s.Titles.length}: 200 campeonatos e 50 Taças`],
['Replay','Uma temporada executada duas vezes com assinatura idêntica; as duas execuções finais de 50 temporadas também coincidiram.']])}

Fonte integral: [COACH_CAREER_50_SEASONS_008.json](audit-008/career-final-evidence/COACH_CAREER_50_SEASONS_008.json). As recusas IA incluem candidatos avaliados e rejeitados por projeto/incompatibilidade; não equivalem a partidas ou pessoas únicas. A política dos humanos automatizados aceita deterministicamente 75% das oportunidades consideradas, usando a mesma rotina de resposta da aplicação.

Assinatura dos resultados e estado final de carreira: **${s.Signature}**. Não inclui timestamps/GUIDs de auditoria sem significado para o jogo.

## Navegador real

Sala privada em http://127.0.0.1:8088/, API em http://127.0.0.1:5088/. Banco isolado e IDs em [browser-used-fixture.json](audit-008/browser-used-fixture.json); processos em [dev-session.json](audit-008/dev-session.json). A aba original do usuário não foi utilizada para alterar a partida existente.

1. Entrou como Carreira 008 desempregado, com duas ofertas reais no banco de QA.
2. Clicou RECUSAR para Ferroviária e ACEITAR para Joinville Tricolor. Recarregou e preservou o clube.
3. Conferiu verde e, temporariamente, amarelo/vermelho por configuração das faixas no servidor. As regras padrão 60/30 foram restauradas antes de jogar.
4. Enviou a tática, viu 1/1, disputou o primeiro tempo, clicou PRONTO para o segundo e concluiu os 90 minutos: **Ferroviária 0–0 Joinville**.
5. Os pontos passaram de **12,5 para 13** e a moral de **70 para 71**, com registro T1 R3.
6. O processo da API foi encerrado e reiniciado contra o mesmo SQLite. Recarregou, restaurou sessão e confirmou clube, pontos e histórico. A API foi novamente atualizada para o build final e a página foi conferida.

Os valores iniciais de um campeonato, uma Taça e 12,5 pontos foram preparados na fixture para verificar preservação. Não são conquistas obtidas nessa partida no navegador. A sobrevivência de ofertas ainda pendentes foi verificada separadamente por integração SQLite após limpar o cache e recarregar; no reinício visual as ofertas já estavam respondidas.

![Carreira após partida completa e API final](audit-008/web-final.jpg)

Capturas: [ofertas](audit-008/web-invitations.jpg), [recusa](audit-008/web-rejected.jpg), [aceite](audit-008/web-accepted.jpg), [reconexão](audit-008/web-reconnected.jpg), [amarelo configurado](audit-008/web-yellow-configured.jpg), [vermelho configurado](audit-008/web-red-configured.jpg), [partida no intervalo](audit-008/web-live-match.jpg), [resultado aos 90](audit-008/web-match-finished.jpg), [reinício](audit-008/web-server-restarted.jpg). Em amarelo/vermelho, moral 70 usa limiares temporários; com os padrões, 70 é verde. [Validação estruturada](audit-008/web-validation.json).

## Correções adicionais sustentadas por reprodução

- A moral variava 79/80 ao inverter a ordem dos mesmos jogos: a tabela era atualizada parcialmente durante a avaliação. A liquidação passou a atualizar toda a tabela primeiro e depois as carreiras. [Falha original](audit-008/morale-order-reproduction.log), teste determinístico aprovado.
- Um agente comprava titular de outro humano depois da seleção deste, deixando dez titulares no harness. [Reprodução](audit-008/lineup-reproduction.log). O harness agora revalida todos após todas as ações de mercado.
- A mesma janela existia no orquestrador real entre envio e início: [reprodução](audit-008/ready-lineup-reproduction.log). Antes de iniciar, valida todas as escalações enviadas; se um titular saiu, retira o pronto e solicita novo envio. Modo automático recompõe primeiro. Não houve mudança na regra de transferência, tática ou substituição.
- IA recém-demitida e ainda verde podia reassumir imediatamente como interina. O interino também respeita duas rodadas após demissão.
- Byes terminados sem minuto disputado não geram pontos, estatística ou título; final sem vencedor não inventa campeão.

Logs de tentativas anteriores com falhas foram preservados. O resultado final é o da [regressão completa](audit-008/final-tests.log), seguido pelos testes de fechamento. O último ajuste de produção somente tornou explícita a razão EXPERIENCE_FOR_DIVISION na recusa; a execução de 50 temporadas repetida manteve exatamente a assinatura.

## Migração e persistência

O mundo já é persistido como snapshot JSON no repositório EF. A versão CareerRulesVersion=8 é inicializada no mundo novo e aplicada após desserializar mundo legado em cada caminho de leitura. Não foi necessário alterar tabelas SQL. CoachPoints passou de int para decimal; os inteiros legados são representados exatamente.

Preservam-se IDs, empregos existentes, títulos agregados, estatísticas, pontos e personalidade. Moral ausente começa em 70; histórico de moral ausente permanece vazio. Para vínculo sem histórico, cria-se registro de continuidade a partir da temporada/rodada da migração, sem inventar a data original. Jogos antigos já terminados são marcados como aplicados para evitar crédito duplicado. Corrige-se vínculo simultâneo humano/IA e humano em Distrital. Convite antigo viável recebe chave de vaga e validade de três rodadas; clube humano ou Distrital invalida a oferta. O aceite revalida a elegibilidade atual.

Teste de legado remove os campos 008 de um JSON real, carrega-o, migra duas vezes e aceita convite antigo: **42 pontos, três campeonatos e duas Taças preservados**, sem reprocessar partida passada. Testes SQLite verificam pontos, ranking, moral e ofertas; a suíte anterior também executou persistência PostgreSQL 18.

Limite explícito: os totais antigos não permitem reconstruir exatamente a nova pontuação por divisão, donos de títulos antigos ou moral anterior. **Não há recálculo retroativo**; o saldo anterior é mantido e as regras novas passam a valer para jogos novos. O registro detalhado de conquistas começa na 008; agregados legados continuam no ranking. Faça backup do banco/snapshot completo antes de abrir saves importantes. Pacote do editor de clubes/jogadores cria conteúdo de mundo; não substitui backup completo de carreira.

## Preservação e arquivos

Não houve investigação Android nem publicação. Motor, defesa, mercado, economia e táticas foram preservados; a evolução de força e o ranking de jogadores não foram alterados. As mudanças 007 no ledger e métricas foram mantidas. [Prova de preservação](audit-008/preservation.json), [diff dos arquivos existentes](audit-008/source-diff.patch) e [manifesto final](audit-008/evidence-manifest.json).

${table(['Arquivo existente alterado','Finalidade'],changes.map(c=>[c.Path,descriptions[c.Path]??'Integração de carreira 008.']))}

Novos arquivos de código, testes e geração:

${newFiles.map(p=>'- '+p).join('\n')}

Além desses arquivos, foram criados os cinco relatórios COACH_*_008.md, baseline, logs, TRX, JSON e capturas em docs/audit-008. Binários e SQLite de QA ficam em artifacts/coach-career-008.

## Limites restantes

- Migração preserva agregados históricos; não inventa o detalhamento que faltava.
- A atratividade usa uma heurística determinística e explicável; a tarefa não demonstra que os pesos sejam o ótimo de balanceamento para todo save.
- A primeira escolha de clube continua livre entre os clubes elegíveis, conforme o fluxo existente; um humano já empregado/demitido deve usar convites nas mudanças seguintes.
- Interinos novos podem elevar a população total de treinadores; a invariável é 128 clubes com exatamente um titular cada. O ranking final tem ${s.Ranking.length} entradas elegíveis, sem exigir 128 treinadores permanentes.
- Esta tarefa não reabre o bloqueio Android nem declara runtime Android aprovado.

## Relatório final

${table(['Campo','Resultado'],Object.entries(statuses))}
`);

write('COACH_MORALE_RULES_008.md',`
# Moral e demissão 008

30/10/2026. STATUS: PASS. Fonte: src/Elifoot.Domain/CoachCareerEngine.cs e CoachMoraleRules em Entities.cs.

## Faixas e configuração

Moral inteira limitada a 0–100, inicial 70. Verde: 60–100; amarela: 30–59; vermelha: 0–29. Comparação inclusiva no mínimo da faixa. O cabeçalho e a aba de carreira exibem número, texto e cor; o histórico persiste todos os eventos e mostra os cinco mais recentes na UI.

O anfitrião pode usar PUT /api/worlds/{worldId}/career/morale-rules?coachId={hostId}, com JSON {"greenMinimum":60,"yellowMinimum":30,"unemployedRecoveryPerRound":6}. Somente PREPARING, RESULTS ou FINISHED. Validação: 1 ≤ yellowMinimum < greenMinimum ≤ 100 e recuperação de 1–20. As regras pertencem à sala e persistem. Alterar faixas reclassifica a moral existente; não muda o valor numérico.

## Fórmula por partida

gap = média de força de todo o elenco do clube − média de força de todo o elenco adversário. A média não altera a força de jogador algum.

Favorito: gap ≥ 6 OU reputação ≥ reputação adversária +15. Azarão: gap ≤ −6 OU reputação +15 ≤ adversária. Se prestígio e elenco produzirem sinais opostos, prevalece o ramo favorito, explicitamente. Demais casos: equilibrado.

${table(['Resultado','Favorito','Equilibrado','Azarão'],[['Vitória','+6','+9','+13'],['Empate','−3','+1','+4'],['Derrota','−14','−9','−4']])}

1. Vitória zera a sequência sem vencer; empate/derrota incrementa.
2. Sem vitória, subtrair min(4, max(0, sequênciaSemVitória−2)).
3. Derrota por três gols ou mais subtrai mais 2.
4. Com três ou mais jogos de liga: posição esperada ordena forçaMédia +0,15×reputação, desempate DeterministicIndex. Posição real usa a tabela com todos os resultados da rodada já aplicados. Se não venceu e está mais de duas posições abaixo do esperado, −2. Se não perdeu e está mais de duas posições acima do esperado, +1.
5. moralFinal = clamp(moralAnterior + delta, 0, 100).

Os últimos cinco resultados são registrados como +1/0/−1. A sequência sem vitória é o modificador recente da fórmula; o somatório recent5 serve ao histórico, sem um segundo peso oculto. Taça usa o vencedor incluindo prorrogação/pênaltis. Preliminares têm zero pontos de carreira, embora uma partida efetivamente disputada possa alterar moral e estatísticas da IA distrital.

O registro guarda temporada, rodada, MatchId, antes/depois e motivo com resultado, gap, sequência e pontos. A tabela é liquidada em duas passagens para não depender da ordem dos jogos. CareerResultsApplied impede reprocessamento após replay/reload.

## Demissão

CoachId e moral são capturados uma vez ANTES do início e persistidos no fixture. Se moralNoInício < yellowMinimum E o resultado é derrota, a demissão ocorre depois de encerrada a partida. Entrar no vermelho por essa primeira derrota não dispara a demissão se o início ainda era amarelo/verde. Vitória ou empate em vermelho não dispara esta regra.

${table(['Caso determinístico','Resultado'],[['60, derrota 0–1 equilibrada','51, amarela, continua.'],['35, derrota 0–1 equilibrada','26, vermelha, continua.'],['20, derrota 0–1 equilibrada','11, demitido uma vez.'],['20, vitória 1–0 equilibrada','29, continua mesmo ainda vermelha.'],['55, vitória 1–0 equilibrada','64, verde.'],['20, empate 0–0 equilibrado','21, continua.']])}

Na demissão, fecha emprego e limpa o vínculo, escalação enviada e pronto humano; preserva treinador, moral, personalidade IA, pontos, títulos e ranking. Cria vaga e interino. Repetir a liquidação/demissão não duplica o contador. Se um treinador já saiu por outra razão, o desligamento não remove o novo treinador do clube.

Humano rebaixado Quarta→Distrital é demitido independentemente da moral. A IA mantém o clube jogável. Promovido Distrital→Quarta volta a ser elegível; não reconecta automaticamente o antigo humano. IA rebaixada conserva a regra anterior de desligamento por rebaixamento, agora com vaga e substituição explicável em vez de rotação cega.

## Desemprego

Uma vez por rodada subsequente: moral = min(mínimoVerde, moral+6). Se já estava acima do mínimo verde, não reduzir. Sem recuperação na rodada da demissão; polling/reload não acelera. Exemplo: 20→26→32→38→44→50→56→60 em sete rodadas posteriores. Desemprego não remove conquistas.

Ainda verde, recém-demitido aguarda duas rodadas antes de novo convite ou nomeação interina. Sem experiência (nenhum campeonato, <60 pontos E <15 vitórias), desempregado só é elegível para Terceira/Quarta; IA pode também trabalhar em Distrital. Isso limita convite imediato de elite sem criar desemprego permanente. Humano desempregado continua participando do contador de preparação.

Provas: MoraleBandsAndPreMatchRedDismissal (seis casos), StrongFavoriteDemandsMoreThanUnderdog_AndRecentPoorFormMatters, MoraleUsesCompletedRoundTableRegardlessOfFixtureProcessingOrder, UnemployedRecoveryIsGradualOncePerRoundAndNeverDeletesCareer, DistrictRelegationDismissesRegardlessOfMorale_AndPromotedClubIsEligible e DismissedAiCannotImmediatelyReappointItselfAsInterim. [TRX final](audit-008/closure-domain-tests/career32-final.trx). [Auditoria](COACH_CAREER_AUDIT_008.md).
`);

write('COACH_INVITATION_AI_008.md',`
# Convites e troca de treinador 008

30/10/2026. STATUS: PASS. Humanos decidem por ACEITAR/RECUSAR; IA avalia deterministicamente o projeto. O ranking oficial permanece lexicográfico; a nota de seleção de uma vaga é outra métrica.

## Projeto e compatibilidade

Divisão numerada 1–5, sendo 5 Distrital. Índice arredondado a três casas:

project = 12×(5−divisão) +0,30×reputação +0,60×forçaMédia +3×log10(max(1,caixa)) +2×capacidadeConstruída/10000.

Usa caixa atual, capacidade construída e elenco completo, sem alterar finanças, estádio ou jogador. A posição atual é exibida na proposta e protege o líder de elite; não entra como coeficiente geral da nota de projeto. Expectativa competitiva é refletida na divisão/qualidade/prestígio e no objetivo exibido.

Pré-requisito: moral VERDE e outro clube. Humano nunca Distrital. Desempregado recém-demitido espera duas rodadas; para Primeira/Segunda precisa de ≥1 campeonato OU ≥60 pontos OU ≥15 vitórias. Sem isso, Terceira/Quarta; IA também Distrital. Um desempregado legado pode não ter data de demissão conhecida: não se inventa cooldown histórico.

Empregado espera três rodadas desde a mudança e requer melhoria de projeto ≥ 6 + Pragmatism/25 (humano usa 50, portanto mínimo 8). Líder da Primeira, com ao menos um jogo e reputação ≥75 exige melhoria ≥16. Também precisa de divisão superior OU melhoria estrutural simultânea: reputação +15, estádio ≥1,3× e caixa ≥1,3×. Não basta uma boa colocação passageira. Um líder pequeno pode aceitar clube muito maior da mesma divisão; um líder grande não sai para projeto inferior/equivalente. Pragmatism influencia a cautela da IA; identidade tática e demais traços são preservados na mudança.

## Ordem de candidatos

Para N entradas do ranking, posição rank (1 é melhor):

score = 20×(N−rank)/max(1,N) + moral/10 + (temCampeonato ? 4 : 0) + min(10,pontos/50) − 3×distânciaEntreDivisões.

Desempregado usa distância zero. Maior nota primeiro; CoachId desempata. Uma boa carreira ajuda, mas um candidato no topo pode recusar projeto ruim. Amarelos/vermelhos não recebem oferta. A explicação CANDIDATE_HIGH_RANK registra a posição numérica avaliada, sem significar que todo candidato tenha classificação absoluta de elite.

## Ciclo da vaga

Vaga tem ID determinístico com seed, clube, ordinal e sequência. Convite tem ID derivado da vaga+candidato. São consultados os candidatos já tentados nessa vaga; não há reoferta infinita. IA incompatível registra recusa e segue para o próximo. Primeiro humano elegível recebe oferta e a busca aguarda sua resposta. Máximo três pendentes por humano e uma oferta ativa por vaga. Validade: três rodadas (ordinal atual < vencimento). Mudança de moral/projeto ou expiração invalida a pendência e permite prosseguir.

Humano recusa: registra CANDIDATE_REJECTED, mantém seu clube/carreira e busca o próximo imediatamente. Sem candidatos, interino permanece e a busca pode ser reavaliada na próxima rodada, respeitando a lista já tentada. Chamadas repetidas na mesma rodada não reiniciam a pesquisa; recusa explícita libera a próxima tentativa. Interino pode ser IA verde livre fora do cooldown ou treinador novo determinístico. Ele não interrompe calendário, mercado ou preparação.

Aceite humano/IA: fecha emprego anterior, remove técnico substituído, cria novo emprego, preserva moral, pontos, títulos e personalidade; zera só a sequência e os cinco resultados do trabalho anterior. Expira demais propostas da mesma vaga ou do mesmo treinador. O clube anterior abre vaga com interino. Nunca há dois titulares. A troca só ocorre em PREPARING/RESULTS/FINISHED; nunca durante simulação/pausa/intervalo. O serviço HTTP usa o mesmo SemaphoreSlim por mundo do relógio; revalida após adquirir o lock, salva, incrementa a versão e publica contador/snapshot.

Primeira entrada ainda permite escolher clube não distrital, conforme o fluxo aprovado. Treinador com carreira estabelecida não usa reconexão/join para pular convites. Um interino não precisa ser contratado como titular para o clube continuar jogando.

## API e interface

- GET /api/worlds/{worldId}/invitations/{coachId}: pendências, emprego, moral/histórico, índice atual, títulos e regras.
- POST /api/worlds/{worldId}/invitations/{invitationId}/accept?coachId={id}.
- POST /api/worlds/{worldId}/invitations/{invitationId}/reject?coachId={id}.

A proposta mostra clube, divisão, posição, força média, caixa, estádio, objetivo e comparação dos índices. Botões ficam protegidos contra comando duplicado; após resposta, recarrega sessão, plantel e snapshot e limpa seleção antiga de jogador. As validações do servidor também rejeitam repetição, vaga encerrada e disputa simultânea.

## Exemplos reais da execução final

${table(['Candidato','Nome','Momento','Decisão','Explicação registrada','InvitationId'],sampleRows)}

Para a mesma primeira vaga, candidato de posição 2 rejeitou melhoria −23; depois posição 16 aceitou melhoria +22,582. Isso demonstra por que a seleção não se reduz a contratar o primeiro colocado. Registros originais estão no [JSON integral](audit-008/career-final-evidence/COACH_CAREER_50_SEASONS_008.json). Houve ${s.AiAccepts} aceites e ${s.AiRefusals} recusas IA; humanos ${s.HumanAccepts}/${s.HumanRefusals}. Recusa humana registrada em AuditLogs tem motivo Recusa humana; a Explanation do convite mantém o diagnóstico original do projeto.

O teste SearchContinuesAfterAiRefusalAndPreservesPersonalityOnAcceptedMove verifica o clube anterior com substituto, exclusividade e igualdade da identidade, pragmatismo, adaptabilidade, agressividade financeira, paciência com jovens e preferências. O teste HTTP de recusa verifica uma nova oferta para o próximo humano na mesma vaga. Aceitação concorrente aceita exatamente uma solicitação; mudança em RUNNING_FIRST_HALF é recusada explicitamente. [TRX integração](audit-008/closure-integration-tests/career7-final.trx).

No navegador, houve recusa Ferroviária e aceite Joinville, seguido de partida real e reinício. [Capturas e procedimento](COACH_CAREER_TESTS_008.md). Pesos são uma política explicável e testada, sem alegação de ótimo estatístico universal.
`);

const comparisons=s.SameResultsLegacyComparison.filter(c=>c.Name.startsWith('Carreira '));
write('COACH_RANKING_008.md',`
# Ranking e pontuação de treinadores 008

30/10/2026. STATUS: PASS. O ranking de jogadores/artilheiros não foi modificado.

## Ordenação

Comparar a tupla (campeonatos DESC, Taças DESC, pontosCarreira DESC, CoachId ASC), lexicograficamente. Não existe soma ponderada entre título e ponto. Vitórias e ser humano não são mais desempates. ID persistente dá ordem estável mesmo sem clube. Os pontos são decimal no domínio/DTO/JSON e aceitam 0,5/1,5 exatamente no servidor; a UI apresenta o valor recebido sem conversão para inteiro.

Exemplos verificados: (1 campeonato,0 Taças,10 pontos) fica acima de (0,10,5000); com campeonatos iguais, mais Taças vence quaisquer pontos; (2,1,60) fica acima de (2,1,50). Demissão/reload/troca preservam a tupla. IA distrital que já tem campeonato não perde lugar no ranking por estar na Distrital. Treinadores sem atividade/título podem seguir os filtros históricos de população do ranking; não confundir entradas com total de técnicos existentes.

## Pontos de liga

${table(['Divisão','Vitória','Empate','Derrota'],[['Primeira',4,2,0],['Segunda',3,'1,5',0],['Terceira',2,1,0],['Quarta',1,'0,5',0],['Distrital',0,0,0]])}

Para divisões 1–4: vitória = 5−divisão; empate = (5−divisão)/2m. A classificação da liga continua 3 pontos por vitória e 1 por empate, como antes; não usa esses pontos de carreira. HomeCoachAtKickoffId/AwayCoachAtKickoffId recebem o crédito mesmo que o vínculo tenha mudado antes da liquidação. CareerResultsApplied persistido impede duplicação de estatísticas, moral e pontos.

## Taça

${table(['Fase vencida','Pontos'],[['64',3],['32',4],['Oitavas',5],['Quartas',6],['Semifinal',7],['Final',8],['Preliminares distritais',0]])}

Seis vitórias = 33 pontos e +1 Taça em campo separado. WinnerClubId resolve vitória por prorrogação/pênaltis, inclusive empate no placar de gols. Derrotado recebe zero. Bye sem minuto jogado não dá prêmio; final sem vencedor válido não inventa título. Não há fallback do campeão da liga para a Taça.

## Campeonatos e dono do título

Cada uma das quatro divisões vale um campeonato. Conclusão exige todos os fixtures de liga da divisão encerrados e aplicados. O campeão vem da classificação existente. A regra explícita de responsabilidade é: **treinador capturado no início da última partida de liga do campeão**, ordenada por rodada e DeterministicMatchIndex. O nome atual do técnico não substitui o dono registrado. Se sair após esse último jogo antes da liquidação, recebe a conquista por essa regra; quem treinou apenas partidas anteriores não recebe o título retroativamente.

Registro: temporada, divisão, clube, treinador e tipo LEAGUE/CUP. Chave de idempotência: temporada+divisão+tipo (Taça tem divisão nula). A final credita ao treinador vencedor capturado no início. Testes encerram as quatro divisões, repetem a liquidação e verificam quatro títulos, sem duplicar. Nas 50 temporadas houve exatamente 200 campeonatos e 50 Taças.

## Comparação com as regras antigas sobre os mesmos resultados

Antes: pontos por mando (casa vitória2/empate1; fora vitória3/empate2), inclusive Taça, sem prêmio de fase; apenas Primeira gerava campeonato. O teste calculou a seguinte comparação a partir das mesmas partidas realmente simuladas e dos mesmos treinadores ao início:

${table(['Humano','Pontos novos','Pontos por mando antigos','Campeonatos novos (4 divisões)','Só títulos da Primeira','Taças'],comparisons.map(c=>[c.Name,c.NewPoints,c.LegacyMandoPoints,c.NewLeagueTitles,c.LegacyFirstDivisionTitles,c.CupTitles]))}

Essa é uma comparação de fórmula sobre resultados iguais. Não é uma segunda trajetória de contratações com o código antigo nem permite atribuir diferenças de carreira exclusivamente a uma regra. O JSON integral também inclui todos os técnicos IA para comparação. A prioridade campeonatos/Taças/pontos já existia e foi preservada; completaram-se a contabilização, atribuição e estabilidade.

Top 5 final:

${table(['#','Treinador','Campeonatos','Taças','Pontos'],s.Ranking.slice(0,5).map(r=>[r.Rank,r.Name,r.LeagueTitles,r.CupTitles,r.CoachPoints]))}

## Saves antigos

Agregados inteiros anteriores passam a decimal sem perda; não são recalculados com regra nova. Jogos terminados na migração ficam aplicados. Títulos legados mantêm seus contadores, sem inventar registros com divisão/dono desconhecidos. Dados detalhados novos começam na versão 8. [Migração e evidências](COACH_CAREER_AUDIT_008.md), [teste JSON legado](audit-008/closure-domain-tests/career32-final.trx).
`);

const mapping=[
['1–6','Moral cai/sobe, verde→amarelo→vermelho, vermelho perde/vence','MoraleBandsAndPreMatchRedDismissal (6 casos)'],
['7','Demissão idempotente','MoraleBandsAndPreMatchRedDismissal + replay'],
['8','Desempregado mantém ranking','RankingIsLexicographicAndTiesAreStableAcrossReloadAndUnemployment'],
['9','Recuperação gradual','UnemployedRecoveryIsGradualOncePerRoundAndNeverDeletesCareer'],
['10–12','Só verde recebe/aceita','OnlyGreenHumanReceivesOpportunity (3) + PendingOfferCannotBeAcceptedAfterMoraleFallsBelowGreen (2)'],
['13–14','Elite recusa; líder pequeno aceita estrutura superior','EliteLeaderRejectsLowerProjectButSmallLeaderCanAcceptGreatStructure'],
['15','Próximo após recusa','SearchContinuesAfterAiRefusalAndPreservesPersonalityOnAcceptedMove + HTTP recusa oferece ao próximo humano'],
['16–17','Humano aceita/recusa','HumanCanAcceptOrRejectThroughHttpAndReceivesUpdatedOwnership (2) + navegador'],
['18–19','IA muda e preserva personalidade','SearchContinuesAfterAiRefusalAndPreservesPersonalityOnAcceptedMove'],
['20–21','Distrital demite; promovido fica elegível','DistrictRelegationDismissesRegardlessOfMorale_AndPromotedClubIsEligible'],
['22–24','Prioridade campeonatos/Taças/pontos','RankingIsLexicographicAndTiesAreStableAcrossReloadAndUnemployment'],
['25','Valores de divisão','ExactDecimalDivisionPoints (4)'],
['26–27','Fases e pênaltis','CupPhaseWinAndPenaltiesCreditOnce (6) + SixCupWinsEqual33AndTitleIsSeparate_NoByeTitle'],
['28','Títulos não duplicam','EveryCompletedDivisionAwardsOneTitleToItsClosingMatchCoach + testes Taça'],
['29','Pontos não duplicam','CreditBelongsToKickoffCoachEvenAfterClubChanges_AndReplayReloadIsIdempotent + 50 temporadas'],
['30','Ranking após reinicialização','SqliteRestartPreservesRankingPendingOffersAndCareer_AndWritesPrivateBrowserFixture + reinício real da API'],
['31','Multiplayer sincronizado','Suíte anterior API/SignalR, 2/4/6/8 clientes, pausa/substituições/reconexão'],
['32','Aceite simultâneo','ConcurrentAcceptancesCannotDuplicateClubOrCoach_AndInFlightChangesAreRejected'],
['33','Ofertas persistem','SqliteRestartPreservesRankingPendingOffersAndCareer_AndWritesPrivateBrowserFixture (cache limpo, disco)'],
['34','Desempregado no contador','HostCanConfigureMoraleBandsWithValidationAndUnemployedCountsInReadyCounter']
];
write('COACH_CAREER_TESTS_008.md',`
# Testes e reprodução 008

30/10/2026. STATUS: PASS. Logs brutos e tentativas com falhas foram preservados.

## Resultado final

${table(['Suíte','Aprovados','Falhas'],[['Domínio',66,0],['Aplicação',5,0],['Integração',18,0],['Simulação',5,0],['Total .NET',94,0],['Flutter',10,0]])}

Compilação Release sem erros/avisos; Flutter analyze sem problemas; build web Release aprovado. .NET SDK 10.0.302; Flutter 3.38.5, Dart 3.10.4; Node v24.20.0. Não foram atualizadas dependências para esta tarefa.

Baseline007 tinha 52 casos .NET e oito Flutter. A 008 adiciona 32 de domínio, um de aplicação, sete de integração e dois de simulação = 42, total 94. Flutter acrescenta dois testes API, e estende o teste visual existente às três faixas = dez. Número de cenários não equivale ao número de métodos: theories expandem parâmetros.

[Log final .NET](audit-008/final-tests.log) e [TRX completos](audit-008/final-tests/). A matriz existente, regressão longa de 100 temporadas e cenários humanos/stress/mercado também foram executados nessa suíte; seus artefatos foram redirecionados a audit-008/final-regression para preservar os relatórios 005/006/007. Os testes antigos de SignalR usam clientes independentes 2/4/6/8, pausa global, substituições e desconexão/reconexão. PostgreSQL 18 também foi exercitado pelo teste existente de persistência.

Após a regressão completa, o último ajuste de produção corrigiu apenas o texto da razão de candidato sem experiência. Reexecutados os 32 casos de domínio e as 50 temporadas; a assinatura final coincidiu integralmente. As asserções de migração JSON legado, próximo humano após recusa e bloqueio durante partida foram fortalecidas; domínio32 e integração7 passaram novamente. Não se soma essas repetições aos 94 casos únicos.

- [Domínio32](audit-008/closure-domain-tests.log), [TRX](audit-008/closure-domain-tests/career32-final.trx).
- [Integração7](audit-008/closure-integration-tests.log), [TRX](audit-008/closure-integration-tests/career7-final.trx).
- [Carreira50 final](audit-008/closure-career50.log), [TRX](audit-008/closure-simulation-tests/career50-final.trx).
- [Flutter analyze](audit-008/flutter-analyze.log), [Flutter10](audit-008/flutter-tests.log), [web build](audit-008/web-build.log), [API final](audit-008/api-closure-build.log).

## Cobertura dos 34 itens exigidos

${table(['Itens do pedido','Verificação','Teste/evidência'],mapping)}

Também há testes para bye sem minuto, final sem campeão inventado, título em todas as divisões, crédito ao treinador no início após troca, migração repetida sem histórico fictício, IA impedida de reassumir imediatamente, ordem de jogos sem efeito na moral e venda de titular já confirmado. Essas verificações exercitam limites reais além da lista do pedido.

## Execução de 50 temporadas

Seed199801, 128 clubes, oito humanos, calendário e MatchSimulationEngine reais, sem placares agregados artificiais. A cada rodada: convites/respostas, montagem de escalações, partidas minuto a minuto, finanças, carreira e mercado. Foram verificadas propriedade exclusiva em todos os clubes, humano fora da Distrital, moral0–100, reprocessamento sem novos pontos/títulos e cinco títulos por temporada.

${s.Matches} partidas, ${s.Minutes} minutos; ${s.HumanAccepts}/${s.HumanRefusals} aceites/recusas humanos; ${s.AiAccepts}/${s.AiRefusals} IA; ${s.Dismissals.length} demissões; ${s.Titles.length} títulos. O relatório contém cada convite, demissão, título, ranking e comparação de pontuação.

Assinatura50: ${s.Signature}. Replay de uma temporada duas vezes: ${s.ReplayOneSeasonSignature}. A segunda execução final das 50 temporadas também teve a mesma assinatura50. Timestamps de auditoria não compõem a assinatura. [JSON final](audit-008/career-final-evidence/COACH_CAREER_50_SEASONS_008.json).

## Validação visual e persistência real

O navegador fez login, entrou desempregado, recusou Ferroviária, aceitou Joinville, recarregou e verificou histórico/clube. Com as faixas padrão restauradas, enviou tática, confirmou segundo tempo e terminou 0–0 aos90. Pontos12,5→13 e moral70→71. Reiniciar o processo da API e recarregar preservou tudo. A versão compilada final foi carregada e novamente conferida. [AX final](audit-008/web-final-ax.txt), [resposta da API final](audit-008/web-final-api.json), [validação estruturada](audit-008/web-validation.json).

![Propostas reais no navegador](audit-008/web-invitations.jpg)

![Partida concluída aos 90 minutos](audit-008/web-match-finished.jpg)

![Carreira restaurada na API final](audit-008/web-final.jpg)

As capturas amarela/vermelha usaram limiares temporários via API do anfitrião, para testar a renderização sem forjar derrotas. Não demonstram uma série de derrotas na sessão visual; essa transição foi exercitada deterministicamente nos testes. Títulos iniciais foram semeados, não conquistados na sessão. A validação visual foi desktop; o teste Flutter existente também verificou o fluxo de largura390. Não se declara validação Android nesta tarefa.

## Comandos reproduzíveis (PowerShell)

Executados a partir de D:\\elifoot_98. Use uma nova pasta de execução para não sobrescrever as evidências entregues:

~~~powershell
Set-Location D:\\elifoot_98
$run008 = Join-Path (Get-Location) ('artifacts\\coach-career-008\\rerun-' + (Get-Date -Format yyyyMMdd-HHmmss))
New-Item -ItemType Directory -Path $run008 -Force | Out-Null
$env:ELIFOOT_AUDIT_OUTPUT = $run008
dotnet build Elifoot.slnx -c Release
dotnet test Elifoot.slnx -c Release --no-build --logger 'trx;LogFileName=rerun008.trx' --results-directory $run008
dotnet test tests/Elifoot.Domain.Tests -c Release --filter FullyQualifiedName~CoachCareer008Tests
dotnet test tests/Elifoot.Integration.Tests -c Release --filter FullyQualifiedName~CoachCareer008IntegrationTests
dotnet test tests/Elifoot.Simulation.Tests -c Release --filter FullyQualifiedName~CoachCareer008_FiftyRealSeasonsAndDeterministicReplay
Push-Location client\\elifoot_flutter
flutter analyze
flutter test
flutter build web --release --dart-define=API_BASE_URL=http://127.0.0.1:5088 --output=..\\..\\artifacts\\coach-career-008\\web
Pop-Location
~~~

A suíte de integração cria fixture SQLite nova e atualiza browser-fixture.json; a fixture efetivamente usada na validação entregue foi congelada em browser-used-fixture.json. Não substitua esse arquivo histórico. O teste existente PostgreSQL requer o servidor DEV local configurado, como na execução aprovada.

Para abrir a mesma sessão entregue, a API5088 e o servidor web8088 ficaram em execução privada. Para reiniciar quando as portas estiverem livres, consulte dev-session.json e use o mesmo banco. Não encerre processos alheios se houver conflito:

~~~powershell
$session008 = Get-Content docs\\audit-008\\dev-session.json -Raw | ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT = 'LocalDev'
$env:Elifoot__DatabaseProvider = 'Sqlite'
$env:Elifoot__AutoSeedDefaultWorld = 'false'
$env:ConnectionStrings__DefaultConnection = "Data Source=$($session008.Database)"
Start-Process dotnet -ArgumentList @('D:\\elifoot_98\\src\\Elifoot.Api\\bin\\Release\\net10.0\\Elifoot.Api.dll','--urls','http://127.0.0.1:5088') -WorkingDirectory D:\\elifoot_98\\src\\Elifoot.Api -WindowStyle Hidden
Start-Process node -ArgumentList @('D:\\elifoot_98\\scripts\\serve-web.mjs','8088','D:\\elifoot_98\\artifacts\\coach-career-008\\web') -WorkingDirectory D:\\elifoot_98 -WindowStyle Hidden
~~~

Abra http://127.0.0.1:8088/. Identidade de QA: Carreira 008 / qa008-web. A sala já está no resultado da rodada3; a carreira tem Joinville,13 pontos,71 moral. Para recriar o cenário de propostas iniciais, use a nova fixture produzida pelo teste SQLite, não edite a evidência já concluída.

Geração destes cinco documentos e verificação dos artefatos:

~~~powershell
node scripts/report-coach-career-008.mjs
~~~

O gerador confere os TRX, total94, Flutter10, assinaturas de50 temporadas e arquivos protegidos. Registra SHA256 do código alterado, novos testes e evidências. Logs api-live/web.stdout e metadados de processos ativos são mutáveis e excluídos do manifesto estático. O SQLite vivo também não é tratado como arquivo estático; os snapshots de validação são as evidências estáveis.
`);

fs.writeFileSync(path.join(audit,'preservation.json'),JSON.stringify({BaselineFiles:baseline.length,ProtectedFiles:protectedFiles,ProtectedSections:protectedSections,ExistingFilesChanged:changes,NewSourceTestAndReportFiles:newFiles},null,2)+'\n');
let diff='';for(const c of changes){const r=spawnSync('git',['diff','--no-index','--no-ext-diff','--',path.join(audit,'baseline',c.Path),path.join(root,c.Path)],{encoding:'utf8',maxBuffer:10*1024*1024});assert.ok(r.status===0||r.status===1,`${c.Path}: ${r.stderr}`);diff+=r.stdout;}
fs.writeFileSync(path.join(audit,'source-diff.patch'),diff);
const files=[];
function walk(dir){for(const item of fs.readdirSync(dir,{withFileTypes:true})){const p=path.join(dir,item.name);if(item.isDirectory()){if(item.name!=='baseline')walk(p);}else if(!['evidence-manifest.json','dev-session.json','api-live.stdout.log','api-live.stderr.log','web.stdout.log','web.stderr.log','browser-fixture.json'].includes(item.name)){files.push(p);}}}
walk(audit);
for(const f of [...changes.map(c=>c.Path),...newFiles,...['COACH_CAREER_AUDIT_008.md','COACH_MORALE_RULES_008.md','COACH_INVITATION_AI_008.md','COACH_RANKING_008.md','COACH_CAREER_TESTS_008.md'].map(f=>'docs/'+f)])files.push(path.join(root,f));
const manifest={Task:'ELIFOOT_COACH_CAREER_MORALE_RANK_008',Status:statuses,GeneratedUtc:new Date().toISOString(),DotnetTests:94,FlutterTests:10,Signature50:s.Signature,ProtectedFiles:protectedFiles,MutableExclusions:['dev-session.json','browser-fixture.json','api-live.*.log','web.*.log','live SQLite database'],Files:[...new Set(files)].sort().map(f=>({Path:slash(path.relative(root,f)),SHA256:hash(fs.readFileSync(f)),Bytes:fs.statSync(f).size}))};
fs.writeFileSync(path.join(audit,'evidence-manifest.json'),JSON.stringify(manifest,null,2)+'\n');
console.log(JSON.stringify({Status:'PASS',Dotnet:94,Flutter:10,Seasons:s.Seasons,Matches:s.Matches,Signature:s.Signature,ExistingFilesChanged:changes.length,ProtectedFiles:protectedFiles.length,ProtectedSections:protectedSections.length,ManifestEntries:manifest.Files.length},null,2));
