"""Reconcile every observed season and create the 012D_FIX before/after evidence."""
from pathlib import Path
from collections import defaultdict
from decimal import Decimal
import argparse,gzip,hashlib,json,statistics,subprocess,xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1];AUDIT=ROOT/'docs/audit-012d-fix';EXP=AUDIT/'experiments/balance-012d-fix'
parser=argparse.ArgumentParser();parser.add_argument('--preview',action='store_true');args=parser.parse_args()
def load(path):return json.loads(path.read_text(encoding='utf-8-sig'),parse_float=Decimal)
def save(path,obj):path.write_text(json.dumps(obj,ensure_ascii=False,indent=2,default=str),encoding='utf-8')
def money(value):return 'R$ '+f'{Decimal(value):,.2f}'.replace(',','_').replace('.',',').replace('_','.')
def median(values):return statistics.median(values)

folders=[p for p in EXP.iterdir() if (p/'summary.json').exists()]
for final_primary in [AUDIT/'regression/balance-012d-fix/final-199801',AUDIT/'verified/balance-012d-fix/final-199801']:
    if (final_primary/'summary.json').exists():folders.append(final_primary)
cases=[];independent=[]
cache_dir=AUDIT/'analysis-cache';cache_dir.mkdir(exist_ok=True)
def decimal_tag(obj):
    if isinstance(obj,Decimal):return {'__decimal__':str(obj)}
    raise TypeError(type(obj).__name__)
def decimal_untag(obj):
    return Decimal(obj['__decimal__']) if set(obj)=={'__decimal__'} else obj
