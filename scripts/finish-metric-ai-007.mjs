import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';

const root=path.resolve(import.meta.dirname,'..'),docs=path.join(root,'docs'),audit=path.join(docs,'audit-007');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const json=n=>read(path.join(docs,n));
const write=(n,s)=>fs.writeFileSync(path.join(docs,n),s+'\n');
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex').toUpperCase();
const tr=json('TRANSFER_RECONCILIATION_007.json'),ai=json('AI_ADAPTABILITY_007.json'),di=json('PLAYER_BEHAVIOR_DISCIPLINE_007.json'),fi=json('FINANCIAL_RECOVERY_007.json'),ec=json('ECONOMIC_INTEGRITY_007.json');
const baseline=read(path.join(audit,'baseline-manifest.json'));
const changed=baseline.filter(r=>sha(path.join(root,r.Path))!==r.SHA256).map(r=>r.Path);
const engine='src/Elifoot.Domain/MatchEngine.cs';
const before=baseline.find(r=>r.Path.replaceAll('\\','/')===engine).SHA256;
assert.equal(sha(path.join(root,engine)),before);
assert.equal(changed.filter(p=>/_(005|006)\.(json|md)$/.test(p)).length,0);
const countTrx=dir=>fs.readdirSync(dir).filter(f=>f.endsWith('.trx')).flatMap(f=>{
  const xml=fs.readFileSync(path.join(dir,f),'utf8');
  return [...xml.matchAll(/<UnitTestResult\b[^>]*testName="([^"]+)"[^>]*outcome="([^"]+)"/g)].map(m=>({name:m[1],outcome:m[2],file:path.relative(root,path.join(dir,f))}));
});
const closure=countTrx(path.join(audit,'closure-tests'));
const sim=countTrx(path.join(audit,'final-tests')).filter(t=>t.name.includes('MultiSeasonAndBenchmarkTests'));
assert.equal(closure.length,49);assert.equal(sim.length,3);
assert(closure.concat(sim).every(t=>t.outcome==='Passed'));
assert(fs.readFileSync(path.join(audit,'flutter-tests.log'),'utf8').includes('+8: All tests passed!'));
assert(fs.readFileSync(path.join(audit,'flutter-analyze.log'),'utf8').includes('No issues found!'));
assert.equal(tr.Candidate.Reconciliation.Delta,0);assert.equal(ec.Candidate.RoundCashVsLedgerMismatches,0);
fs.writeFileSync(path.join(audit,'verification.json'),JSON.stringify({status:'PARTIAL',date:'2026-10-30',
  engineSha256:before,changed,tests:{dotnet:52,flutter:8,results:closure.concat(sim)},
  protectedOriginal005006Reports:true,limitations:['Individual non-card foul attribution absent from original engine','Historical stage counters unavailable before007','Live snapshot is not a complete career archive']},null,2));
let diff='';for(const p of changed) {
  const result=spawnSync('git',['diff','--no-index','--no-ext-diff','--',path.join(audit,'baseline',p),path.join(root,p)],{encoding:'utf8',cwd:root});
  assert([0,1].includes(result.status));diff+=result.stdout;
}
fs.writeFileSync(path.join(audit,'source-diff.patch'),diff);
const money=n=>n.toLocaleString('pt-BR',{minimumFractionDigits:2,maximumFractionDigits:2});
const behaviorOrder=['FairPlay','Cordeirinho','Cavalheiro','Caneleiro','Caceteiro','Sarrafeiro'];
const behaviorRows=behaviorOrder.map(b=>{const r=di.Baseline.ByBehavior.find(r=>r.Behavior===b);return `| ${b} | ${r.Minutes} | ${r.Appearances} | ${r.Yellows} | ${r.DirectReds} | ${r.SecondYellowReds} | ${r.SuspensionAwards} | ${r.RedsPer10000Minutes.toFixed(4)} |`;}).join('\n');
const financialRows=fi.Baseline.Clubs.map(c=>{const e=c.Episodes[0];return `| ${c.Name} | ${e.Entry.Season}/${e.Entry.Round} | ${money(e.Entry.Cash)} | ${e.Recovery?.ElapsedRounds??'não recuperou'} | ${e.PrimaryMechanism} | ${c.Relapses} | ${money(c.FinalCash)} |`;}).join('\n');
const sourceNew=['src/Elifoot.Domain/AiMarketMetrics.cs','tests/Elifoot.Domain.Tests/MetricAiAudit007Tests.cs','tests/Elifoot.Integration.Tests/FinancialPersistence007Tests.cs','tests/Elifoot.Simulation.Tests/MetricAudit007Capture.cs','scripts/report-metric-ai-007.mjs','scripts/finish-metric-ai-007.mjs'];

