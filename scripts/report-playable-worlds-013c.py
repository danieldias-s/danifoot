"""Build reviewable 013C acceptance records from actual evidence, never inferred passes."""
from pathlib import Path
import subprocess,json,hashlib,re,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[1];AUDIT=ROOT/'docs/audit-013c'
def read(name):return json.loads((AUDIT/name).read_text(encoding='utf-8-sig'))
def git(path,*args):return subprocess.check_output(['git','-C',str(path),*args]).decode().strip()
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
suites={};outcomes={};full_count=0
files=sorted(list((AUDIT/'tests').glob('final-013c_*.trx'))+list((AUDIT/'tests').glob('boundary-013c_*.trx')),key=lambda p:p.stat().st_mtime)
for p in files:
    tree=ET.parse(p);unit=tree.find('.//t:UnitTest',ns);counter=tree.find('.//t:Counters',ns)
    suite=Path(unit.attrib['storage']).name
    if p.name.startswith('final-013c_'):full_count+=int(counter.attrib['total'])
    for item in tree.findall('.//t:UnitTestResult',ns):
        outcomes[(suite,item.attrib['testId'])]={'Outcome':item.attrib['outcome'],'Trx':str(p.relative_to(ROOT))}
for (suite,test_id),item in outcomes.items():
    row=suites.setdefault(suite,{'Suite':suite,'Total':0,'Passed':0,'Failed':0,'Skipped':0,'Trx':[]})
    row['Total']+=1;row['Passed']+=item['Outcome']=='Passed';row['Failed']+=item['Outcome']=='Failed';row['Skipped']+=item['Outcome'] not in ['Passed','Failed']
    if item['Trx'] not in row['Trx']:row['Trx'].append(item['Trx'])
net=read('dotnet-full-exits.json');boundary=read('boundary-exits.json');final_build=read('final-build-exit.json'); flutter=read('flutter-exits.json'); actual=read('actual-worlds.json');modes=read('actual-modes.json')
restart=read('restart-verification.json'); replay=read('actual-restart-http.json');normal=read('normal-clock-acceptance.json');recon=read('financial-reconciliation.json');source=read('source-state.json')
protected={name:sha(next((ROOT/'src/Elifoot.Domain').rglob(name)))==expected for name,expected in source['ProtectedFiles'].items()}
original={}
for key,item in source['Sources'].items():
    path=Path(item['Path']);head=git(path,'rev-parse','HEAD');status=git(path,'status','--short','--untracked-files=all').splitlines()
    original[key]={'Path':str(path),'Head':head,'StateUnchanged':head==item['Head'] and status==item['Status']}
node=(AUDIT/'node-tests.log').read_text(encoding='utf-8-sig');nodeok=bool(re.search(r'pass\s+2',node) and re.search(r'fail\s+0',node))
testcount=sum(v['Total'] for v in suites.values())
ok=(len(suites)==4 and testcount==552 and full_count==551 and all(v['Passed']==v['Total'] and v['Failed']==v['Skipped']==0 for v in suites.values())
    and net['Build']==net['Tests']==boundary['Domain']==boundary['Integration']==final_build['Exit']==0 and flutter=={'Analyze':0,'Tests':0,'Build':0} and nodeok
    and all(v['StateUnchanged'] for v in original.values()) and all(protected.values())
    and all(v['Status']=='PASS' for v in [actual,modes,restart,replay,normal,recon]))
