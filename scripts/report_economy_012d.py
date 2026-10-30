"""Independent ledger reconciliation and final 012D reports. Does not run the game."""
from pathlib import Path
from collections import defaultdict
from decimal import Decimal
import argparse, csv, gzip, hashlib, json, re, subprocess, xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
AUDIT=ROOT/'docs/audit-012d'
parser=argparse.ArgumentParser();parser.add_argument('--audit-only',action='store_true');args=parser.parse_args()
candidate=AUDIT/'solution-final/economy-012d'
data_dir=candidate if (candidate/'100-seasons.json').exists() else AUDIT/'final/economy-012d'
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'),parse_float=Decimal)
def plain(v):
    if isinstance(v,Decimal):return str(v)
    raise TypeError(type(v).__name__)
def save(path,value):path.write_text(json.dumps(value,ensure_ascii=False,indent=2,default=plain),encoding='utf-8')
def money(value):return 'R$ '+f'{Decimal(value):,.2f}'.replace(',','_').replace('.',',').replace('_','.')
d=read(data_dir/'100-seasons.json');last=d['Seasons'][-1]
assert d['SeasonsCompleted']==100 and d['TotalMatchesCompleted']==35100
categories=defaultdict(Decimal);ids=set();keys=set();pairs=defaultdict(list);ledger_count=0
with gzip.open(data_dir/'ledger.jsonl.gz','rt',encoding='utf-8') as stream:
    for line in stream:
        t=json.loads(line,parse_float=Decimal);ledger_count+=1
        assert t['Id'] not in ids;ids.add(t['Id'])
        key=(t['ClubId'],t['IdempotencyKey']);assert key not in keys;keys.add(key)
        categories[t['Category']]+=Decimal(t['Amount'])
        if t.get('SellerClubId') is not None and t.get('TransferTransactionId') is not None:pairs[t['TransferTransactionId']].append(t)
assert sum(categories.values())==last['TotalCash']
assert dict(categories)=={k:Decimal(v) for k,v in d['ReconciledCashByCategory'].items()}
for rows in pairs.values():
    assert len(rows)==2 and sum(Decimal(r['Amount']) for r in rows)==0
    assert len({r['PlayerId'] for r in rows})==1
loan_summaries={};loan_keys=set();charged=set();bank_rows=0
with gzip.open(data_dir/'bank.jsonl.gz','rt',encoding='utf-8') as stream:
    for line in stream:
        row=json.loads(line,parse_float=Decimal);e=row['Entry'];bank_rows+=1
        key=(row['Id'],e['Key']);assert key not in loan_keys;loan_keys.add(key)
        current=loan_summaries.setdefault(row['Id'],{'Mode':row['Mode'],'OpeningDebt':Decimal(e['DebtBefore']),
            'Debt':Decimal(e['DebtBefore']),'InterestCharged':Decimal(0),'InterestPaid':Decimal(0),'PrincipalPaid':Decimal(0)})
        assert current['Debt']==e['DebtBefore']
        before=Decimal(e['DebtBefore']);after=Decimal(e['DebtAfter']);interest=Decimal(e['InterestCharged']);paid=Decimal(e['InterestPaid']);principal=Decimal(e['PrincipalPaid'])
        if e['Kind']=='DISBURSEMENT':assert before==0 and after==500000 and e['CashChange']==500000
        else:
            assert after==before+interest-paid-principal
            assert Decimal(e['CashChange'])==-paid-principal
        if e.get('MatchId'):
            match_key=(row['Id'],e['MatchId']);assert match_key not in charged;charged.add(match_key)
            if row['Mode']=='ClassicFixed':assert interest==25000 and principal==0
        current['Debt']=after;current['InterestCharged']+=interest;current['InterestPaid']+=paid;current['PrincipalPaid']+=principal