write('METRIC_AI_AUDIT_007.md',`# Auditoria de métricas, IA, disciplina e recuperação financeira — 007

Data: 30/10/2026, America/Sao_Paulo. Projeto: D:\\elifoot_98. **STATUS: PARTIAL**. As correções comprovadas e a regressão passaram. A cobertura da auditoria tem limites explícitos: o motor não atribui todas as faltas a jogadores; eventos de tentativa anteriores a007 não existiam; um snapshot recente não equivale à carreira inteira.

## Baseline, preservação e população

Git: \`git status --short\` e \`git log -3 --oneline\` retornaram “not a git repository”. Não houve reset, checkout, commit ou sobrescrita de mudanças alheias. Foram copiados75 arquivos de código/documentação antes de alterações, com SHA256 em [baseline-manifest.json](audit-007/baseline-manifest.json). Configurações e bancos DEV existentes não foram substituídos. Os relatórios005/006 originais continuam com seus hashes originais; resultados reexecutados foram direcionados por \`ELIFOOT_AUDIT_OUTPUT\` para subpastas próprias.

Baseline reproduzida: seed199801, temporadas1–100,128 clubes,128 treinadores IA,32 clubes de liga por temporada e96 distritais,2200 rodadas,35100 partidas reais. As contagens de100 temporadas não descrevem uma temporada isolada nem apenas os32 treinadores inicialmente nas ligas. Os27 testes anteriores passaram antes das correções. .NET SDK10.0.302; Flutter3.38.5; Dart3.10.4.

## Defeitos e decisões

| ID | Classe | Evidência | Tratamento |
|---|---|---|---|
| METRIC-007-01 | METRIC_ERROR | 8800 bilaterais comparadas a médias que incluem24525 leilões | Separar mecanismos na origem do coletor; projeção dos nove indicadores a partir de fatos únicos |
| BUG-007-01 | BUG | Controle0–100,0–3 aos83: abaixo65 não reage; acima65 escolhe segunda preferência defensiva | Reação gradual por placar/tempo, somente preferências próprias adequadas e banco disponível; cooldown10min |
| BUG-007-02 | BUG | Recarregar JSON elimina cache ignorado na serialização e repete liquidação | Guard de rodada persistido; reconstrução para saves006 com patrocínio completo; contratos, lesões, suspensões, empréstimos e obras testados |
| BUG-007-03 | BUG | SQLite: primeira gravação esperava256 lançamentos, tinha200 | Remover TakeLast(200); gravar todos os novos lançamentos; repetir save sem duplicar |
| HARDENING-007-01 | BUG / prevenção por invariante | Código permitia débito mesmo se a perna de crédito já existisse | Verificar ambas as chaves antes da transferência; teste de perna pré-existente |
| DOC-007-01 | DOCUMENTATION_ERROR | Pesos disciplinares e patrocínios descritos diferiam do código006 reproduzido | Corrigir documentação atual, preservar os relatórios históricos |
| DESIGN-007-01 | DESIGN_CHOICE | Fair Play tem peso0 documentado; demais eventos não geram cartões | Preservar; não inventar regra do executável histórico |
| DESIGN-007-02 | DESIGN_CHOICE | Patrocínio nas22 rodadas; salários apenas com jogo | Preservar valores; tornar dependência e recaídas transparentes |

O limiar65 foi uma escolha **intencional e documentada** em005, BALANCEAMENTO-002. Sua intenção era preservar identidade. O defeito reproduzido foi o comportamento extremo e a escolha cega de índice1, não simplesmente a existência de um limiar. A correção mantém as formações favoritas e seus pesos, sem mudar a escolha pré-jogo nem sortear novas preferências.

## Reconciliação das compras

Fonte: [TRANSFER_RECONCILIATION_007.json](TRANSFER_RECONCILIATION_007.json); registros canônicos completos no ledger comprimido indicado nesse JSON. Uma compra é o débito concluído, contado uma vez por FinancialTransaction.Id; a venda correspondente aponta para TransferTransactionId. Lances, tentativas, avaliações, ofertas pendentes, renovações e contratos gratuitos não são compras.

| Período/cenário | Bilaterais | Leilões pagos | Compras concluídas | Diferença contra soma por treinador |
|---|---:|---:|---:|---:|
| Baseline006 reconstituída,100 temporadas | 8800 | 24525 | 33325 | 0 |
| Candidato007, mesmas100 temporadas/seed | 8800 | 23373 | 32173 | 0 |

Na baseline, ofensivos44:12945 compras (3640 bilaterais+9305 leilões), média294,2045; equilibrados42:10314 (2504+7810), média245,5714; defensivos42:10066 (2656+7410), média239,6667. Valores impressos com arredondamento em005/006 não devem ser usados como registros inteiros. Não havia duplicidade: havia diferença de categorias incluídas.

As novas transações capturam PlayerId, BuyerClubId, SellerClubId, ManagerId responsável na execução, TransactionType, Amount, SeasonNumber e RoundNumber. O histórico é atribuído pelo ID imutável do treinador, mesmo que o clube troque de técnico depois. A oferta grava seu autor na submissão; o aceite usa o responsável na execução. A projeção \`AiMarketMetrics.FromRecords\` não mantém contadores paralelos. A transferência paga32173 inclui832 contratações gratuitas **fora dessa soma**: são outras movimentações, explicitamente FreeAgentSigning de valor0.

Indicadores007 nesta população: avaliações8922; tentativas8800; ofertas submetidas0; aceites de ofertas0; compras32173; lances150573; leilões vencidos23373; vendas8800; renovações38289. As ofertas são zero porque o cenário principal tem somente IA; o fluxo IA→humano→aceite foi executado separadamente em teste real do serviço. Tentativa é visita de execução após validações iniciais de identidade/idempotência ou submissão de proposta; uma proposta e sua execução posterior são etapas distintas. As definições precisas estão no JSON. Tentativas e lances de006 são **desconhecidos**, não zero.

## Qualidade das contratações

As32173 compras têm justificativa e snapshot de força, titular/reserva da posição, preço, FairValue, salário, posição, comportamento, estrela, formação e caixa restante. ${tr.Candidate.Quality.belowStarter} têm força inferior à referência titular; isto, isoladamente, não é defeito. As categorias diferenciam cobertura, titular urgente, melhoria titular e oportunidade. Nenhuma excedeu1,25x FairValue; maior razão observada ${tr.Candidate.Quality.maxPriceToFair.toFixed(6)}. O teste de força11 por R$2milhões rejeita a compra com o mercado ativo. Não se alterou ranking de alvos, orçamento, curva de preço ou diversidade estratégica.

A ordenação atual prioriza listados e depois utilidade por preço dentro de até6 candidatos por posição. Isso não prova ótimo global; não foi encontrado um caso reproduzido que justifique trocar essa estratégia. Não se afirma que todos os jogadores não examinados eram alternativas disponíveis/vendáveis equivalentes.

## Adaptabilidade e personalidade

[AI_ADAPTABILITY_007.json](AI_ADAPTABILITY_007.json) responde às dez perguntas e registra33 controles (11 atributos × empate/desvantagem1/desvantagem3) com elencos e seed iguais. Esses são estados controlados de decisão, não resultados de partidas inventados. As alterações de gameplay foram verificadas em100 temporadas pelo motor real.

Perdendo: minuto mínimo \`clamp(84 - A*0.28 - max(0,desvantagem-1)*8,55,84)\`; protegendo vantagem: \`clamp(88 - A*0.14 - max(0,vantagem-1)*3,70,88)\`. A avaliação continua nos checkpoints62/74/83/112 e intervalos; lesões permitem avaliação imediata. Cooldown10min baseado nos eventos efetivamente aplicados. Não acrescenta avaliações a cada minuto. Apenas preferências próprias que respondam ao placar ou reduzam improvisações são elegíveis, com encaixe e quotas. Sem opção adequada, mantém a formação. Não substitui expulso nem repõe a vaga perdida.

64 e65 tomam as mesmas decisões nos controles registrados. A0 já pode reagir à desvantagem3 (no cenário sequencial, aos74); A100 reage antes. Empate não dispara mudança aleatória de formação. Substituição, formação e pré-escalação são indicadores distintos. Houve58 treinadores que mudaram formação em jogo na baseline e128 no candidato, ao longo de100 temporadas, não em todo jogo. Preferências/identidade/atributos continuam na entidade persistente do treinador.

## Disciplina e distribuição das expulsões

O arquivo inteiro MatchEngine.cs é idêntico à baseline: SHA256 **${before}**. Nenhuma probabilidade de gol, peso disciplinar ou fórmula defensiva foi recalibrada nesta missão. A variação observada vem das decisões da IA e de suas consequências ao longo das temporadas.

| Comportamento — baseline100 temporadas | Minutos em campo | Participações com minuto | Amarelos | Diretos | Segundo amarelo | Sanções de suspensão | Vermelhos/10000min |
|---|---:|---:|---:|---:|---:|---:|---:|
${behaviorRows}

Minuto de expulsão conta; reserva entra na exposição a partir do próximo minuto simulado. Suspensões são sanções geradas por expulsão/terceiro amarelo, não jogos efetivamente cumpridos. Fair Play não gera falta intencional/cartão nos mecanismos implementados, mas participa de gols, lesões, impedimentos e substituições. Não existe evento disciplinar de reclamação neste modelo; sua imunidade a tal evento não pode ser testada como se ele existisse.

[PLAYER_BEHAVIOR_DISCIPLINE_007.json](PLAYER_BEHAVIOR_DISCIPLINE_007.json) contém diretos/segundos amarelos, comportamento, formação no evento, divisão, força individual, minuto, mando e ID do jogador, com exposição individual e taxas por comportamento. Baseline5735=3213+2522; candidato5717=3175+2542. Pesos ordenados0/0,35/0,75/1,45/2,35/3,60, com cautela após amarelo. Cordeirinho e Cavalheiro são distintos; Caceteiro/Sarrafeiro têm maior taxa normalizada.

**Limite de atribuição:** faltas sem cartão e pênaltis não escolhem/registram infrator individual. Não é possível calcular sua distribuição exata por comportamento em equipes mistas sem inventar dados ou alterar o motor. Por isso esses campos são null. Executaram-se2400 partidas controladas,400 por comportamento, para medir faltas exatas em equipes homogêneas, mantendo o motor real e as mesmas seeds. A amostra de400 não transforma ausência ocasional de vermelho de Cordeirinho em probabilidade zero.

## Os21 clubes em crise de006

Crise é caixa abaixo deR$150mil no pós-mercado da rodada, com clube nas ligas. Recuperação é atingirR$350mil; não significa falência anterior nem saúde perpétua. A contagem original21/21 foi reproduzida. Há27 episódios recuperados entre esses21 clubes;5 recaíram e16 cumprem a definição observacional de sustentabilidade (nenhuma recaída amostrada, pelo menos22 rodadas posteriores, caixa final≥350mil). Nenhuma previsão após a temporada100 é feita.

| Clube | Primeira crise T/R | Caixa ao entrar | Rodadas até primeira recuperação | Principal receita da primeira recuperação | Recaídas | Caixa final T100 |
|---|---:|---:|---:|---|---:|---:|
${financialRows}

Cada episódio em [FINANCIAL_RECOVERY_007.json](FINANCIAL_RECOVERY_007.json) traz causa observada/fluxos de entrada, receitas e despesas posteriores, vendas identificáveis, folha antes/depois, obras e situação final. Entre27 episódios:23 predominantemente por receitas,2 por premiação e2 por vendas. Uma receita dominante não prova causalidade exclusiva. Na nova trajetória:19 clubes atingiram crise,19 recuperaram pelo menos uma vez,8 recaíram e11 satisfizeram o critério observacional de sustentabilidade.

Patrocínio é receita externa explícita:185/125/92/72mil por rodada nas quatro divisões e20mil no Distrital. Total da baselineR$12.566.400.000 em100 temporadas. Continua sendo pago em22 rodadas, mesmo após eliminação, enquanto salários/contratos exigem participação em jogo. Essa política intencional de005 sustenta caixa e favorece recuperação; não foi identificada transferência bilateral criadora de dinheiro nem crédito emergencial oculto. Não se igualou patrocínio automaticamente a subsídio indevido. O cálculo sem patrocínio no JSON é aritmético com decisões fixas, não outra simulação econômica.

## Conservação, liquidação e estádios

Baseline: R$3.317.455.881,00 debitados e creditados; candidato: R$3.314.681.535,00 em cada lado; diferençaR$0,00 em ambos. Zero IDs/chaves duplicados e zero pares bilaterais ausentes/desbalanceados. Caixa inicial candidatoR$138.800.000,00; finalR$5.664.382.164,15; todos os fluxos e saldos foram reconciliados em centavos inteiros. Saldo inicial é estoque de abertura, não receita operacional. Arremates de atletas sem contrato são saída para fora da economia de clubes, sem vendedor; não devem ser confundidos com transferências conservadas entre dois clubes.

O teste de reload verifica salários, contratos, lesões, suspensões, empréstimos, reforma e ampliação sem processar duas vezes. O teste SQLite verifica256 registros na primeira gravação e idempotência da segunda. A serialização do guard de rodada não exige alterar o schema SQL; metadados novos vivem no snapshot JSON. A tabela relacional conserva os campos financeiros anteriores, agora sem truncar os novos lançamentos.

Estádios: custos fixos/variáveis, manutenção, desgaste, ocupação, condição, reformas e ampliações aparecem no fluxo por clube. O clube controlado em crise não pode iniciar ampliação sem caixa; decisão sazonal respeita seus limiares de caixa/ROI. Não foram alterados coeficientes, capacidade, preço ou política de manutenção. Caixa baixo pode impedir manutenção e deteriorar capacidade; receitas continuadas podem restaurar capacidade financeira. Não se afirma causalidade exclusiva do estádio onde há simultaneamente transferências e receitas.

## Regressão e limitações

**52 casos .NET únicos aprovados**, incluindo os27 anteriores, e8 testes Flutter. Build Release sem avisos/erros; Flutter analyze sem problemas. O total52 é a união dos49 casos não estatísticos do fechamento com os3 estatísticos já aprovados após a correção tática. Após essa execução longa só houve projeção de métricas, persistência SQL, documentação e reforço de testes; o gameplay não mudou novamente. Não foram repetidas100 temporadas para uma remoção de TakeLast na persistência. Consulte [REGRESSION_TESTS_007.md](REGRESSION_TESTS_007.md) para comandos e TRX.

Multiplayer: testes HTTP/SignalR em TestServer com repositório InMemory, salas2/4/6/8, ciclo de100 rodadas, pausa global, quotas e reconexão. SQLite em memória foi executado para persistência financeira. **Não é validação PostgreSQL de produção nem partida Android**. A verificação externa da porta5080 encontrou o servidor parado; os servidores dos testes são efêmeros. O script DEV existente permite iniciar uma sessão local. Não foi substituído seu banco para fazer smoke test.

A primeira captura financeira completa recontou os128 saldos iniciais como fluxo novo e seu assert de instrumentação falhou. Os35100 jogos e arquivos brutos terminaram; a reconciliação independente corrigiu abertura versus fluxo sem reescrever os brutos. O coletor final já exclui a abertura e valida0 divergências. Falha inicial NuGet por sandbox foi resolvida com execução local autorizada. Logs brutos, inclusive reproduções com falha, foram preservados.

## Arquivos e reprodução

Modificados (comparação com cópia original):

${changed.map(p=>'- '+p.replaceAll('\\','/')).join('\n')}

Criados em código/scripts:

${sourceNew.map(p=>'- '+p).join('\n')}

Entregas: este relatório; TRANSFER_RECONCILIATION_007.json; AI_ADAPTABILITY_007.json; PLAYER_BEHAVIOR_DISCIPLINE_007.json; FINANCIAL_RECOVERY_007.json; ECONOMIC_INTEGRITY_007.json; REGRESSION_TESTS_007.md; KNOWN_ISSUES_007.md. Evidências em docs/audit-007, com baseline, logs, TRX, ledger/ações/fluxos comprimidos e verificação de hashes. O arquivo source-diff.patch registra as diferenças nos arquivos existentes.

Próximas recomendações: usar os IDs/eventos e escopo explícito nos relatórios; não interpretar snapshot de duas temporadas como carreira inteira; decidir em tarefa própria se é necessário ampliar retenção consultável; acompanhar patrocínio/caixa e recaídas com playtest antes de ajustar economia; só adicionar atribuição de faltas se houver requisito de produto que justifique alterar telemetria do motor. Nenhuma publicação em produção.

## Relatório final obrigatório

\`\`\`text
STATUS: PARTIAL — limites de dados históricos/granularidade e retenção descritos; correções e regressão PASS
TRANSFER_METRICS: PASS — fatos únicos e mecanismos separados
TOTAL_TRANSFERS: 32173 compras pagas na trajetória007 de100 temporadas;832 movimentações gratuitas separadas
DIRECT_PURCHASES: 7107 lista direta +1693 negociações IA/IA; propostas aceitas0 neste cenário
AUCTION_PURCHASES: 23373
PURCHASE_ATTEMPTS: 8800; avaliações8922; lances150573
PURCHASES_COMPLETED: 32173
RECONCILIATION: PASS —32173 por comprador=32173 por treinador; diferença0; duplicidades0
METRIC_ROOT_CAUSE: total bilateral comparado a médias que também incluíam leilões
AI_ADAPTABILITY: PASS nos controles e100 temporadas reais
THRESHOLD_65: intencional em005; substituído após defeito extremo comprovado
BELOW_65_BEHAVIOR: reage a desvantagem extrema quando há preferência adequada e recursos
ABOVE_65_BEHAVIOR: antecipa reação gradualmente, sem mudança aleatória em empate
TACTICAL_PERSONALITY: preservada; usa somente preferências próprias
FIX_APPLIED: tempo/placar, encaixe, banco, quotas, cooldown10min; nenhuma alteração pré-jogo
DISCIPLINE: pesos/calibração006 preservados; granularidade limitada explicitamente
FAIR_PLAY:0 cartões; regra documentada do projeto
CORDEIRINHO: baseline0,0372 vermelhos/10000min
CAVALHEIRO: baseline0,2065 vermelhos/10000min
CANELEIRO: baseline0,6957 vermelhos/10000min
CACETEIRO: baseline2,3899 vermelhos/10000min
SARRAFEIRO: baseline5,6308 vermelhos/10000min
RED_CARD_DISTRIBUTION: JSON com evento, minuto, mando, jogador, força, divisão e formação
SECOND_YELLOW: PASS;2522 baseline/2542 candidato; suspensão/removal verificados
FINANCE: fluxos reconciliados; patrocínio e recaídas explícitos
CRISIS_CLUBS: baseline21; candidato19 (mesmo seed/período, trajetórias diferentes)
RECOVERED_CLUBS: baseline21; candidato19, ao menos uma vez
RECOVERY_MECHANISMS: baseline27 episódios:23 receitas/2 premiações/2 vendas; sem crédito emergencial oculto
SUSTAINABLE_RECOVERY: baseline16 sem recaída amostrada; candidato11, conforme definição observacional
TRANSFER_MONEY_CONSERVATION: PASS; diferençaR$0,00 em ambas as populações
FINANCIAL_IDEMPOTENCE: PASS; memória/reload/save006/SQLite, com defeitos corrigidos
STADIUM: sistema preservado; clube sem caixa não financia expansão no teste
STADIUM_FINANCIAL_EFFECTS: bilheteria, operação, manutenção e obras rastreadas nos fluxos e capturas
MATCH_ENGINE: real minuto a minuto; código inteiro inalterado
DEFENSE_PRESERVED: PASS, SHA256 idêntico à baseline
DETERMINISTIC_REPLAY: PASS de assinatura de gameplay em duas temporadas iguais por seed
MULTIPLAYER_REGRESSION: PASS HTTP/SignalR/TestServer/InMemory;2/4/6/8 e100 rodadas
DOTNET_TESTS:52 casos únicos PASS (49 fechamento+3 estatísticos pós-fix);27 anteriores preservados
FLUTTER_TESTS:8 PASS; analyze sem problemas
FILES_MODIFIED: lista acima e audit-007/source-diff.patch
FILES_CREATED:6 fontes/scripts listados,8 entregas obrigatórias e evidências audit-007
KNOWN_ISSUES: KNOWN_ISSUES_007.md; não há novo PASS Android/PostgreSQL/visual
NEXT_RECOMMENDATIONS: retenção consultável e atribuição de faltas em escopo próprio; observar economia antes de retocar valores
\`\`\`
`);