analysis_code_hash=hashlib.sha256(Path(__file__).read_bytes().split(b"\n# END_INDEPENDENT_ANALYSIS\n")[0]).digest()
for folder in sorted(folders):
    fingerprint=hashlib.sha256(analysis_code_hash+str(folder.relative_to(ROOT)).encode())
    for filename in ['summary.json','seasons.json','clubs.json.gz','ledger.jsonl.gz']:
        with (folder/filename).open('rb') as raw:
            for chunk in iter(lambda:raw.read(1024*1024),b''):fingerprint.update(chunk)
    cache=cache_dir/(fingerprint.hexdigest()+'.json')
    if cache.exists():
        cached=json.loads(cache.read_text(encoding='utf-8'),object_hook=decimal_untag)
        cases.append(cached['Case']);independent.append(cached['Reconciliation']);continue
    summary=load(folder/'summary.json');years=load(folder/'seasons.json')
    with gzip.open(folder/'clubs.json.gz','rt',encoding='utf-8') as stream:club_years=json.load(stream,parse_float=Decimal)
    initial=load(folder/'initial-clubs.json');cash={c['Id']:Decimal(0) for c in initial};seen=set();keys=set();pairs=defaultdict(list)
    annual=defaultdict(lambda:defaultdict(Decimal));annual_club=defaultdict(lambda:defaultdict(lambda:defaultdict(Decimal)))
    highest_transfer=Decimal(0);prize_keys=[]
    with gzip.open(folder/'ledger.jsonl.gz','rt',encoding='utf-8') as stream:
        for line in stream:
            t=json.loads(line,parse_float=Decimal);amount=Decimal(t['Amount']);club=t['ClubId'];year=t['SeasonNumber'];category=t['Category']
            assert t['Id'] not in seen;seen.add(t['Id']);key=(club,t['IdempotencyKey']);assert key not in keys;keys.add(key)
            cash[club]+=amount
            if category=='InitialBalance':continue
            annual[year][category]+=amount;annual_club[year][club][category]+=amount
            if t.get('SellerClubId') and t.get('TransferTransactionId'):pairs[t['TransferTransactionId']].append(amount)
            if category in ['TransferPurchase','AuctionPurchase']:highest_transfer=max(highest_transfer,-amount)
            if category=='PrizeMoney':prize_keys.append((year,key))
    assert len(cash)==128 and all(len(p)==2 and sum(p)==0 for p in pairs.values())
    assert sum(cash.values())==summary['FinalCash']
    for year,club_year in zip(years,club_years):
        assert year['Season']==club_year['Season'];s=year['Season'];flow=sum(annual[s].values())
        assert year['TotalCash']==year['OpeningCash']+flow
        assert year['OpeningCash']+year['ExternalRevenue']-year['ExternalExpenses']+year['InternalTransfersNet']+year['CreditNet']==year['TotalCash']
        assert year['OpeningDebt']+year['Categories'].get('LoanDisbursement',0)+year['InterestCharged']-year['InterestPaid']-year['PrincipalPaid']==year['Debt']
        assert annual[s]==year['Categories'];assert year['ReconciliationDifference']==0
        assert median([c['Cash'] for c in club_year['Clubs']])==year['MedianCash']
        year['PositiveCash']=sum(max(Decimal(0),c['Cash']) for c in club_year['Clubs'])
        year['UnfundedCashDeficit']=sum(max(Decimal(0),-c['Cash']) for c in club_year['Clubs'])
        year['RecordedObligationsIncludingCashDeficit']=year['Debt']+year['UnfundedCashDeficit']
        for c in club_year['Clubs']:
            assert c['Cash']==c['OpeningCash']+sum(annual_club[s][c['Id']].values())
            assert c['SquadCount']>=11
        for division in year['ByDivision']:
            members=[c for c in club_year['Clubs'] if c['Division']==division['Division']]
            flows=defaultdict(Decimal)
            for c in members:
                for k,v in annual_club[s][c['Id']].items():flows[k]+=v
            division['CashFlow']=dict(flows)
            division['MedianCash']=median([c['Cash'] for c in members]);division['MaximumCash']=max(c['Cash'] for c in members);division['MinimumCash']=min(c['Cash'] for c in members)
    closing=club_years[-1]['Clubs'];late=years[75:];last=years[-1]
    growth=sum(y['TotalCash']-y['OpeningCash'] for y in late)/len(late)
    debt_growth=sum(y['Debt']-y['OpeningDebt'] for y in late)/len(late)
    categories={k:sum(y['Categories'].get(k,0) for y in years) for k in set(k for y in years for k in y['Categories'])}
    total_external=sum(y['ExternalRevenue'] for y in years)
    case={'Case':summary['Case'],'Seed':summary['Seed'],'Source':str(folder.relative_to(ROOT)),'Summary':summary,'Seasons':years,
        'Policy':summary.get('OperatingRules',{}),
        'FinalMedianCash':last['MedianCash'],'FinalMaximumCash':last['MaximumCash'],'FinalMinimumCash':last['MinimumCash'],
        'FinalInsolventClubs':last['InsolventClubs'],'PersistentInsolventClubs':last['PersistentInsolventClubs'],
        'LateAnnualCashChange':growth,'LateAnnualDebtChange':debt_growth,'LateCashMinimum':min(y['TotalCash'] for y in years[50:]),
        'LateCashMaximum':max(y['TotalCash'] for y in years[50:]),'CumulativeCategories':categories,
        'SalaryShareOfExternalRevenue':-categories.get('PlayerSalary',0)/total_external,
        'HighestPlayerPurchase':highest_transfer,'LargestCashAccumulations':sorted(closing,key=lambda c:c['Cash']-next(i['OpeningCash'] for i in initial if i['Id']==c['Id']),reverse=True)[:10],
        'ClosingInsolventClubs':[c for c in closing if c['Insolvent']]}
    cases.append(case);independent.append({'Case':summary['Case'],'Seed':summary['Seed'],'Transactions':len(seen),'ReconciledTransferPairs':len(pairs),
        'AnnualReconciliations':len(years),'ClubYearReconciliations':len(years)*128,'CashDifference':0,'DebtDifference':0,'FinalCash':summary['FinalCash']})
    cache.write_text(json.dumps({'Case':case,'Reconciliation':independent[-1]},ensure_ascii=False,default=decimal_tag),encoding='utf-8')
# END_INDEPENDENT_ANALYSIS
save(AUDIT/'independent-reconciliation.json',{'Status':'PASS','Cases':independent})
save(ROOT/'docs/ECONOMY_CASH_FLOW_012D.json',{'Task':'ELIFOOT_ECONOMY_LONG_TERM_BALANCE_012D_FIX','BaseCommit':'7529e69',
    'Metric':'Cash of all 128 clubs. Debt and assets separate. Stadium asset uses the preexisting credit proxy, not a real property appraisal.',
    'InternalTransfersConserved':True,'CashAndDebtReconciliationDifference':0,'Cases':cases})
