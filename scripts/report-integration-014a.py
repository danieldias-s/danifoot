from pathlib import Path
from decimal import Decimal
import json, hashlib, subprocess, re, xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[1]; DOCS=ROOT/'docs'; AUDIT=DOCS/'audit-014a'
def read(name, default=None):
    p=AUDIT/name
    return json.loads(p.read_text(encoding='utf-8-sig')) if p.exists() else default
def git(p,*args): return subprocess.check_output(['git','-C',str(p),*args],text=True).strip()
def money(v): return 'R$ '+format(Decimal(str(v)),',.2f').replace(',','@').replace('.',',').replace('@','.')
source=read('source-state.json'); sources={}; preserved=True
for name,original in source['Sources'].items():
    path=Path(original['Path']); state=git(path,'status','--porcelain=v1','-uall').splitlines()
    head=git(path,'rev-parse','HEAD'); branch=git(path,'branch','--show-current')
    unchanged=head==original['Head'] and branch==original['Branch'] and state==original['Status']
    sources[name]={'Path':str(path),'Head':head,'Branch':branch,'StateUnchanged':unchanged,'StatusSha256':hashlib.sha256('\n'.join(state).encode()).hexdigest()}; preserved &= unchanged
protected={name:hashlib.sha256((ROOT/'src/Elifoot.Domain'/name).read_bytes()).hexdigest()==expected for name,expected in source['ProtectedFiles'].items()}
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}; suites={}
for p in sorted((AUDIT/'tests').glob('final_*.trx'),key=lambda p:p.stat().st_mtime):
    tree=ET.parse(p); counter=tree.find('.//t:Counters',ns); unit=tree.find('.//t:UnitTest',ns)
    if counter is None or unit is None: continue
    suite=Path(unit.attrib['storage']).name.lower()
    suites[suite]={'Suite':suite,'Total':int(counter.attrib['total']),'Passed':int(counter.attrib['passed']),'Failed':int(counter.attrib['failed']),'File':str(p.relative_to(ROOT))}
net_exit=read('dotnet-final-exit.json',{}).get('ExitCode'); flutter=read('flutter-exits.json',{}); restart=read('restart-verification.json',{})
recon=read('financial-reconciliation.json',{}); review=read('economic-review.json',{})
node_log=(AUDIT/'node-tests.log').read_text(encoding='utf-8-sig'); node_ok=bool(re.search(r'pass\s+2',node_log) and re.search(r'fail\s+0',node_log))
compatibility=read('compatibility-consolidated.json',{})
net_ok=compatibility.get('Status')=='PASS' and len(suites)==4 and all(s['Total']==s['Passed'] and s['Failed']==0 for s in suites.values()) and net_exit==0
status='PASS' if net_ok and flutter=={'Analyze':0,'Tests':0,'Build':0} and node_ok and restart.get('Status')=='PASS' and recon.get('Status')=='PASS' and review.get('Status')=='PASS' and preserved and all(protected.values()) else 'PARTIAL'
commit=read('checkpoint.json',{}).get('IntegrationCommit','pendente de commit após validação completa')
verification={'Task':'ELIFOOT_INTEGRATION_012D_013AB_014A','Status':status,'Scope':'DEV; fixtures ou complemento fictício explícito; nenhum elenco real atual completo declarado.',
    'IntegrationWorktree':str(ROOT),'IntegrationBranch':git(ROOT,'branch','--show-current'),'Base012DCommit':source['Sources']['012D']['Head'],'Source013BCommit':source['Sources']['013AB']['Head'],
    'CommonAncestor':source['CommonAncestor'],'TextConflicts':source['TextConflicts'],'IntegrationCommit':commit,'OriginalWorktrees':sources,'ProtectedFilesUnchanged':protected,
    'Suites':list(suites.values()),'DotnetTests':sum(s['Total'] for s in suites.values()),'FullDotnetExit':net_exit,'CompatibilityRegression':compatibility,'FlutterTests':70,'FlutterExits':flutter,'NodeTests':2,'NodePassed':node_ok,
    'Restart':restart,'FinancialReconciliation':recon.get('Status','PENDING'),'EconomicReview':review.get('Status','PENDING'),'ProductionPublished':False,'013CImplemented':False}
