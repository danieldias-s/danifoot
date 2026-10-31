"""Read-only reconciliation of the 012D baseline cash series, in exact decimal money."""
from pathlib import Path
from collections import defaultdict
from decimal import Decimal
import gzip,json,statistics,math

root=Path(__file__).resolve().parents[1]
audit=root/'docs/audit-012d-fix'
source=root/'docs/audit-012d/solution-final/economy-012d'
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'),parse_float=Decimal)
old=load(source/'100-seasons.json')
initial=load(audit/'experiments/balance-012d-fix/baseline-199801/initial-clubs.json')
names={c['Id']:c['Name'] for c in initial}
cash=defaultdict(Decimal);categories=defaultdict(Decimal);ids=set();keys=set();pairs=defaultdict(list);years=[];season=1
def snapshot(year):
    values=sorted(cash.values());total=sum(values)
    return {'Season':year,'Cash':total,'MeanCash':total/len(values),'MedianCash':statistics.median(values),'MaximumCash':values[-1],'MinimumCash':values[0],
        'TopTenShare':sum(values[-10:])/total,'TopTen':[{'Id':k,'Name':names[k],'Cash':v} for k,v in sorted(cash.items(),key=lambda kv:kv[1],reverse=True)[:10]],
        'Categories':dict(categories),'NegativeClubs':sum(v<0 for v in values)}
with gzip.open(source/'ledger.jsonl.gz','rt',encoding='utf-8') as stream:
    for line in stream:
        tx=json.loads(line,parse_float=Decimal);year=tx['SeasonNumber']
        if year!=season:
            assert year==season+1;years.append(snapshot(season));categories.clear();season=year
        assert tx['Id'] not in ids;ids.add(tx['Id'])
        key=(tx['ClubId'],tx['IdempotencyKey']);assert key not in keys;keys.add(key)
        cash[tx['ClubId']]+=Decimal(tx['Amount']);categories[tx['Category']]+=Decimal(tx['Amount'])
        if tx.get('SellerClubId') and tx.get('TransferTransactionId'):pairs[tx['TransferTransactionId']].append(Decimal(tx['Amount']))
years.append(snapshot(season))
assert len(years)==100 and len(cash)==128
for actual,expected in zip(years,old['Seasons']):assert actual['Cash']==expected['TotalCash']
assert all(len(p)==2 and sum(p)==0 for p in pairs.values())
x=list(range(1,101));y=[float(v['Cash']) for v in years]
def regression(values):
    xm=statistics.mean(x);ym=statistics.mean(values);slope=sum((a-xm)*(b-ym) for a,b in zip(x,values))/sum((a-xm)**2 for a in x)
    intercept=ym-slope*xm;residual=sum((b-(intercept+slope*a))**2 for a,b in zip(x,values));ss=sum((b-ym)**2 for b in values)
    return {'Slope':slope,'Intercept':intercept,'R2':1-residual/ss}
result={'SourceCheckpoint':'7529e69','Metric':'Sum of cash of all 128 clubs; excludes asset valuations and debt. Loans are reconciled separately.',
    'UniqueTransactions':len(ids),'InternalTransferPairs':len(pairs),'InternalTransferDifference':0,'CashSeriesMatchesOriginal':True,
    'FinalCash':sum(cash.values()),'ClosingClubs':[{'Id':k,'Name':names[k],'Cash':v} for k,v in sorted(cash.items())],
    'LinearCashRegression':regression(y),'LogCashRegression':regression([math.log(v) for v in y]),'Seasons':years}
(audit/'baseline/existing-ledger-analysis.json').write_text(json.dumps(result,ensure_ascii=False,indent=2,default=str),encoding='utf-8')
print(json.dumps({k:result[k] for k in ['FinalCash','UniqueTransactions','InternalTransferPairs','LinearCashRegression','LogCashRegression']},default=str))