print('Independently reconciled',len(cases),'experiments.')
if args.preview:raise SystemExit(0)
baseline=next(c for c in cases if c['Case']=='baseline' and c['Seed']==199801)
after=next(c for c in cases if c['Case']=='final' and c['Seed']==199801 and c['Policy'].get('Name')=='CLASSIC_OPERATING_BALANCE_012D_FIX_V2')
extras=[c for c in cases if c['Case']=='h6-quality-2p2' and c['Seed']!=199801]
assert len(extras)==2
before=baseline['Seasons'][-1];last=after['Seasons'][-1]
assert baseline['Summary']['FinalCash']==Decimal('18107128451.84')
h6_primary=next(c for c in cases if c['Case']=='h6-quality-2p2' and c['Seed']==199801)
assert all(a['TotalCash']==b['TotalCash'] and a['Debt']==b['Debt'] and a['Categories']==b['Categories'] for a,b in zip(after['Seasons'],h6_primary['Seasons']))
parameters=lambda c:{k:v for k,v in c['Policy'].items() if k!='Name'}
assert all(parameters(c)==parameters(after) for c in extras+[h6_primary])

def suite_result(path):
    tree=ET.parse(path);counter=tree.find('.//{*}Counters');unit=tree.find('.//{*}UnitTest');assert counter is not None and unit is not None
    return {'Suite':Path(unit.attrib['storage']).name,'Total':int(counter.attrib['total']),'Passed':int(counter.attrib['passed']),
        'Failed':int(counter.attrib.get('failed',0)),'File':str(path.relative_to(ROOT))}
suites=[]
for path in (AUDIT/'tests').glob('verified_net*.trx'):
    row=suite_result(path)
    suites.append(row)
assert len(suites)==4 and all(s['Total']==s['Passed'] and s['Failed']==0 for s in suites)
full_solution_total=sum(s['Total'] for s in suites)
domain_review=AUDIT/'tests/domain-verified-final.trx'
if domain_review.exists():
    review=suite_result(domain_review);assert review['Failed']==0 and review['Total']==review['Passed']
    suites=[s for s in suites if s['Suite'].lower()!='elifoot.domain.tests.dll']+[review]
total=sum(s['Total'] for s in suites)
assert 'No issues found!' in (AUDIT/'flutter-analyze.log').read_text(encoding='utf-8-sig')
assert 'All tests passed!' in (AUDIT/'flutter-tests.log').read_text(encoding='utf-8-sig')
assert after['Summary']['TotalMatchesCompleted']==35100
preserved=[]
base_manifest=load(AUDIT/'baseline/manifest.json')
protected=['MatchEngine.cs','DisciplineEngine.cs','TacticsAndSquadRules.cs','ClassicForceEvolutionEngine.cs','ClassicForceState.cs','PlayerStrengthEvolutionEngine.cs','AiManagerBrain.cs','CoachCareerEngine.cs','PlayerStatisticsEngine.cs','ClassicEconomyEngine.cs','FinancialInsolvencyEngine.cs','Entities.cs']
for name in protected:
    rel='src/Elifoot.Domain/'+name;p=ROOT/rel;actual=hashlib.sha256(p.read_bytes()).hexdigest();expected=next(r['Sha256'] for r in base_manifest['Files'] if r['Path']==rel)
    assert actual==expected;preserved.append({'Path':rel,'Sha256':actual})