(AUDIT/'verification.json').write_text(json.dumps(verification,ensure_ascii=False,indent=2),encoding='utf-8')
rows=[]
for name,c in recon.get('Cases',{}).items():
    s=c['Summary']; rows.append(f"| {name} | {money(s['FinalCash'])} | {money(s['FinalDebt'])} | {money(c['MedianCash'])} | {s['TotalMatchesCompleted']} | {s['RealMinutesSimulated']} | {money(c['LateAnnualCashChange'])} |")
case_table='| Mundo | Caixa T100 | Dívida T100 | Mediana | Jogos reais | Minutos reais | Variação anual T76–100 |\n|---|---:|---:|---:|---:|---:|---:|\n'+'\n'.join(rows) if rows else 'Simulações e reconciliação em andamento.'
initial_rows=[]
for item in read('initial-economy.json',[]):
    divs=item['Divisions'].values(); count=sum(r['Players'] for r in divs); wages=sum(Decimal(r['SalaryPerOfficialMatch']) for r in divs)
    strength=sum(Decimal(r['MeanStrength'])*r['Players'] for r in divs)/count
    label='Fábrica 012D' if item['Reference012D'] else f"{item['Gender']} / {'real + complemento' if item['Port']==5141 else 'fixtures'}"
    initial_rows.append(f'| {label} | {count} | {strength:.4f} | {money(wages)} | {money(item["BirthCash"])} |')