write('REGRESSION_TESTS_007.md',`# Regressão007

Data30/10/2026. Build Release PASS,0 avisos/erros. Baseline27/27 .NET PASS. Final52 casos únicos PASS: Domain34, Application4, Integration11, Simulation3. Flutter8/8 PASS e analyze sem problemas. Os27 casos anteriores permanecem; foram acrescentados25 casos (a teoria de11 atributos conta como11).

## Evidências

- Baseline: audit-007/baseline-tests-retry.log e baseline-tests/*.trx.
- Antes da correção: audit-007/reproductions.log e reproductions/reproductions.trx:12 falhas esperadas,11 respostas táticas inadequadas e1 reload de liquidação.
- SQLite antes: audit-007/ledger-first-save-reproduction.log:256 esperados,200 gravados na primeira operação. O teste inicial que verificava só depois de duas gravações não detectava isso; foi fortalecido antes da correção.
- Execução completa pós-correção tática: audit-007/final-tests.log e final-tests/*.trx,50 casos naquela revisão, incluindo os3 estatísticos.
- Fechamento após a correção SQL: audit-007/closure-tests.log e closure-tests/*.trx,49 casos não estatísticos. A união com os3 estatísticos resulta52; não é uma única chamada com52.
- Flutter: audit-007/flutter-analyze.log e flutter-tests.log.
- Verificação: audit-007/verification.json contém cada nome e arquivo TRX, hashes e escopo.

## Comandos executados

PowerShell na raiz D:\\elifoot_98:

\`\`\`powershell
git status --short
git log -3 --oneline
$env:ELIFOOT_AUDIT_OUTPUT='D:\\elifoot_98\\docs\\audit-007\\baseline-run'
dotnet test Elifoot.slnx -c Release --logger 'trx;LogFilePrefix=baseline' --results-directory docs/audit-007/baseline-tests
dotnet test tests/Elifoot.Domain.Tests -c Release --no-restore --filter FullyQualifiedName~MetricAiAudit007Tests
dotnet test tests/Elifoot.Simulation.Tests -c Release --no-restore --filter FullyQualifiedName~Audit007_Reconstruct
$env:ELIFOOT_AUDIT_OUTPUT='D:\\elifoot_98\\docs\\audit-007\\final-run'
dotnet build Elifoot.slnx -c Release
dotnet test Elifoot.slnx -c Release --no-build --logger 'trx;LogFilePrefix=final' --results-directory docs/audit-007/final-tests
dotnet test tests/Elifoot.Integration.Tests -c Release --no-restore --filter FullyQualifiedName~FinancialPersistence007Tests
$env:ELIFOOT_AUDIT_OUTPUT='D:\\elifoot_98\\docs\\audit-007\\closure'
dotnet build Elifoot.slnx -c Release
dotnet test Elifoot.slnx -c Release --no-build --filter 'FullyQualifiedName!~MultiSeasonAndBenchmarkTests' --logger 'trx;LogFilePrefix=closure' --results-directory docs/audit-007/closure-tests
Push-Location client/elifoot_flutter
C:\\flutter\\bin\\flutter.bat analyze
C:\\flutter\\bin\\flutter.bat test
Pop-Location
node scripts/report-metric-ai-007.mjs
node scripts/finish-metric-ai-007.mjs
\`\`\`

Os comandos de reprodução anterior documentam a versão de antes do fix; executar hoje seus testes espera sucesso. Use nova pasta ELIFOOT_AUDIT_OUTPUT para evitar substituir capturas e relatórios históricos. O script de fechamento apenas verifica/evidencia os resultados existentes; ele espera as pastas atuais. Para repetir somente a captura real100 temporadas em pasta nova, execute o teste Audit007_Reconstruct com o ambiente apontando para ela e configure ELIFOOT_007_CANDIDATE no gerador de relatórios.

## Cobertura A–N

| Pedido | Evidência executada |
|---|---|
| A reconciliação | ledger integral baseline/candidato; unicidade, pares e soma por manager |
| B/C responsável e troca de clube | Transfers_ReconcileOnce_AndRetainResponsibleManagerAfterClubChangeAndReload; rotação real anterior mantida |
| D lances versus compras | AuctionBids_AreNotPurchases; fluxo real IA→oferta→humano→aceite |
| E/F adaptabilidade/extremo | teoria0,20,40,50,60,64,65,70,80,90,100; matriz33 estados;100 temporadas reais |
| G personalidade | teste de rotação existente; preferências e identidade conservadas; nenhuma alteração nos atributos |
| H/I/J disciplina/segundo amarelo/suspensão | testes006 preservados;2400 controles homogêneos pelo MatchEngine; exposições de35100 partidas |
| K recuperação |21 clubes baseline com27 episódios;19 na nova trajetória; controle de receita identificada |
| L idempotência | repetição por2200 rodadas; reload JSON completo e compatibilidade006 |
| M conservação | centavos inteiros; débitos/créditos e ambos os guards; SQLite real em memória |
| N estádio em crise | expansão sem caixa rejeitada; decisão sazonal; fluxos de obras registrados |

## Escala das execuções

Antes do fix: suíte006 (100 temporadas principais+25 agentes humanos+25 estresse+4000 matriz+2 replays de1 temporada) e captura independente de100 temporadas. Depois do fix: a mesma suíte006 mais captura007 de100 temporadas e benchmark. O cenário principal do relatório compara apenas as100 temporadas seed199801 de cada lado; não soma esses períodos repetidos como se fossem200 temporadas distintas de uma carreira. Os controles homogêneos têm2400 partidas adicionais, fora da população principal.

Replay determinístico: duas execuções de1 temporada,351 partidas cada, com assinatura de gameplay, resultados, gols, disciplina e caixa iguais. Não se afirma igualdade literal de snapshots que contêm GUIDs e timestamps. A fórmula defensiva e todo MatchEngine.cs preservam SHA256 ${before}.

Multiplayer E2E usa HTTP+SignalR via ASP.NET TestServer/InMemory e os testes anteriores de2/4/6/8 clientes e100 rodadas. Persistência financeira usa SQLite em memória. Nenhum PostgreSQL externo nem runtime Android foi certificado em007. Não houve teste visual de interface novo, pois não houve alteração de UI.
`);

