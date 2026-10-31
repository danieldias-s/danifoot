from pathlib import Path
from decimal import Decimal
from collections import defaultdict
import gzip,json,sys

root=Path(__file__).resolve().parents[1]
case=sys.argv[1] if len(sys.argv)>1 else 'h3-quality-square-199801'
folder=root/'docs/audit-012d-fix/experiments/balance-012d-fix'/case
initial=json.loads((folder/'initial-clubs.json').read_text(encoding='utf-8'))
names={c['Id']:c['Name'] for c in initial};probes={c['Id']:role for c,role in zip(initial[24:28],['capital-probe-1','capital-probe-2','legacy-probe','distress-probe'])}
principal=defaultdict(Decimal);optional=defaultdict(Decimal);under_reserved=defaultdict(Decimal);clubs=defaultdict(Decimal);count=0
with gzip.open(folder/'ledger.jsonl.gz','rt',encoding='utf-8') as stream:
    for line in stream:
        t=json.loads(line,parse_float=Decimal);club=t['ClubId'];amount=Decimal(t['Amount'])
        if t['Category'] in ['LoanDisbursement','LoanPrincipalRepayment']:principal[club]+=amount
        if principal[club]<=0 or amount>=0 or t['Category'] not in ['StadiumMaintenance','StadiumRenovation','StadiumExpansion','TransferPurchase','AuctionPurchase'] or t['IdempotencyKey'].startswith('012d-'):continue
        optional[t['Category']]-=amount;count+=1
        portion=min(-amount,max(Decimal(0),principal[club]-Decimal(t['BalanceAfter'])))
        under_reserved[t['Category']]+=portion;clubs[club]+=portion
rows=json.load(gzip.open(folder/'clubs.json.gz','rt',encoding='utf-8'))[-1]['Clubs']
result={'Case':case,'OptionalTransactionsWhilePrincipalOutstanding':count,'OptionalSpendingWhilePrincipalOutstanding':dict(optional),
    'SpendingBelowPrincipalReserve':dict(under_reserved),'ClubsWithSpendingBelowPrincipal':[{'Id':k,'Name':names[k],'Amount':v} for k,v in clubs.items()],
    'ClosingInsolventClubs':[dict(c,InitialProbe=probes.get(c['Id'],'none')) for c in rows if c['Insolvent']]}
out=root/'docs/audit-012d-fix'/('ai-capital-'+case+'.json');out.write_text(json.dumps(result,ensure_ascii=False,indent=2,default=str),encoding='utf-8')
print(json.dumps({k:result[k] for k in ['OptionalTransactionsWhilePrincipalOutstanding','OptionalSpendingWhilePrincipalOutstanding','SpendingBelowPrincipalReserve']},default=str))