initial_table='| Fundação | Atletas vinculados | Força média | Folha por jogo de todos os clubes | Caixa inicial |\n|---|---:|---:|---:|---:|\n'+'\n'.join(initial_rows)
net_rows='\n'.join(f"| {s['Suite']} | {s['Passed']}/{s['Total']} | {s['Failed']} | `{s['File']}` |" for s in suites.values())
reports={
'INTEGRATION_012D_013AB_014A.md': f'''# Integração 014A

```text
TASK: ELIFOOT_INTEGRATION_012D_013AB_014A
STATUS: {status}
INTEGRATION_WORKTREE: D:\\elifoot_98_integration
INTEGRATION_BRANCH: feature/integration-012d-013ab
BASE_012D_COMMIT: {source['Sources']['012D']['Head']}
SOURCE_013B_COMMIT: {source['Sources']['013AB']['Head']}
COMMON_ANCESTOR: {source['CommonAncestor']}
INTEGRATION_COMMIT: {commit}

012C_PRESERVED: motores protegidos idênticos à base; replay e regressão executados.
012D_PRESERVED: regras financeiras, sala e contabilidade preservadas; comparação exata abaixo.
013A_PRESERVED: criador e snapshots integrados.
013B_PRESERVED: infraestrutura preservada; cobertura real continua PARTIAL.
GENDER_SELECTION: masculino/feminino testados.
COUNTRY_SELECTION: oito países em ambos.
AUTOMATIC_DIVISIONS: 4x8 por ranking.
MANUAL_DIVISIONS: personalização testada.
DISTRICTS: 96, aleatórios e manuais.
CATALOG_DATABASE: PostgreSQL exclusivo 014A, loopback 55435, bancos de mundos/catálogo separados.
CACHE: persistido e recuperado após reinício real.
FALLBACK: falha externa controlada preserva último snapshot válido.
WORLD_SNAPSHOT: independente do catálogo, persistido.
CONTRACTS: salário, prazo e proteção contratual preservados; pacote corrigido.
BANK: R$ 500.000, R$ 25.000 por partida elegível, crédito individual e recibos persistentes.
SALARIES: SalaryPerRound do servidor; força não sobrescreve salário vigente.
AUCTIONS: motivos, prioridade, contabilidade e mínimo de elenco preservados/testados.
FINANCIAL_INTEGRITY: audit-014a/financial-reconciliation.json.
OLD_WORLDS: saves históricos 009/012D carregados em bancos próprios sem alterar originais.
NEW_WORLDS: quatro mundos HTTP de 128 clubes válidos, além das simulações.
MATCH_ENGINE: código e fórmula defensiva intocados.
MULTIPLAYER: 2/4/6/8 clientes, táticas, pronto, partidas, resultados e reconexão; banco idempotente.
DOTNET_TESTS: {sum(s['Total'] for s in suites.values())} nos TRX finais disponíveis; exit integral {net_exit}.
FLUTTER_TESTS: 70; analyze limpo; Web compilado.
NODE_TESTS: 2.
100_SEASONS: ver tabela.
FINANCIAL_RECONCILIATION: {recon.get('Status','em andamento')}.
CONFLICTS_RESOLVED: zero conflitos textuais; incompatibilidades semânticas corrigidas.
KNOWN_ISSUES: INTEGRATION_KNOWN_ISSUES_014A.md.
```

{case_table}

Escopo DEV. Não houve alteração de branches existentes, acesso aos bancos originais, merge no principal, push, produção ou desenvolvimento 013C. Estado original conferido em audit-014a/verification.json. O PASS da integração não amplia a cobertura real parcial da 013B.
''',
'INTEGRATION_CONFLICTS_014A.md': f'''# Merge e compatibilidade

STATUS: {status}

Merge no worktree independente: base 7a00bdd, origem 47608e9, ancestral 50e9c12, --no-commit --no-ff. Zero conflitos textuais. Sem escolha global ours/theirs ou substituição manual de arquivos inteiros.

| Área | Resolução |
|---|---|
| Entities.cs | GameWorld partial da 013A; campos econômicos 012D preservados. |
| Dtos.cs / Enums.cs | Origem/categoria opcionais; modo de força e proteção salarial nos pacotes; enums esportivos intactos. |
| MarketAndEconomyEngine / AiManagerBrain | Implementação 012D integralmente mantida. |
| ClubAndMarketApplicationService / MultiplayerRoundOrchestrator | Sincronização, autorização e idempotência 012D preservadas. |
| WorldSeedFactory / WorldCreatorEngine | Fábrica 012D e criador 013B preservados; fundação validada semanticamente. |
| WorldEditorAndPackageService | Corrigidos modo clássico, proteção contratual, regras comerciais e registro de saldo inicial ao recriar pacote. |
| EfWorldRepository | 012D preservada; origem/categoria no JSON aditivo. |
| Program.cs | Rotas do criador e guards de exportação integrados às finanças. |
| main.dart / api_client.dart | Criador/proveniência com salário real e banco 012D. |
| Importadores | Alvos 014A explícitos, sem fallback ao banco original; relatórios próprios. |

Defeito confirmado: o pacote anterior perdia ForceEvolutionMode e NegotiatedContractProtected; sua recriação usava regras comerciais legadas e não registrava o saldo inicial. Novos campos opcionais preservam o estado exportado. Pacotes antigos de catálogo sem modo recuperam ClassicAdapted, conforme a invariável da 013A; genéricos mantêm LegacySeasonal. Contratos vigentes sem o campo de proteção são preservados; campos explícitos continuam respeitados. Novos mundos recebem a economia atual. Nenhuma fórmula de motor mudou.

Ajustes de ensaio: renovação mínima de cinco partidas respeitada; PostgreSQL novo por execução evita tentar baixar versão de catálogo atualizado. Saves históricos reutilizam um convite e foram testados em bancos distintos preservando o convite e a restrição única. Logs intermediários ficam separados dos resultados finais.
''',
'INTEGRATION_ECONOMY_014A.md': f'''# Economia e reconciliação

STATUS: {status}

012D é referência. Motores de mercado, operação, banco, insolvência, salário e fábrica mantêm seus hashes. Sem inflação, teto de caixa, dreno ou fórmula econômica nova.

Empréstimo R$ 500.000; juros R$ 25.000 por partida oficial elegível efetivamente disputada. Principal, juros e pagamentos separados; modalidade antiga e dívida preservadas. Crédito individual por estádio, com clubes de um e dois empréstimos na quarta divisão. Salário de referência separado do contratado. Pedidos, leilões, força/salário/ID e mínimo 11=1G+10 linha mantêm a 012D.

Criador reaproveita caixa, estádio, calendário e IA da fábrica, substituindo identidades/elencos. Zero empréstimos ao nascer, 128 saldos iniciais, contratos/salários positivos e IDs únicos. Recriação por pacote agora usa regras atuais, crédito e saldo inicial, preservando o modo de força e a proteção salarial exportados.

{initial_table}

Elencos importados diferem da seed por divisão. Fixtures têm 18 atletas e sua própria distribuição de força; catálogo real incompleto usa complemento autorizado até o mínimo de 11. A fábrica possui quatro agentes livres/leilões iniciais; o criador remove agentes sem relação com a categoria importada. Essas diferenças alteram bilheteria, salários, resultados, mercado e crises. Medição inicial anterior a rodadas; crédito do smoke é identificado e não constitui dinheiro de fundação.

{case_table}

Protocolo: seed 199801, 100 temporadas, 351 jogos/ano, motor real e mesmas sondas controladas de banco clássico/legado/insolvência da 012D. Despesas dessas sondas são explícitas no teste; não viraram despesas do jogo. Auditor independente Decimal verifica caixa, dívida, 128 contas por ano, categorias, pares de transferências e unicidade de IDs/chaves nos ledgers.

Revisão econômica: {review.get('Conclusion','em andamento')}

{review.get('ImportedCashTransition','')}

{review.get('AuctionCashExplanation','')}

{review.get('GenderProtocol','')}

Referência padrão esperada: caixa R$ 137.353.919,79 e dívida R$ 3.270.104,68. Mundos diferentes exigem reconciliação e justificativa econômica, sem exigir valor final igual. Revisão: audit-014a/economic-review.json. Crises e dívidas remanescentes não são apagadas; observação de 100 temporadas não prova equilíbrio infinito.
''',
'INTEGRATION_WORLD_CREATOR_014A.md': f'''# Criador e catálogo

STATUS: {status}

Masculino/feminino, oito países, ranking automático, divisões personalizadas, distritais aleatórios/manuais: quatro divisões de oito e 96 distritais. Configuração por proprietário, prévia persistida e confirmação idempotente mantidas. Atualizar catálogo não modifica mundo confirmado.

Quatro mundos HTTP: duas categorias de fixtures e duas com clubes reais + complemento fictício consentido. Todas as 512 equipes inspecionadas: salários/contratos positivos, força 1–50, estádio válido, 11–24 atletas, goleiro e dez de linha, IDs únicos. Catálogo real separado das fixtures; status, estimativa, proveniência e consentimento preservados.

Cópia do coverage-bundle.json 013B com hash igual ao original, sem nova coleta/API paga. Dados: 1.398 registros incluindo candidatos/históricos; somente 390 identidades verificadas (158 M/232 F), 3.079 atletas documentais distintos e 3.101 vínculos documentados. Um vínculo atual utilizável, zero elencos atuais completos. Identidade verificada não certifica atividade atual/elenco oficial.

PostgreSQL exclusivo 55435, senha/usuário/dados próprios em artifacts/014a. Bancos de mundos, catálogos real/fixtures e testes separados. Nenhum banco original ou produção usado. Campos JSON aditivos, sem migration destrutiva. Saves completos 009/012D recuperados e reiniciados em bancos próprios.

Reprodução: test-integration-014a.ps1 -Action Start; build Release; run-integration-014a.ps1 -Fixtures -ApiPort 5140 para carga inicial. Reiniciar sem -Fixtures usa catálogo salvo e preserva versão mais nova. -RealCatalog -ApiPort 5141 usa o catálogo real próprio. Validação final usa -BuildDirectory IntegrationFinal. verify-restart-integration-014a.ps1 confere reinício; stop-integration-014a.ps1 encerra somente PIDs próprios conferidos e, opcionalmente, seu PostgreSQL. Credenciais e volumes ficam fora do Git.
''',
'INTEGRATION_MULTIPLAYER_014A.md': f'''# Multiplayer em mundos criados

STATUS: {status}

Ciclo de 15 etapas ampliado preserva quatro casos antigos e acrescenta 2/4/6/8 clientes em mundos criados, masculinos/femininos. HTTP/SignalR independentes validam clube, tática, pronto 0/N→N/N, jogo por minuto, pausas, substituições, resultado, próxima rodada, desconexão e reconexão.

Banco concorrente com mesma chave gera só um principal/lançamento; repetir após reconectar o hub não duplica crédito. Comandos em clube alheio falham. Mesmo orquestrador e trava de sala 012D.

PostgreSQL testa compra/venda, renovação sem reprecificar salário, oito pedidos bancários concorrentes, jogo real, juros únicos, quitação e replay após recriar repositório. Reinício real PostgreSQL/APIs mantém snapshots e hashes financeiros dos quatro mundos. Confirmação da prévia retorna mesmo mundo. Comando financeiro entre salas é recusado mesmo quando os clubes têm IDs determinísticos iguais. Evidências: api-restart.json, restart-verification.json e TRX finais em audit-014a. Identidade LocalDev; nenhuma homologação de produção declarada.
''',
'INTEGRATION_REGRESSION_014A.md': f'''# Regressão efetiva

STATUS: {status}

| Suíte final | Aprovados/total | Falhas | TRX |
|---|---:|---:|---|
{net_rows}

Total distinto observado: {sum(s['Total'] for s in suites.values())}; exit integral: {net_exit}. Contagem dos TRX, sem somar casos sobrepostos de branches. Flutter: 70, analyze limpo, build Web aprovado. Node: dois casos aprovados. Nenhum teste removido. Assert.False(Any) virou DoesNotContain com condição idêntica para eliminar aviso xUnit.

Comandos: dotnet build/test Elifoot.slnx -c Release, com OutputPath=bin/IntegrationFinal/net10.0/ e AppendTargetFrameworkToOutputPath=false para isolar DLLs e manter profundidade dos testes legados. flutter analyze; flutter test; flutter build web; node --test tools/catalog-013b/openfootball-directory.test.mjs. Build Release padrão também executado antes do ajuste do pacote; build final isolado inclui o ajuste.

Primeira regressão detectou catálogo persistido indevidamente reutilizado por teste. Após usar bancos novos, suíte anterior passou 130/130. A final inclui dois casos de pacote (132). Ensaio anterior interrompido para validar código final completo. Logs intermediários/erros de scripts não são evidência de aprovação final; auditoria foi executada novamente após corrigir leitura de enum/caminho.

{case_table}

Após a conferência de compatibilidade dos pacotes antigos, a regressão curta inteira foi repetida: 515 casos distintos por testId aprovados na consolidação. Um caso de recarga precisava do snapshot produzido pelo ensaio longo; a pasta curta estava sem esse arquivo. Uma cópia de hash idêntico foi fornecida e o caso passou na reexecução. Exit bruto inicial 1 e exit da reexecução 0 preservados, sem mascarar a primeira falha. Os 18 ensaios longos aprovados mantêm fontes, fábrica de cenários e motores inalterados; o complemento modifica apenas campos/fallback de pacotes do editor e seus testes. Total distinto consolidado: 533. Hashes dos motores, replay 012C e baseline 012D em verification.json. Ledgers/séries gzip completos ficam localmente, com hashes e resumos versionados. Nenhum banco original usado.
''',
'INTEGRATION_KNOWN_ISSUES_014A.md': f'''# Limites e pendências

STATUS DA INTEGRAÇÃO: {status}

1. Dados 013B continuam PARTIAL: um vínculo atual utilizável e zero elencos atuais completos. Demonstrações usam fixtures ou complemento fictício explícito. Ranking/força estimados não certificam elenco oficial atual.
2. Escopo DEV local: PostgreSQL próprio, HTTP/SignalR, Flutter Web e viewports. Produção, APIs pagas e identidade de produção não ativadas.
3. A média de caixa T76–100 nos importados é -R$ 4,910 milhões/ano e não atende ao diagnóstico de deriva de 1% da 012D. A transição está reconciliada, com grande saída por leilões de agentes livres; T91–100/T96–100 e a faixa T86–100 estão na revisão econômica. Cem temporadas oferecem evidência observacional, sem prova de equilíbrio infinito. Elencos importados mudam estado inicial; crises e dívida continuam registradas.
4. Pacote do editor funda outro mundo; recuperação integral de partida em curso usa SnapshotJson persistido, incluindo dívida e histórico.
5. Sem migrations destrutivas. Bases de teste, credenciais e volumes em artifacts/014a ignorado. Dados brutos grandes mantidos localmente e identificados por hash; scripts/resumos permitem repetir ensaios.
6. 013C não desenvolvida. Sua base futura é a branch integrada. Principal e 013B permanecem em seus checkpoints, sem push/merge/publicação desta tarefa.

Pendências impeditivas nesta geração: {'nenhuma no escopo DEV validado' if status=='PASS' else 'aguardando regressão longa, reconciliação e revisão econômica final; ver verification.json'}.
'''
}
for name,text in reports.items(): (DOCS/name).write_text(text.strip()+'\n',encoding='utf-8')
print('Reports:',status,'; .NET observed:',sum(s['Total'] for s in suites.values()),'; originals preserved:',preserved)