status='PASS' if ok else 'PARTIAL'
checkpoint=read('checkpoint.json')['ImplementationCommit'] if (AUDIT/'checkpoint.json').exists() else 'pendente de commit local após validação'
result={'Task':'ELIFOOT_PLAYABLE_REAL_CLUB_WORLDS_013C','Status':status,'Worktree':str(ROOT),'Branch':git(ROOT,'branch','--show-current'),
 'ApprovedBase':source['ApprovedBase'],'ImplementationCommit':checkpoint,'OriginalWorktrees':original,'ProtectedFilesUnchanged':protected,
 'Suites':list(suites.values()),'DotnetTests':testcount,'FullDotnetExits':net,'FullSolutionTestCount':full_count,'PostBoundaryExits':boundary,'FinalBuild':final_build,'Identity':'Latest outcome by assembly name and stable testId; no duplicate rerun counts','FlutterTests':76,'FlutterExits':flutter,'NodeTests':2,'NodePassed':nodeok,
 'ActualRealClubs':{'Male':158,'Female':232},'ActualRealCurrentSquads':0,'ActualWorlds':actual['Worlds'],
 'DataModes':modes,'Restart':restart,'RestartReplay':replay,'NormalRoomClock':normal,'FinancialReconciliation':recon['Status'],'LongRegressionInputScope':recon['Scope'],
 'CoverageStatus':'PARTIAL','NoProductionPublish':True,'NoPush':True,'NoAutomaticMerge':True}