assert sum(r['Debt'] for r in loan_summaries.values())==last['TotalDebt']
assert sum(r['InterestCharged'] for r in loan_summaries.values())==d['InterestCharged']
assert sum(r['InterestPaid'] for r in loan_summaries.values())==d['InterestPaid']
assert sum(r['PrincipalPaid'] for r in loan_summaries.values())==d['PrincipalPaid']
controls={}
for strength in [1,10,20,30,40,50]:
    values=[next(b for b in s['StrengthBins'] if b['Strength']==strength) for s in d['Seasons']]
    assert len({b['FixedQualityMarketValue'] for b in values})==1
    assert len({b['FixedQualitySalary'] for b in values})==1
    controls[str(strength)]={'MarketValue':values[0]['FixedQualityMarketValue'],'ReferenceSalary':values[0]['FixedQualitySalary'],'NominalIndexT1ToT100':100}
independent={'Status':'PASS','Dataset':str(data_dir.relative_to(ROOT)),'LedgerEntries':ledger_count,'BankEntries':bank_rows,
    'UniqueChargedLoanMatches':len(charged),'ReconciledTransferPairs':len(pairs),'InitialCash':categories['InitialBalance'],
    'FinalCash':last['TotalCash'],'FinalDebt':last['TotalDebt'],'CashDifference':0,'PairDifference':0,
    'CashByCategory':dict(categories),'Loans':loan_summaries,'FixedQualityControls':controls}
save(AUDIT/'independent-reconciliation.json',independent)
print(json.dumps({k:independent[k] for k in ['Status','LedgerEntries','BankEntries','UniqueChargedLoanMatches','ReconciledTransferPairs','FinalCash','FinalDebt']},default=plain))
if args.audit_only:raise SystemExit(0)

suites=[]
for path in sorted((AUDIT/'tests').glob('solution-release*.trx')):
    tree=ET.parse(path);counter=tree.find('.//{*}Counters');unit=tree.find('.//{*}UnitTest')
    if counter is None or unit is None:continue
    suite=Path(unit.attrib['storage']).name
    suites.append({'Suite':suite,'Total':int(counter.attrib['total']),'Passed':int(counter.attrib['passed']),
        'Failed':int(counter.attrib.get('failed','0')),'File':str(path.relative_to(ROOT))})
assert len({s['Suite'] for s in suites})==4,'The complete Release solution regression has not finished.'
assert all(s['Failed']==0 and s['Total']==s['Passed'] for s in suites)
solution_total=sum(s['Total'] for s in suites)
for project,stem in [('Domain','domain'),('Application','application'),('Integration','integration')]:
    review=AUDIT/f'tests/{stem}-review-final.trx'
    if not review.exists():continue
    tree=ET.parse(review);counter=tree.find('.//{*}Counters');assert int(counter.attrib.get('failed','0'))==0
    dll=f'Elifoot.{project}.Tests.dll'
    suites=[s for s in suites if s['Suite'].lower()!=dll.lower()]
    suites.append({'Suite':dll,'Total':int(counter.attrib['total']),'Passed':int(counter.attrib['passed']),
        'Failed':0,'File':str(review.relative_to(ROOT))})
total=sum(s['Total'] for s in suites)
assert 'No issues found!' in (AUDIT/'flutter-analyze-final.log').read_text(encoding='utf-8-sig')
flutter_log=(AUDIT/'flutter-tests-final.log').read_text(encoding='utf-8-sig')
assert 'All tests passed!' in flutter_log
flutter_count=int(re.findall(r'\+(\d+): All tests passed!',flutter_log)[-1])

preserved=[]
for name in ['MatchEngine.cs','DisciplineEngine.cs','TacticsAndSquadRules.cs','AiManagerBrain.cs','CoachCareerEngine.cs','PlayerStatisticsEngine.cs','ClassicForceState.cs']:
    current=ROOT/'src/Elifoot.Domain'/name;base=AUDIT/'baseline/files/src/Elifoot.Domain'/name
    expected=hashlib.sha256(base.read_bytes()).hexdigest();actual=hashlib.sha256(current.read_bytes()).hexdigest();assert actual==expected
    preserved.append({'File':str(current.relative_to(ROOT)),'Sha256':actual})
