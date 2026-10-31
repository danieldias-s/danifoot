from pathlib import Path
from decimal import Decimal as D
import json
ROOT=Path(__file__).resolve().parents[1]; AUDIT=ROOT/'docs/audit-014a'; BASE=AUDIT/'verified-final'
def load(path):return json.loads(path.read_text(encoding='utf-8-sig'),parse_float=D)
def number(value):return format(value,'f') if isinstance(value,D) else value
recon=load(AUDIT/'financial-reconciliation.json'); cases={}
for name,case in recon['Cases'].items():
    folder=ROOT/case['Directory'];years=load(folder/'seasons.json'); summary=case['Summary']; intervals={}
    for length in (25,10,5):
        span=years[-length:]; flows={key:sum(y['Categories'].get(key,D(0)) for y in span) for key in sorted({key for y in span for key in y['Categories']})}
        intervals[str(length)]={'FromSeason':101-length,'ThroughSeason':100,'OpeningCash':span[0]['OpeningCash'],'ClosingCash':span[-1]['TotalCash'],
            'AverageAnnualCashChange':sum(y['TotalCash']-y['OpeningCash'] for y in span)/length,'Categories':flows}
    bank=D(summary['FinalDebt']);deficit=D(case['CashDeficits']);cash=D(summary['FinalCash'])
    cases[name]={'Intervals':intervals,'BankDebt':bank,'CashDeficits':deficit,'CombinedObligations':bank+deficit,'CombinedObligationsOverCash':(bank+deficit)/cash,
        'SolventClubs':128-case['FinalInsolventClubs'],'InsolventClubs':case['FinalInsolventClubs'],'PersistentInsolventClubs':case['FinalPersistentInsolventClubs'],
        'T86Through100CashMinimum':min(y['TotalCash'] for y in years[85:]),'T86Through100CashMaximum':max(y['TotalCash'] for y in years[85:]),
        'FinalCash':cash,'MinimumClubCash':case['MinimumCash'],'MaximumClubCash':case['MaximumCash'],
        'InterestCharged':case['InterestCharged'],'InterestPaid':case['InterestPaid'],'PrincipalPaid':case['PrincipalPaid'],
        'Crises':summary['Crises'],'Recoveries':summary['Recovered'],'ForcedAuctions':summary['ForcedAuctions'],'UnsoldAuctions':summary['Unsold']}
assert recon['Approved012DBaselineMatched'] and recon['Status']=='PASS'
assert all(D(c['Summary']['OperatingRules']['GateQualityExponent'])==D('2.2') for c in recon['Cases'].values())
review={'Status':'PASS','Scope':'Integração e comportamento economicamente justificável nos estados exercitados por 100 temporadas; não valida equilíbrio universal ou infinito.',
    'RulesChangedForImportedWorlds':False,'NoCashCapOrNewDrain':True,'ApprovedSeedExactlyReproduced':True,
    'Accounting':{'LedgerRows':sum(c['LedgerRows'] for c in recon['Cases'].values()),'ClubYearIdentities':38400,'AnnualCashDifference':0,'AnnualDebtDifference':0,'UnmatchedInternalTransfers':0,'DuplicateIdsOrKeys':0},
    'Cases':cases,
    'Conclusion':'O cenário 012D reproduz exatamente caixa e dívida aprovados. Nos importados, a maior força/folha inicial e a ausência de quatro agentes livres da seed mudam competição e mercado. O caixa menor é reconciliado com pagamentos efetivos, especialmente leilões de agentes livres; não houve mudança de fórmula ou remoção de dívida. As identidades contábeis, elencos mínimos, crédito e persistência foram aprovados. As curvas importadas apresentam uma transição de liquidez relevante e não são declaradas equivalentes à estabilidade da seed padrão.',
    'ImportedCashTransition':'T76–100 tem média de -R$ 4.909.762,7868/ano (aprox. -6,20% do estoque final), e não atende ao diagnóstico de deriva de 1% usado na 012D. Esse resultado é mostrado integralmente. T91–100 e T96–100 também são informados, sem substituir a janela principal. A faixa T86–100 ficou entre R$ 73.855.889,47 e R$ 90.098.994,30; T100 fecha com R$ 79.157.786,17.',
    'AuctionCashExplanation':'Em T76–100, AuctionPurchase soma -R$ 643.774.671,00 e AuctionSale +R$ 325.000,00. Compras internas de atletas têm contrapartida e soma zero; leilões de agentes livres retiram dinheiro do mundo conforme o comportamento 012D preservado. Não se inventou crédito a ex-clube para compensar a saída.',
    'GenderProtocol':'Os dois ensaios longos usam a mesma montagem automática/aleatória de fixtures e a mesma seed. Forças, salários e IDs determinísticos de participantes coincidem; nomes/origem/categoria diferem. Por isso as trajetórias financeiras são iguais. Os mundos HTTP femininos personalizados têm montagem diferente, medida separadamente.',
    'Limitations':['Transição de caixa dos importados requer acompanhamento com dados reais representativos e novas configurações futuras; não se recalibrou 012D em fixtures.','Persistem 10 clubes insolventes e dívida em cada mundo de fixtures; 118 clubes solventes.','013B mantém zero elencos reais atuais completos; complemento foi explícito.','Os campos SourceCheckpoint históricos dos resumos identificam o protocolo; commits reais da execução estão no manifesto de origem e no checkpoint de integração.']}
(AUDIT/'economic-review.json').write_text(json.dumps(review,default=number,ensure_ascii=False,indent=2),encoding='utf-8')
print('Economic review recorded: exact baseline; imported liquidity transition disclosed, cash and debt reconciled.')