other_head=subprocess.check_output(['git','rev-parse','feature/real-football-data-013b'],cwd=ROOT,text=True).strip()
base_market=subprocess.check_output(['git','show','7529e69:src/Elifoot.Domain/MarketAndEconomyEngine.cs'],cwd=ROOT).decode('utf-8-sig').replace('\r\n','\n')
current_market=(ROOT/'src/Elifoot.Domain/MarketAndEconomyEngine.cs').read_text(encoding='utf-8-sig')
assert base_market.split('    /// Automatically determines ticket price')[0]==current_market.split('    /// Automatically determines ticket price')[0]
gate_pass=all(c['Summary']['FinalCash']<baseline['Summary']['FinalCash']/10 for c in [after]+extras)
no_systemic_collapse=all(c['FinalInsolventClubs']<128/10 and c['Seasons'][-1]['RecordedObligationsIncludingCashDeficit']<c['Summary']['FinalCash']/10 for c in [after]+extras)
bounded_by_seed={str(c['Seed']):abs(c['LateAnnualCashChange'])<Decimal('0.01')*c['Summary']['FinalCash'] for c in [after]+extras}
status='PASS' if gate_pass and no_systemic_collapse and all(bounded_by_seed.values()) else 'PARTIAL'
verification={'Status':status,'Scope':'Observed 100-season sustainability; no proof over an infinite horizon. Individual unresolved crises and debts remain explicit.',
    'RootCauseUnderstood':True,'CashAndDebtReconciled':True,'FullSolutionTests':full_solution_total,'DotnetDistinctTests':total,'FlutterTests':61,'Suites':suites,
    'ProtectedFiles':preserved,'013BWorktreeNotWrittenByThisTask':True,'013BObservedHeadAtStart':'40e71b4c1424fe9c5942de405c39c457ef4deac6',
    '013BObservedHeadAtValidation':other_head,'013BNote':'The separate 013B task progressed on its own branch; no checkout, reset, merge or file write was performed there by this task.',
    'NoProductionPublish':True,'BaselineExact':True,
    'H6AndFinalPrimaryAnnualTrajectoryIdentical':True,'SecondarySeedsUseIdenticalEffectiveParameters':True,
    'PrimaryLateCashChange':after['LateAnnualCashChange'],'SecondarySeeds':[c['Seed'] for c in extras],
    'AcceptanceChecks':{'LargeSurplusRemovedAcrossSeeds':gate_pass,'NoGeneralizedInsolvency':no_systemic_collapse,'LateDriftBelowOnePercentOfStockPerYearBySeed':bounded_by_seed},
    'ThresholdNote':'The numerical diagnostic checks were applied after the initial calibration and are disclosed as diagnostics, not preregistered proof or product limits.'}