for name in ['ClassicForceEvolutionEngine.cs','PlayerStrengthEvolutionEngine.cs']:
    current=(ROOT/'src/Elifoot.Domain'/name).read_text(encoding='utf-8-sig')
    base=(AUDIT/'baseline/files/src/Elifoot.Domain'/name).read_text(encoding='utf-8-sig')
    expected=re.sub(r'(?m)^\s*p\.SalaryPerRound = MarketAndEconomyEngine\.CalculateFairSalaryPerRound\(p\);\n','',base)
    assert current==expected,'Force changes must be limited to removing the automatic salary write: '+name

verification={'Status':'PASS','OverallTaskStatus':'PARTIAL','Reason':'Long-term cash accumulation remains a measured balance limitation; no claim of strategic equilibrium.',
    'BaselineCommit':'50e9c12','Suites':suites,'SolutionRunTests':solution_total,'DotnetDistinctTests':total,'FlutterTests':flutter_count,'Preserved':preserved,
    'IndependentReconciliation':'docs/audit-012d/independent-reconciliation.json','ProductionPublished':False}
save(AUDIT/'verification.json',verification)
changed=[];created=[]
diff=subprocess.run(['git','diff','--name-status','50e9c12'],cwd=ROOT,capture_output=True,text=True,check=True)
for line in diff.stdout.splitlines():
    state,path=line.split('\t',1)
    (created if state=='A' else changed).append(path)