write('KNOWN_ISSUES_007.md',`# Limitações conhecidas007

**STATUS: PARTIAL**, apesar de52 casos .NET e8 Flutter aprovados. Investigações e correções comprovadas concluídas; as limitações abaixo não foram transformadas em estatísticas inventadas.

1. **UNCONFIRMED / dados ausentes:** o motor registra faltas totais por equipe, mas não infrator de falta sem cartão ou pênalti. Nas100 temporadas mistas, faltas por comportamento/jogador são null. Há faltas exatas em2400 controles homogêneos. Não se alterou o motor só para fabricar granularidade retroativa.
2. **Histórico anterior:** tentativas, avaliações, lances e autor da oferta não eram eventos de006. Podem ser medidos a partir de007; não podem ser reconstruídos como números exatos do passado. A baseline dos pagamentos e da atribuição foi capturada a cada rodada antes da rotação, explicitamente identificada como observação, não como campo original persistido.
3. **Retenção:** o mundo remove transações/eventos antigos do snapshot para limitar memória. O ledger financeiro SQL agora persiste todos os lançamentos novos, mas mantém o schema anterior sem metadados completos do treinador/jogador. Para a auditoria100 temporadas, os arquivos ledger.jsonl.gz/actions.jsonl.gz preservam a atribuição porID; um snapshot isolado não é arquivo vitalício da carreira. Não houve migração nem criação de um novo sistema de histórico. Consultas futuras de carreira inteira precisam usar arquivo completo ou definir retenção persistente própria, sem atribuição pelo treinador atual.
4. **BALANCE_ISSUE a acompanhar / DESIGN_CHOICE preservada:** patrocínio em22 rodadas e salários por participação geram fluxo líquido generoso e acúmulo de caixa. A baseline21/21 recuperados incluía5 recaídas; o candidato19/19 inclui8. Não há garantia econômica fora das100 temporadas observadas. Não foram retocados coeficientes sem defeito demonstrado.
5. **Qualidade da IA:** as compras têm razões verificáveis e limites de valor, mas o shortlist/listados-primeiro não é um otimizador global. Não se prova que uma compra abaixo da força titular seja ruim, nem que toda alternativa hipotética fosse vendável. Nenhuma irracionalidade do exemplo força11/R$2milhões foi reproduzida; o controle rejeita esse preço.
6. **Fair Play histórico:** a regra0 é comprovada na documentação/código do projeto. Não foi autenticado o comportamento de um executável proprietário original. Não se adicionou cartão por realismo estatístico.
7. **Ambiente:** não há Git neste diretório; preservação por cópia/hash/diff. O backend DEV persistente não estava ouvindo5080 na verificação; testes usam servidores efêmeros. Não foram sobrescritos bancos para iniciar uma sala. Android segue com as limitações de004, fora desta missão; sem nova declaração de PASS. PostgreSQL e sessão humana visual não foram revalidados.
8. **Instrumentação corrigida:** a primeira captura contabilizou abertura de caixa como receita nova e falhou em128 comparações de fluxo. Os brutos foram preservados; a reconciliação independente separa abertura e movimentos. O coletor final passou com0 diferenças. Isso não foi classificado como defeito do motor.

Corrigidos: definição das compras e separação de leilões; atribuição nova no momento da operação; reação tática extrema e gradual preservando preferências; guarda de liquidação após reload; gravação completa acima de200 lançamentos; documentação desatualizada. Os testes prévios, fórmula defensiva, gols, ranking, ligas, promoções, rebaixamentos e quotas de substituição continuam aprovados.
`);
console.log(JSON.stringify({status:'PARTIAL',dotnet:52,flutter:8,engineSha256:before,changedFiles:changed},null,2));