save(AUDIT/'verification.json',verification)
checkpoints=[1,5,10,25,50,75,100]
rows='\n'.join(f"| {n} | {money(baseline['Seasons'][n-1]['TotalCash'])} | {money(after['Seasons'][n-1]['TotalCash'])} | {money(after['Seasons'][n-1]['MedianCash'])} | {money(after['Seasons'][n-1]['MaximumCash'])} | {money(after['Seasons'][n-1]['Debt'])} | {after['Seasons'][n-1]['InsolventClubs']} |" for n in checkpoints)
case_rows='\n'.join(f"| {c['Case']} ({c['Policy'].get('GateQualityExponent',0)}) / {c['Seed']} | {money(c['Summary']['FinalCash'])} | {money(c['FinalMedianCash'])} | {money(c['Summary']['FinalDebt'])} | {money(c['LateAnnualCashChange'])} | {c['FinalInsolventClubs']} |" for c in cases)
flow_rows='\n'.join(f"| {k} | {money(baseline['CumulativeCategories'].get(k,0))} | {money(after['CumulativeCategories'].get(k,0))} |" for k in sorted(set(baseline['CumulativeCategories'])|set(after['CumulativeCategories'])))
top_rows='\n'.join(f"| {c['Name']} | {money(c['Cash'])} | {money(c['Cash']-next(i['OpeningCash'] for i in load(EXP/'baseline-199801/initial-clubs.json') if i['Id']==c['Id']))} |" for c in baseline['LargestCashAccumulations'])
root=f'''# Causa do acúmulo de caixa — 012D_FIX

Base `7529e69`, reproduzida integralmente: {money(before['TotalCash'])} em T100. Essa métrica soma **somente o caixa dos 128 clubes**. Não soma patrimônio ou empréstimos à parte; os desembolsos bancários já aparecem no caixa e têm dívida correspondente. Em T100 a dívida da baseline é zero. Elencos valem {money(before['AssetsRoster'])} pelo modelo de mercado e estádios {money(before['AssetsStadiumProxy'])} pela proxy do crédito, ambos separados.

A mediana é {money(before['MedianCash'])}; mínimo {money(before['MinimumCash'])}; máximo {money(before['MaximumCash'])}. Nenhum clube ficou com caixa baixo. Os dez maiores detêm {before['TopTenShare']*100:.2f}% do total. A série cresce aproximadamente de forma linear: R² 0,99923, contra 0,84958 para o logaritmo do caixa; a expansão tardia média foi {money(baseline['LateAnnualCashChange'])}/ano. Não se trata apenas de poucos clubes com crescimento exponencial.

## Causas demonstradas

1. O apoio financeiro introduzido na QA002/005/006 repete R$ 185/125/92/72/20 mil a cada rodada global. Os 96 distritais, mesmo eliminados, recebem as 22 parcelas integrais. A emissão anual recorrente é R$ 125.664.000 e soma R$ 12.566.400.000 em 100 temporadas. Isso era uma política intencional documentada, não uma transação duplicada. O problema é a dimensão econômica permanente desse apoio.
2. A bilheteria depende de divisão, reputação, campanha, capacidade e condição, mas ignorava a qualidade nominal dos elencos. A força e os salários de novos contratos caem com a dinâmica 012C, enquanto a receita de público permanece elevada. Bilheteria baseline: {money(baseline['CumulativeCategories']['TicketRevenue'])}. A folha representou apenas {baseline['SalaryShareOfExternalRevenue']*100:.2f}% das entradas externas legítimas.
3. A IA considerava o caixa emprestado no orçamento para compras e obras e tomava empréstimos para cobrir folha sem verificar capacidade operacional de suportar juros. Quando as receitas são recalibradas, esse uso transforma o crédito em financiamento de déficit recorrente. H3/H4 demonstraram crescimento de dívida mesmo com caixa agregado menor; foram rejeitadas.

Não houve evidência de duplicação de receitas, crédito ao vendedor sem débito do comprador, perdão de dívida ou dinheiro bancário tratado como receita definitiva. Os pares internos e as identidades anuais foram conferidos independentemente. Empréstimos/principal baseline somam zero em caixa; juros são despesa externa. Arremate de livre sai do universo de clubes, sem crédito fictício ao ex-clube. Prêmios e acesso são receita externa explícita, com chaves únicas, não transferência interna. A baseline não é economicamente neutra apenas porque é contabilmente correta.

## Dez maiores acumulações na baseline

| Clube | Caixa T100 | Aumento desde a abertura |
|---|---:|---:|
{top_rows}

Salários são contratados **por partida oficial**, conforme unidade já implementada e exibida. Não surgiu uma obrigação mensal em folgas. Jogador renovado continua na folha a cada partida; força não sobrescreve seu salário. Cobrar salário em toda rodada apenas para consumir dinheiro mudaria a unidade contratual, portanto não foi usado. Custos operacionais, manutenção e obras permanecem lançamentos reais; não foi criado imposto ou despesa de expurgo.

O JSON `ECONOMY_CASH_FLOW_012D.json` conserva todas as temporadas, divisões, extremos, mediana, patrimônio separado, fluxos e dez maiores. As referências anteriores consultadas incluem 005/006, `METRIC_AI_AUDIT_007.md`, `FINANCIAL_RECOVERY_007.json`, `ECONOMIA_ESTADIOS.md` e os relatórios integrais 012D. A assimetria de patrocínio e salários já era reconhecida pela 007; a presente missão autoriza sua recalibração.
'''
(ROOT/'docs/ECONOMY_CASH_ROOT_CAUSE_012D.md').write_text(root,encoding='utf-8')
comparison=f'''# 100 temporadas antes e depois — 012D_FIX

STATUS: {status}. Mesmo motor minuto a minuto, 128 clubes, força clássica, seed 199801 e sondas iniciais idênticas às da 012D. Cada caso concluiu 35.100 partidas; placares agregados não foram utilizados. Ativos são proxies de modelo, separados de caixa e dívida. Divisões são as vigentes durante a temporada observada; acessos/rebaixamentos aparecem separadamente nos registros por clube.

| Marco | Caixa antes | Caixa depois | Mediana depois | Maior caixa depois | Dívida depois | Insolventes |
|---:|---:|---:|---:|---:|---:|---:|
{rows}

## Hipóteses e seeds

| Caso / seed | Caixa T100 | Mediana | Dívida | Mudança anual média T76–T100 | Insolventes T100 |
|---|---:|---:|---:|---:|---:|
{case_rows}

H1 isolou o orçamento comercial anual; H2 acrescentou sensibilidade linear da demanda; H3 tornou-a quadrática; H4 reservou dívida no orçamento discricionário da IA; H5 exigiu superávit observado antes de novo crédito. H1/H2 não eliminaram superávit estrutural; H3/H4 diminuíram caixa mas deixaram endividamento crescente, motivo para rejeição. H5 estabilizou apenas a seed principal e foi mantida como comparação provisória V1. H6, expoente 2,2, obteve deriva tardia inferior a 1% do estoque por ano nas três seeds e foi incorporada como política V2. Resultados antigos não foram renomeados ou sobrescritos. A sequência e as decisões estão em `audit-012d-fix/HYPOTHESES.md`.

## Fluxos acumulados na comparação principal

| Categoria | Antes | Depois |
|---|---:|---:|
{flow_rows}

No cenário final principal, T51–T100 ficou entre {money(after['LateCashMinimum'])} e {money(after['LateCashMaximum'])}; a variação média em T76–T100 foi {money(after['LateAnnualCashChange'])}/ano. A folha passou a representar {after['SalaryShareOfExternalRevenue']*100:.2f}% das receitas externas. A maior compra efetiva foi {money(after['HighestPlayerPurchase'])}. O cenário teve {after['Summary']['Crises']} entradas em crise, {after['Summary']['Recovered']} recuperações, {after['Summary']['ForcedAuctions']} leilões por dívida e {after['Summary']['Unsold']} leilões novos sem comprador. Crises persistentes no fechamento: {last['PersistentInsolventClubs']}; elas permanecem explícitas, sem perdão de saldo ou liberação administrativa abaixo do mínimo.

A dívida da tabela é bancária. O JSON também separa caixa positivo e déficits de caixa negativos. Na principal, déficit de caixa não financiado de {money(last['UnfundedCashDeficit'])}, somado à dívida bancária, corresponde a {money(last['RecordedObligationsIncludingCashDeficit'])} de obrigações registradas; não foi escondido por compensação com o caixa dos clubes ricos.

Todas as seeds adicionais usam a mesma política e as mesmas sondas declaradas; muda a seed, portanto elencos, estádios, jogos e decisões podem divergir. O horizonte de 100 temporadas demonstra comportamento observado, não prova matemática para tempo infinito. As séries completas permitem distinguir saldo menor de sustentabilidade: caixa, dívida, fluxo, desigualdade, restrições de compra, crises e recuperação são analisados conjuntamente.

As seeds adicionais utilizam o rótulo experimental H6, que tem os mesmos parâmetros efetivos da política V2: anual, expoente 2,2, reserva de dívida e teste de superávit da IA. A seed principal foi repetida na regressão integral sob o rótulo de produto V2. O limiar de 1% é um diagnóstico quantitativo posterior à calibração inicial, explicitado para evitar selecionar apenas a seed favorável; ele não é limite imposto ao produto nem prova histórica. Existem crises persistentes, que não foram apagadas para cumprir esse diagnóstico macroeconômico.
'''
(ROOT/'docs/ECONOMY_100_SEASONS_BEFORE_AFTER_012D.md').write_text(comparison,encoding='utf-8')
fix=f'''# Correção do balanceamento — 012D_FIX

STATUS: {status}. Não houve teto de caixa, expurgo anual, imposto patrimonial, inflação de preços ou eliminação de dívida.

- **Termos comerciais anuais:** R$ 185.000/125.000/92.000/72.000/20.000 por temporada, em parcelas de centavos que somam exatamente o orçamento anual. A divisão inicial do contrato é persistida; mudança de divisão vale no contrato da próxima temporada. Distritais eliminados mantêm o patrocínio contratado, sem receber 22 orçamentos inteiros.
- **Demanda por qualidade:** multiplicar apenas a demanda de público por `((força média casa + força média visitante)/(referência casa + referência visitante))^2,2`. Referências iniciais: 41/33/26,5/21/15,5. São as médias dos intervalos iniciais de força por divisão, não uma indexação monetária anual. Preço de ingresso, piso/teto de público, elasticidade de preço, condição e custos anteriores permanecem. Lesionados e suspensos contam com força nominal; disponibilidade temporária não reduz artificialmente a qualidade comercial do plantel.
- **Orçamento da IA:** preservar a dívida a quitar no orçamento discricionário. O dinheiro continua no caixa; o banco não recebe sem débito real. Para novo empréstimo automático, o superávit observado dos últimos até 20 jogos, mínimo quatro, deve suportar principal e juros de mais 20 jogos. A projeção inclui bilheteria, patrocínio, folha, operação, manutenção e renovação, excluindo venda de patrimônio, prêmios e empréstimos. Treinadores humanos conservam autoridade para assumir riscos.
- **Saves:** mundos novos usam a política `CLASSIC_OPERATING_BALANCE_012D_FIX_V2`. Mundos antigos mantêm os termos em vigor e agendam a mudança para a próxima temporada; não se retira caixa acumulado, não se recalcula folha e não se altera empréstimo antigo. O agendamento e os termos sobrevivem no JSON do mundo, sem migration relacional destrutiva.

O banco de R$ 500.000/R$ 25.000, crédito individual, limite 1/2 na quarta, propostas salariais, teto de um pedido por rodada, recusa, insolvência e prioridades de venda permanecem. A mudança da decisão financeira da IA não muda elegibilidade bancária de humanos, taxa ou cobrança. O MatchEngine e todos os arquivos protegidos da 012C foram comparados por SHA-256 com `7529e69`.

Arquivos centrais: `OperatingEconomyEngine.cs`, estado econômico, liquidação de mercado, decisão bancária da IA, factory, agendamento na virada de temporada e carregamento do repositório. API/Flutter mostram orçamento anual parcelado e folha por partida. Testes novos cobrem identidade anual, distritais sem jogo, preservação do salário, crédito/juros, agendamento, replay, concorrência e decisões de crédito. Dois testes antigos que exigiam patrocínio integral por rodada tiveram expectativas corrigidas; nenhum caso foi removido.

Limitações: parâmetros de demanda/comercial são adaptação de projeto, sem alegação de regra histórica comprovada. Ainda há crises individuais persistentes, inclusive uma sonda inicial de empréstimo, e seu saldo não é apagado. Não se exige riqueza igual entre clubes nem ausência de risco. Esta tarefa não escreveu no worktree da 013B (`feature/real-football-data-013b`), que progrediu independentemente de 40e71b4 para {other_head[:7]}. Não houve checkout, reset, merge ou intervenção nessa branch. Nenhuma publicação.
'''
(ROOT/'docs/ECONOMY_BALANCE_FIX_012D.md').write_text(fix,encoding='utf-8')
table='\n'.join(f"| {s['Suite']} | {s['Passed']} | {s['Failed']} | `{s['File']}` |" for s in suites)
checkpoint_file=AUDIT/'checkpoint.json'
checkpoint_ref=load(checkpoint_file).get('ImplementationCommit') if checkpoint_file.exists() else 'Consultar audit-012d-fix/checkpoint.json, emitido após o commit local.'
validation=f'''# ELIFOOT_ECONOMY_LONG_TERM_BALANCE_012D_FIX

STATUS: {status}.

ROOT_CAUSE: emissão comercial integral em cada rodada, demanda de público dissociada da força nominal e crédito automático da IA sem teste de capacidade operacional; não foi duplicação contábil.

INITIAL_T100_CASH: {money(before['TotalCash'])}.
FINAL_T100_CASH: {money(last['TotalCash'])}.
INITIAL_TOP_CLUB_CASH: {money(before['MaximumCash'])}.
FINAL_TOP_CLUB_CASH: {money(last['MaximumCash'])}.
INITIAL_MEDIAN_CLUB_CASH: {money(before['MedianCash'])}.
FINAL_MEDIAN_CLUB_CASH: {money(last['MedianCash'])}.

REVENUE_ANALYSIS: patrocínio e bilheteria recalibrados causalmente; prêmios preservados e registrados; venda entre clubes conserva dinheiro.
EXPENSE_ANALYSIS: folha, operação, manutenção, obras, contratação de livres e juros conferidos; nenhuma despesa fictícia.
SPONSORSHIP: orçamento anual por contrato, em 22 parcelas exatas.
DISTRICT_CLUBS: orçamento persiste após eliminação, sem multiplicação por 22; dados segregados no JSON.
SALARIES: contrato por partida, salário negociado intacto quando a força muda; sem cobrança indevida em folgas.
STADIUM_COSTS: parâmetros anteriores preservados; IA respeita reserva de dívida para gastos discricionários.
BANK_LOANS: principal R$ 500.000; juros R$ 25.000 por jogo elegível; dívida separada de renda.
AUCTIONS: regras anteriores preservadas, inclusive livre sem receita ao ex-clube e insolvência com vínculo até venda.
FINANCIAL_RECONCILIATION: zero diferença de caixa e dívida em cada temporada; 12.800 identidades clube/temporada por caso; pares internos únicos de soma zero.
INSOLVENCY: {last['InsolventClubs']} no fechamento principal; {after['Summary']['Recovered']} recuperações; dívida final {money(last['Debt'])}, preservada.
INFLATION: nenhuma indexação de salário ou valor justo; orçamento e demanda têm unidades explícitas.
100_SEASONS: baseline e candidata, mais seeds 199802/199803 e todas as hipóteses registradas.
REAL_MATCHES: 35.100 por caso completo; candidata principal {after['Summary']['RealMinutesSimulated']:,} minutos reais.
DOTNET_TESTS: solução integral {full_solution_total}/{full_solution_total}; {total} casos distintos aprovados após a verificação adicional de juros legados; baseline de 410 casos mantida, com expectativas comerciais atualizadas.
FLUTTER_TESTS: 61 aprovados; análise limpa.
DETERMINISTIC_REPLAY: baseline exata e replay clássico de duas temporadas na regressão, além de persistência/reprocessamento financeiro.
012C_PRESERVED: hashes de motor, força, lesões, disciplina, táticas, IA tática, carreira e estatísticas idênticos à base.
013B_PRESERVED: nenhum arquivo escrito por esta tarefa no worktree separado; a tarefa 013B avançou independentemente de 40e71b4 para {other_head[:7]}.
CHECKPOINT_COMMIT: {checkpoint_ref}

## Testes reais

Build Release sem erro ou aviso. A primeira execução completa encontrou uma expectativa antiga de recuperar R$ 350 mil com três repasses; ela foi corrigida para as parcelas realmente devidas. A tentativa paralela de recompilar integração encontrou DLL em uso no Windows; a saída global experimental também revelou um fixture antigo dependente da profundidade da pasta. A execução integral final usou a mesma profundidade de saída em cada projeto (`bin/BalanceFix/net10.0`) e passou nas quatro suítes. A verificação adicional da projeção de juros legados repetiu toda a suíte de domínio; ela não converte o empréstimo antigo e não modifica os dados das sondas de 100 temporadas, cujo empréstimo legado já estava quitado antes de qualquer decisão de novo crédito. Logs de tentativas foram conservados.

| Suíte | Aprovados | Falhas | TRX final |
|---|---:|---:|---|
{table}

## Sustentabilidade e limites

O status não depende apenas de T100 menor: exige reconciliação, comparação causal das variantes, deriva tardia do estoque, dívida examinada, crises/recuperações, pluralidade de seeds e preservação das regras. O período final observado não reproduz o superávit universal de R$ 192 milhões/ano da baseline. H5 mantinha crescimento tardio de R$ 6,38/R$ 4,22 milhões por ano nas seeds adicionais e foi substituída por H6/V2. Com V2, os três diagnósticos de deriva tardia ficaram abaixo de 1% do estoque por ano, sem insolvência generalizada ou dívida agregada dominante. Persistem crises individuais e dívida não quitada; não se declarou recuperação fictícia. O critério é observacional por 100 temporadas e não garante equilíbrio em horizonte infinito ou validação de PostgreSQL/produção.

FILES_CHANGED: listados em `audit-012d-fix/final-manifest.json`.
KNOWN_ISSUES: crises individuais persistentes; adaptação comercial/elasticidade sem comprovação histórica; saldos de saves antigos não são confiscados; limites do horizonte e dos ambientes testados.

Os relatórios anteriores da 012D foram preservados como evidência histórica; a comparação e a decisão desta missão estão nos cinco arquivos novos solicitados. Nenhuma publicação em produção.

Na principal foram {sum(y['LoanDisbursements'] for y in after['Seasons'])} empréstimos (incluindo as sondas), {money(sum(y['InterestCharged'] for y in after['Seasons']))} de juros cobrados, {money(sum(y['InterestPaid'] for y in after['Seasons']))} de juros pagos e {money(sum(y['PrincipalPaid'] for y in after['Seasons']))} de principal quitado. Os juros vencidos permanecem no saldo. Déficits de caixa sem financiamento somam {money(last['UnfundedCashDeficit'])}; com a dívida bancária, são {money(last['RecordedObligationsIncludingCashDeficit'])} de obrigações registradas. Não se exige quitação fictícia das crises remanescentes para declarar a correção do excesso macroeconômico.
'''
(ROOT/'docs/ECONOMY_FINAL_VALIDATION_012D.md').write_text(validation,encoding='utf-8')
print('Reports generated; overall',status,'tests',total)