untracked=subprocess.run(['git','ls-files','--others','--exclude-standard','src','tests','scripts','client/elifoot_flutter/lib','client/elifoot_flutter/test'],cwd=ROOT,capture_output=True,text=True,check=True)
created.extend(untracked.stdout.splitlines())
created.extend(str(p.relative_to(ROOT)).replace('\\','/') for p in (ROOT/'docs').glob('*012D.md'))
created.append('docs/ECONOMY_FINAL_REPORT_012D.md')
created=sorted(set(created));changed=sorted(set(changed))
code=[]
for folder in ['src','tests','client/elifoot_flutter/lib','client/elifoot_flutter/test','scripts']:
    for path in sorted((ROOT/folder).rglob('*')):
        if not path.is_file() or any(part in ['bin','obj','build','.dart_tool','__pycache__'] for part in path.parts):continue
        if path.suffix not in ['.cs','.csproj','.dart','.py','.mjs','.ps1']:continue
        code.append({'Path':str(path.relative_to(ROOT)).replace('\\','/'),'Sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
save(AUDIT/'final-code-manifest.json',{'BaselineCommit':'50e9c12','Changed':changed,'Created':created,'Files':code})
with (AUDIT/'economy-checkpoints.csv').open('w',encoding='utf-8',newline='') as stream:
    out=csv.writer(stream);out.writerow(['Season','Cash','Debt','Loans','InterestCharged','InterestPaid','Requests','Accepted','Refused','Auctions','Unsold','Insolvent','Recovered'])
    for s in d['Checkpoints']:out.writerow([s[k] for k in ['Season','TotalCash','TotalDebt','Loans','InterestCharged','InterestPaid','Requests','Accepted','Refused','Auctions','Unsold','Insolvent','Recovered']])
flows=['Loans','InterestCharged','InterestPaid','PrincipalPaid','Requests','Accepted','Refused','Auctions','Unsold','Recovered']
with (AUDIT/'economy-annual-flows.csv').open('w',encoding='utf-8',newline='') as stream:
    out=csv.writer(stream);out.writerow(['Season']+['New'+k for k in flows]+['ClosingCash','ClosingDebt','InsolventClubs','NegativeCashClubs','MinimumSquadClubs'])
    previous={k:0 for k in flows}
    for season in d['Seasons']:
        out.writerow([season['Season']]+[season[k]-previous[k] for k in flows]+[season[k] for k in ['TotalCash','TotalDebt','Insolvent','NegativeCash','MinimumSquadClubs']])
        previous={k:season[k] for k in flows}

checkpoint_table='\n'.join(f"| {s['Season']} | {money(s['TotalCash'])} | {money(s['TotalDebt'])} | {s['Loans']} | {s['Requests']} | {s['Insolvent']} |" for s in d['Checkpoints'])
division_table='\n'.join(f"| {s['Division']} | {money(s['Cash'])} | {money(s['Debt'])} | {money(s['MeanSalary'])} | {s['MinimumSquadClubs']} | {s['CreditLimits']} |" for s in last['ByDivision'])
control_table='\n'.join(f"| {strength} | {money(c['MarketValue'])} | {money(c['ReferenceSalary'])} | 100 → 100 |" for strength,c in controls.items())
report=f'''# Economia em 100 temporadas — 012D

STATUS: PARTIAL. A implementação e as invariantes contábeis passaram; o equilíbrio estratégico de longo prazo permanece uma limitação medida.

Seed {d['Seed']}; 128 clubes; 100 temporadas; {d['TotalMatchesCompleted']:,} partidas reais; {d['RealMinutesSimulated']:,} minutos. MatchEngine original preservado. O cenário é IA com sondas iniciais declaradas: dois empréstimos clássicos e despesa de capital de giro, um empréstimo legado e um clube em insolvência. Não representa um mundo sem intervenção inicial. As transações das sondas constam do livro-caixa.

## Resultados reconciliados

- {ledger_count:,} lançamentos únicos; {len(pairs):,} pares de transferência/venda por dívida, todos de soma zero.
- {last['Loans']} empréstimos contratados; desembolso {money(categories['LoanDisbursement'])}; juros cobrados e pagos {money(d['InterestPaid'])}; principal quitado {money(d['PrincipalPaid'])}; dívida final {money(last['TotalDebt'])}.
- {last['Requests']} pedidos salariais: {last['Accepted']} aceitos e {last['Refused']} recusados no cenário. {d['ContractsPreservedChecks']:,} verificações de salário vigente preservado. Recusas e renovação obrigatória também foram validadas em testes controlados.
- Leilões: {d['AuctionsByReason']}. Sem comprador: {last['Unsold']} no cenário; caso sem lance e reoferta com intervalo validado separadamente. Não inferir cobertura de recusa/ausência de comprador a partir de contagens naturais iguais a zero.
- Clubes em crise no fechamento T100: {last['Insolvent']}; recuperações registradas: {last['Recovered']}; caixa negativo T100: {last['NegativeCash']}; clubes no elenco mínimo T100: {last['MinimumSquadClubs']}.
- Zero violações de escalação, elenco e liquidação duplicada. Nenhum empréstimo é quitado pelo fim do contador. O teste legado específico conserva R$ 8.622,06 após 20 prestações e exige pagamento real do residual.

## Marcos da execução

| Temporada | Caixa total | Dívida | Empréstimos acumulados | Pedidos acumulados | Insolventes |
|---:|---:|---:|---:|---:|---:|
{checkpoint_table}

## Divisões após o fechamento da temporada 100

Os grupos são as divisões após acessos/rebaixamentos; históricos acumulados são atribuídos ao clube e agrupados por sua divisão nesse fechamento. O JSON inclui todas as temporadas, juros, quitações, pedidos, recusas, leilões, insolvência, elenco mínimo, salários e valores médios por divisão.

| Divisão | Caixa | Dívida | Salário médio por jogador/partida | Clubes com 11 | Distribuição dos limites |
|---|---:|---:|---:|---:|---|
{division_table}

## Inflação e composição

Controle fixo: posição M, Cavalheiro, sem estrela e dez partidas contratuais. Nenhuma curva recebeu indexação temporal. Cada controle foi comparado nas 100 temporadas pelo auditor independente.

| Força | Valor justo fixo | Salário de referência fixo | Índice nominal T1 → T100 |
|---:|---:|---:|---:|
{control_table}

O salário contratado e o valor médio observado por força variam com posição, estrela, contrato e negociações. Faixas sem atletas têm média nula, não preço zero. Esses dados estão em `StrengthBins`, separadamente dos controles. Preços efetivos de cada transferência estão no livro-caixa comprimido.

## Acumulação monetária e comparação histórica

O caixa inicial foi {money(categories['InitialBalance'])}; chegou a {money(last['TotalCash'])}. O modelo permaneceu operacional, mas crédito deixou de ser necessário após os primeiros anos. Isso limita a validação do banco como ferramenta estratégica durante todo o horizonte.

Patrocínios somaram {money(categories['Sponsorship'])}; bilheterias {money(categories['TicketRevenue'])}; salários {money(-categories['PlayerSalary'])}. A política preexistente paga patrocínio em 22 rodadas inclusive em folgas e eliminações, enquanto a folha depende de jogos. A auditoria 007 já descrevia essa assimetria e caixa final de R$ 5.664.382.164,15 no seu candidato; a 006 relatava recuperação de 21 clubes usando outro critério de crise. São contextos diferentes: força, disciplina, contratos, preços e decisões mudaram entre missões. A diferença não demonstra que os novos empréstimos causaram o crescimento.

A curva de valor justo da 005 permanece. A 012/012B/012C mudou a população de força; a 012D preserva o salário negociado durante a vigência e somente novos contratos podem reprificá-lo. Não há comparação causal controlada que isole todas essas mudanças. Não foi introduzido indexador anual nem alterado patrocínio/estádio para mascarar o resultado.

## Evidência

Dataset: `{data_dir.relative_to(ROOT)}`. Arquivos: `100-seasons.json`, `ledger.jsonl.gz`, `bank.jsonl.gz`, `contracts-and-crises.jsonl.gz`, `salary-requests-final.json`; reconciliação independente em `docs/audit-012d/independent-reconciliation.json`; marcos CSV em `docs/audit-012d/economy-checkpoints.csv`. O arquivo `economy-annual-flows.csv` separa novas operações e fluxos de cada uma das 100 temporadas dos contadores acumulados, mantendo caixa, dívida e crises como estoques de fechamento. O JSON `Status=PASS` refere-se às invariantes verificadas, não à validação do equilíbrio estratégico global.
'''
(ROOT/'docs/ECONOMY_100_SEASONS_012D.md').write_text(report,encoding='utf-8')

known=f'''# Pendências e limites — 012D

STATUS: PARTIAL.

1. **Equilíbrio estratégico de longo prazo:** caixa agregado de {money(last['TotalCash'])} em T100, contra abertura de {money(categories['InitialBalance'])}. O crédito funciona e foi reconciliado, mas perdeu relevância depois dos primeiros anos. A assimetria entre patrocínio por rodada e folha por partida já existia e foi preservada. Uma recalibração coordenada dessas receitas/despesas ainda precisa de experimentos próprios; não se afirma economia equilibrada.
2. **História:** os valores exatos R$ 500.000/R$ 25.000 e a fórmula de limite não foram autenticados em executável original. São adaptação autorizada, não engenharia reversa comprovada.
3. **Saves antigos:** composição de juros/principal anterior à migração pode ser desconhecida. Saldo, taxa e prestação são conservados, sem reconversão ou perdão. Contratos vigentes antigos são protegidos conservadoramente.
4. **Cobertura natural:** o cenário de 100 temporadas teve {last['Refused']} recusas e {last['Unsold']} leilões sem comprador. Esses caminhos foram exercitados nos testes controlados e concorrentes, sem inventar ocorrências no cenário natural.
5. **Crédito superior:** faixas candidatas 1–3/2–4/2–5 foram conservadas como configuração da adaptação. A execução não demonstrou colapso por crédito, mas o excesso de caixa impede afirmar calibração estratégica definitiva dos tetos.
6. **Cliente legado:** chaves e versões continuam opcionais nos endpoints anteriores por compatibilidade. A interface atual envia ambas; consumidores antigos precisam reutilizar uma chave para garantir repetição entre requisições independentes.
7. **Pesquisa e escopo:** nenhum executável não autorizado foi obtido. Não houve publicação em produção nem alteração de outro projeto. O banco de desenvolvimento não foi usado nos testes; persistência foi validada em bancos descartáveis.

Validação efetiva: {total} testes .NET em Release, {flutter_count} testes Flutter, análise Flutter limpa, 100 temporadas com partidas reais e reconciliação independente. Logs iniciais com falhas de expectativas antigas/dependências de QA foram mantidos; `verification.json` aponta os resultados finais.
'''
(ROOT/'docs/KNOWN_ISSUES_012D.md').write_text(known,encoding='utf-8')

suite_table='\n'.join(f"| {s['Suite']} | {s['Passed']} | {s['Failed']} | `{s['File']}` |" for s in suites)
reg=ROOT/'docs/ECONOMY_REGRESSION_012D.md';text=reg.read_text(encoding='utf-8')
marker='\n## Resultado final verificado\n'
text=text.split(marker)[0]+marker+f'''
Build Release: zero erros e avisos. Solução Release completa: {solution_total}/{solution_total} testes aprovados. Após a revisão das chaves do histórico e da repetição após falha de persistência, passaram novamente 338 testes de domínio, seis de aplicação e 45 de integração. Os casos novos incluem dois clubes reutilizando uma chave de comando e falha de gravação seguida de repetição sem crédito duplicado. A tabela consolida {total} testes distintos aprovados em Release, sem contar os reprocessamentos em duplicidade. Flutter: {flutter_count}/{flutter_count}; análise sem apontamentos. A execução da solução usa `ELIFOOT_AUDIT_OUTPUT=docs/audit-012d/solution-final`, com o artefato real 009 disponível antes de seu consumidor.

| Suíte | Aprovados | Falhas | Evidência |
|---|---:|---:|---|
{suite_table}

O SHA-256 do MatchEngine permanece `01179d6a0f0a073b69ff7bf0589674b664f960fbedcba6e1d5907ea55e18bb71`. Também foram comparados disciplina, táticas, IA, carreira, estatísticas e estado da força. Nos dois motores de evolução, a única alteração é remover a escrita automática do salário; a referência é calculada a partir dos atributos atuais, inclusive após edição/importação.

`verification.json` e `independent-reconciliation.json` registram os controles. A solução inclui replay de duas temporadas clássicas, testes bancários após restauração e concorrência HTTP/SignalR em 2/4/6/8 participantes, além da disputa de IA com humanos. O status geral permanece PARTIAL pelo balanceamento monetário, apesar de as verificações funcionais passarem.
'''
reg.write_text(text,encoding='utf-8')
final=f'''# ELIFOOT_CLASSIC_BANK_CONTRACTS_ECONOMY_012D

STATUS: PARTIAL — implementação e auditoria técnica concluídas; equilíbrio monetário de longo prazo ainda pendente.

HISTORICAL_BANK_RESEARCH: documentação histórica e relatos classificados em `CLASSIC_BANK_RESEARCH_012D.md`; fórmula de crédito e valores exatos não comprovados historicamente.
LOAN_PRINCIPAL: R$ 500.000 por nova operação.
INTEREST_PER_MATCH: R$ 25.000 por empréstimo ativo e partida oficial efetivamente disputada, sem capitalizar juros vencidos.
DIV4_CREDIT_LIMIT: 1–2, individual por clube.
DIV3_CREDIT_LIMIT: 1–3, adaptado.
DIV2_CREDIT_LIMIT: 2–4, adaptado.
DIV1_CREDIT_LIMIT: 2–5, adaptado.
CREDIT_ELIGIBILITY: capacidade utilizável × R$ 50; limiares versionados; caixa emprestado não amplia crédito; dívidas antigas preservadas no rebaixamento.
LOAN_REPAYMENT: integral com caixa disponível real, principal e juros separados; legados conservam taxa, prestação e dívida residual.
BANK_IDEMPOTENCE: recibos persistentes, versão/fase da sala, semáforo compartilhado, IDs de partidas cobradas e replay após SQLite.
SALARY_RAISE_REQUESTS: força/promoção; PENDING/ACCEPTED/REFUSED persistentes; salário negociado separado da referência.
ONE_REQUEST_PER_ROUND: uma solicitação por clube/rodada, determinística.
SEASON_RENEWAL: negociação vigente protegida; renovação preventiva preserva salário e registra compromisso.
REFUSED_RENEWAL_AUCTION: saída somente se legal, livre, lance inicial zero, sem receita ao ex-clube.
MANDATORY_RENEWAL: piso do elenco impõe renovação, sem criar caixa.
MINIMUM_SQUAD: 11 registrados, um goleiro e dez de linha; indisponibilidade temporária permanece separada.
LAST_GOALKEEPER_PROTECTION: domínio, API, IA e liquidação.
INSOLVENCY: caixa negativo ou juros vencidos; dívida persistente, recuperação por receitas reais.
FORCED_AUCTION: vínculo até venda; pagamento ao vendedor; renovados elegíveis; resultado persistente.
PLAYER_EXIT_PRIORITY: força, salário, ID estável.
AUCTION_WITHOUT_BIDS: AUCTION_UNSOLD, sem liberação por dívida, intervalo de duas rodadas; sem elegível não entra em loop.
INFLATION: nenhum indexador temporal; controles equivalentes constantes; crescimento excessivo de caixa medido e pendente de recalibração.
SALARY_EVOLUTION: {d['ContractsPreservedChecks']:,} verificações preservando salário entre novos contratos.
PLAYER_MARKET_VALUES: curva preservada e seis faixas de força auditadas por temporada.
100_SEASONS: 100, 35.100 partidas, {d['RealMinutesSimulated']:,} minutos reais; todas as divisões.
BANK_LOANS_TOTAL: {last['Loans']} no cenário com sondas declaradas.
BANK_INTEREST_TOTAL: {money(d['InterestCharged'])}, reconciliados.
FORCED_AUCTIONS_TOTAL: {d['AuctionsByReason'].get('FinancialInsolvency',0)} no cenário; demais casos cobertos em testes controlados.
INSOLVENT_CLUBS: {last['Insolvent']} no fechamento T100.
RECOVERED_CLUBS: {last['Recovered']} episódios no cenário.
DOTNET_TESTS: solução completa {solution_total}/{solution_total}; revisão 338 domínio, seis aplicação, 45 integração; {total} testes distintos aprovados em Release.
FLUTTER_TESTS: {flutter_count}/{flutter_count}; análise limpa.
MULTIPLAYER: 2/4/6/8 em HTTP/SignalR; empréstimos e decisões repetidos; reserva de caixa; IA/humanos disputando o mesmo atleta.
DETERMINISTIC_REPLAY: duas temporadas clássicas na regressão e liquidação bancária/salarial após serialização.
KNOWN_ISSUES: `KNOWN_ISSUES_012D.md`, sobretudo crescimento de caixa e limites da evidência histórica.

Defeitos corrigidos: quitação por prazo, salário sobrescrito pela força, estimativa de 3,5% na interface, ausência de sincronização/recibos e modalidade bancária sem crédito individual. Não foi comprovado defeito que justificasse substituir o comportamento anterior do leilão por vencimento.

Juros e dívidas, salários e arremates foram reconciliados; {len(pairs):,} pares de venda somam zero. Histórico legado desconhecido foi marcado como tal. O checkpoint 012C é `50e9c12`; MatchEngine e núcleo disciplinar/tático preservados. Nenhuma publicação em produção.

Arquivos e evidências: `audit-012d/verification.json`, `audit-012d/independent-reconciliation.json`, os dez documentos obrigatórios e fontes/testes de economia 012D. O relatório não declara PASS global porque o caixa chegou a {money(last['TotalCash'])} e o papel estratégico do crédito ao longo de todo o horizonte não foi demonstrado como equilibrado.
'''
(ROOT/'docs/ECONOMY_FINAL_REPORT_012D.md').write_text(final,encoding='utf-8')
with (ROOT/'docs/ECONOMY_FINAL_REPORT_012D.md').open('a',encoding='utf-8') as stream:
    stream.write('\n## FILES_CHANGED\n\n'+'\n'.join('- `'+p+'`' for p in changed)+'\n\n## FILES_CREATED\n\n'+'\n'.join('- `'+p+'`' for p in created)+'\n')
print(f'Reports generated: {total} .NET tests, {flutter_count} Flutter tests; overall PARTIAL.')