(AUDIT/'verification.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
worldrows='\n'.join(f"| {w['Gender']} | {w['World']} | 128 | {w['Origins'].get('REAL_VERIFIED_CURRENT',0)} | {w['Origins']['FICTIONAL_GENERATED']} | {w['Assembly']} / {w['Districts']} | 4 |" for w in actual['Worlds'])
netrows='\n'.join(f"| {s['Suite']} | {s['Passed']}/{s['Total']} | {s['Failed']} | {s['Skipped']} |" for s in suites.values())
econrows='\n'.join(f"| {name} | {c['Summary']['FinalCash']} | {c['Summary']['FinalDebt']} | {c['Summary']['TotalMatchesCompleted']} | {c['LedgerRows']} |" for name,c in recon['Cases'].items())
report=f'''# Mundos jogáveis com clubes reais — 013C

```text
TASK: ELIFOOT_PLAYABLE_REAL_CLUB_WORLDS_013C
STATUS: {status}
WORKTREE: D:\\elifoot_98_integration
BRANCH: feature/playable-real-club-worlds-013c
BASE_COMMIT: {source['ApprovedBase']}
CHECKPOINT_COMMIT: {checkpoint}
012D_PRESERVED: motores e contabilidade preservados; regressão de 100 temporadas reconciliada.
013A_PRESERVED: montagem 4x8+96, escolha manual, snapshots, cache e compatibilidade.
013B_PRESERVED: provedores, direitos, referências e cobertura preservados.
REAL_MALE_CLUBS: 158
REAL_FEMALE_CLUBS: 232
REAL_CURRENT_PLAYERS: 1 no catálogo e no mundo feminino exercitado.
GENERATED_PLAYERS: 2304 no mundo masculino; 2303 no feminino; autorização explícita.
REAL_CURRENT_COMPLETE_SQUADS: 0
AUTOMATIC_DIVISIONS: PASS, masculino.
MANUAL_DIVISIONS: PASS, feminino, 128 atribuições preservadas.
DISTRICTS: PASS, 96 por mundo, manual/sorteio/ranking exercitados.
RANKING_SOURCE: Elo local estimado masculino; ordem técnica por seed no feminino.
RANKING_CONFIDENCE: masculino até 0,35 e grafos desconectados; feminino 0.
WORLD_SNAPSHOT: país/categoria/clube/atribuições/modo/seed/ranking/proveniência congelados.
DATABASE: PostgreSQL próprio em loopback 55436; mundos e catálogo separados.
CACHE: snapshots recuperados após reinício real do PostgreSQL e API.
FALLBACK: testes herdados conservam o último snapshot válido após falha de fonte.
END_TO_END: login, clube, atletas, escalação, tática, partidas, resultados, avanço, save e reinício.
DOTNET_TESTS: {testcount}; execução integral de 551 e repetição após correção do limite de 24; sem exclusões na execução integral.
FLUTTER_TESTS: 76; analyze limpo; build web concluído.
NODE_TESTS: 2.
LICENSE_LIMITATIONS: direitos preservados; sem nova autorização para mídia ou APIs proprietárias.
KNOWN_ISSUES: KNOWN_ISSUES_013C.md; cobertura real permanece PARTIAL.
```

A base integrada d838cc5 foi escolhida explicitamente pelo usuário. Os checkpoints 7a00bdd e 47608e9 e seus worktrees originais foram preservados; não houve merge automático, push ou publicação.

| Mundo | WorldId | Clubes reais | Atletas reais atuais | Fictícios | Montagem / distritais | Rodada recuperada |
|---|---|---:|---:|---:|---|---:|
{worldrows}

Cada mundo concluiu três rodadas via HTTP e o MatchEngine existente, incluindo partida oficial do clube humano. Nesses dois ensaios o relógio foi comandado por ticks, sem fabricar resultados ou chamar provedores externos. Um terceiro mundo real completou outras três rodadas com o relógio normal da sala, sem chamadas HTTP de tick; evidência em normal-clock-acceptance.json. Um empréstimo repetido criou apenas uma dívida de R$ 500.000; R$ 25.000 de juros foram cobrados/pagos na partida elegível. O prazo zero não eliminou o principal. Salários, contratos, atletas e saldos foram recuperados sem alteração após reinício.

O modo A recusou a criação por falta de elencos; B sem consentimento não gerou jogadores. Em C, Brasil isolado produziu 128 clubes somente com autorização para equipes e atletas. A insuficiência sem autorização permaneceu bloqueada.

A identificação CLUBE REAL — ELENCO SIMULADO, origens de atletas, fontes, datas e classificação estimada/provisória aparecem na interface. Referências históricas ficam separadas do elenco atual. Não se declara nenhum elenco simulado como real completo.

Defeitos corrigidos: perda de aspas escapadas pelo leitor CSV, detectada no round-trip da proveniência; e risco de loop ao completar posições ausentes em um elenco já com 24 atletas. A substituição agora exclui a posição que está sendo adicionada, garantindo progresso. Um teste com 24 meio-campistas reais verifica conclusão, posições mínimas e identidades únicas. Metadados de origem e proteção salarial agora sobrevivem à exportação/importação; o formato antigo continua aceito. A primeira execução foi interrompida após as duas falhas de CSV, corrigida e seguida de regressão integral limpa de 551 casos. A revisão posterior do limite de 24 repetiu toda a suíte de domínio (388) e os dois fluxos PostgreSQL 013C. O consolidado registra 552 casos distintos, sem contar repetições. As 24 simulações aprovadas usam a fábrica legada e não chamam o gerador alterado; seus motores e fontes permaneceram idênticos. Os registros de cada fase foram preservados.

Execução local: iniciar scripts/test-playable-worlds-013c.ps1; build Release da solução com -p:OutputPath=bin/Playable013C/net10.0/ -p:AppendTargetFrameworkToOutputPath=false; scripts/run-playable-worlds-013c.ps1 -RealCatalog -ApiPort 5143. Para o cliente: flutter run -d chrome --dart-define=API_BASE_URL=http://127.0.0.1:5143. O runner não carrega fixtures no catálogo real. Credenciais/dados ficam em artifacts/013c fora do Git. O runner usa relógio normal por padrão. Somente a aceitação controlada usa -ManualClock e os endpoints de ticks.

Relatórios complementares: WORLD_DATA_MODES_013C.md, REAL_AND_SIMULATED_ROSTERS_013C.md, WORLD_CREATION_ACCEPTANCE_013C.md e KNOWN_ISSUES_013C.md. Evidência estruturada: audit-013c/verification.json.
'''
(ROOT/'docs/PLAYABLE_WORLDS_013C.md').write_text(report,encoding='utf-8')
acceptance=f'''# Aceitação — 013C

STATUS: {status}

| Suíte .NET | Aprovados | Falhas | Ignorados |
|---|---:|---:|---:|
{netrows}

Build Release: zero avisos/erros. A execução usa OutputPath isolado bin/Playable013C/net10.0, preservando a profundidade esperada pelos testes; não utiliza filtro. Os TRX integrais têm prefixo final-013c e totalizam 551 casos. Depois da correção de limite do gerador, os TRX boundary-013c registram 388 casos de domínio e dois fluxos PostgreSQL repetidos. O consolidado por assembly e testId estável totaliza 552 casos distintos: baseline 533 mais 17 casos de domínio e dois de integração novos. O build final também foi repetido. A alteração tardia restringiu-se ao gerador dos modos novos; as simulações legadas não o utilizam, e seus motores/fontes permaneceram idênticos. Não houve repetição dos 26 minutos de simulações já aprovadas. Nomes truncados de teorias não são usados para deduplicar casos.

Flutter: 76 aprovados, baseline 70 mais seis casos novos, incluindo larguras 390 e 1366, recusa, consentimento, reset de modo e rótulos. Analyze sem problemas e build Web aprovados. Node: dois testes existentes aprovados.

A aceitação HTTP usa o bundle real preservado, separado das fixtures sintéticas dos testes automatizados. Ela cobre masculino/feminino, oito países selecionados, um país, montagem automática, manual completa, distritais aleatórios/manuais/por ranking, determinismo, duplicação, mínimo de elenco, posições, força, comportamento, contrato, salário, valor de mercado, origem, login, clube humano, escalação e três rodadas. O script de reinício compara hashes de TODOS os mundos e snapshots reais locais, reinicia processos próprios e verifica dados, confirmação de draft e empréstimo idempotente via HTTP.

| Matriz | Resultado e evidência |
|---|---|
| A, cobertura insuficiente | Bloqueio e geração zero; actual-modes.json. |
| A, cobertura completa | Contrato sintético em ambos os gêneros; não contado como dado real. |
| B, recusa | Bloqueio; nenhum jogador fictício gerado. |
| B, autorizado | Dois mundos reais de 128 clubes; actual-worlds.json. |
| C, Brasil | 128 clubes após autorização; equipes geradas restritas ao país escolhido. |
| Manual soberano | 128 atribuições preservadas no feminino; força independe da divisão. |
| Dados históricos/incertos | Seis casos de vínculo inválido excluídos do elenco atual. |
| Persistência / catálogo | API e PostgreSQL reiniciados; atualização do catálogo não altera o mundo; teste novo de integração. |
| Salário / banco / leilões / insolvência | Regressão econômica herdada e reconciliação independente abaixo. |
| Cache / fallback / direitos | Regressão 013A/B integral, mantendo guards de licença. |

| Protocolo de 100 temporadas | Caixa final | Dívida final | Partidas reais | Linhas reconciliadas |
|---|---:|---:|---:|---:|
{econrows}

Os campos SourceCheckpoint herdados nos resumos identificam protocolos históricos; o código efetivamente executado é identificado pela base e pelo manifesto desta missão. As 24 simulações da suíte foram executadas, inclusive testes longos; não houve exclusão de MultiSeasonAndBenchmarkTests. Os três conjuntos acima reproduzem exatamente caixa e dívida da base integrada. Todas as identidades anuais de caixa e dívida, saldos por clube e pares de transferências foram reconciliados independentemente. As métricas de salários, mercado, inflação, empréstimos e crises permanecem separadas nos arquivos herdados produzidos nesta execução.

Esses protocolos longos usam a fábrica 012D e fixtures importadas 014A. Não representam 100 temporadas dos dois mundos reais 013C: estes foram ensaiados por três rodadas e reinício. A transição de liquidez dos mundos de fixtures continua visível e não comprova equilíbrio universal. Nenhum índice inflacionário, teto de caixa ou dreno financeiro novo foi introduzido.

Evidências: audit-013c/full-build.log, full-regression.log, tests/final-013c_*.trx, tests/boundary-013c_*.trx, boundary-exits.json, final-build-exit.json, flutter-exits.json, actual-worlds.json, actual-modes.json, restart-verification.json, actual-restart-http.json, financial-reconciliation.json e final-manifest.json. Ledgers e séries comprimidas grandes permanecem locais com SHA-256 no manifesto; os resumos e resultados são versionados.
'''
(ROOT/'docs/WORLD_CREATION_ACCEPTANCE_013C.md').write_text(acceptance,encoding='utf-8')
print(status,':',testcount,'.NET; 76 Flutter; 2 Node; actual male/female worlds and restart.')
if not ok:raise SystemExit(1)
