from pathlib import Path
from decimal import Decimal
from collections import defaultdict
import json,gzip,hashlib
ROOT=Path(__file__).resolve().parents[1]; AUDIT=ROOT/'docs/audit-014a'; BASE=AUDIT/'verified-final'
D=Decimal

def read(path):return json.loads(path.read_text(encoding='utf-8-sig'),parse_float=D)
def serial(value):
    if isinstance(value,D):return format(value,'f')
    raise TypeError(type(value).__name__)
def dump(path,data):path.write_text(json.dumps(data,default=serial,ensure_ascii=False,indent=2),encoding='utf-8')

def reconcile(folder):
    folder=folder.resolve()
    summary=read(folder/'summary.json'); seasons=read(folder/'seasons.json')
    assert len(seasons)==100
    categories=defaultdict(lambda:defaultdict(D)); clubs=defaultdict(D); club_year=defaultdict(lambda:defaultdict(D))
    seen=set();keys=set();pairs=defaultdict(lambda:[0,D(0)]);rows=0;initial=D(0)
    with gzip.open(folder/'ledger.jsonl.gz','rt',encoding='utf-8') as stream:
        for line in stream:
            tx=json.loads(line,parse_float=D);rows+=1
            assert tx['Id'] not in seen;seen.add(tx['Id'])
            key=(tx['ClubId'],tx['IdempotencyKey']);assert key not in keys;keys.add(key)
            amount=D(tx['Amount']);club=tx['ClubId'];season=tx['SeasonNumber']
            clubs[club]+=amount
            assert clubs[club]==D(tx['BalanceAfter']),('BalanceAfter',club,rows)
            if tx['Category'] in (0,'InitialBalance'):initial+=amount;continue
            # Accept either enum encoding; the checked-in converter currently writes names.
            category=CATEGORIES[int(tx['Category'])] if isinstance(tx['Category'],int) else tx['Category']
            categories[season][category]+=amount;club_year[season][club]+=amount
            if tx.get('SellerClubId') is not None and tx.get('TransferTransactionId'):
                pair=pairs[tx['TransferTransactionId']];pair[0]+=1;pair[1]+=amount
    assert initial==D(summary['InitialCash']);assert rows==summary['Transactions']
    assert all(count==2 and net==0 for count,net in pairs.values())
    with gzip.open(folder/'clubs.json.gz','rt',encoding='utf-8') as stream:club_series=json.load(stream,parse_float=D)
    previous={};year_reports=[]
    for year,series in zip(seasons,club_series,strict=True):
        number=year['Season'];assert series['Season']==number
        actual=categories[number];assert dict(actual)=={k:D(v) for k,v in year['Categories'].items()}
        assert D(year['OpeningCash'])+sum(actual.values())==D(year['TotalCash'])
        assert D(year['OpeningDebt'])+actual.get('LoanDisbursement',D(0))+D(year['InterestCharged'])-D(year['InterestPaid'])-D(year['PrincipalPaid'])==D(year['Debt'])
        rows_by_club=series['Clubs'];assert len(rows_by_club)==128
        for club in rows_by_club:
            assert D(club['OpeningCash'])+club_year[number][club['Id']]==D(club['Cash'])
            if number>1:assert previous[club['Id']]==D(club['OpeningCash'])
            assert 11<=club['SquadCount']<=24
        previous={c['Id']:D(c['Cash']) for c in rows_by_club}
        values=sorted(previous.values());assert sum(values)==D(year['TotalCash'])
        assert (values[63]+values[64])/2==D(year['MedianCash'])
        year_reports.append({'Season':number,'CashDifference':D(0),'DebtDifference':D(0),'ClubCashDifferences':0,'TransferNet':D(year['InternalTransfersNet'])})
    assert sum(clubs.values())==D(summary['FinalCash']);assert clubs==previous
    assert summary['TotalMatchesCompleted']==35100 and summary['RealMinutesSimulated']>=3159000
    final=seasons[-1];late=seasons[75:];growth=sum(D(y['TotalCash'])-D(y['OpeningCash']) for y in late)/len(late)
    return {'Directory':str(folder.relative_to(ROOT)),'Summary':summary,'MedianCash':D(final['MedianCash']),
        'MinimumCash':D(final['MinimumCash']),'MaximumCash':D(final['MaximumCash']),
        'LateAnnualCashChange':growth,'FinalInsolventClubs':final['InsolventClubs'],'FinalPersistentInsolventClubs':final['PersistentInsolventClubs'],
        'CashDeficits':sum(-D(c['Cash']) for c in club_series[-1]['Clubs'] if D(c['Cash'])<0),
        'InterestCharged':sum(D(y['InterestCharged']) for y in seasons),'InterestPaid':sum(D(y['InterestPaid']) for y in seasons),
        'PrincipalPaid':sum(D(y['PrincipalPaid']) for y in seasons),
        'FinalDivisionCash':final['ByDivision'],'FinalTopTen':final['TopTen'],
        'LedgerRows':rows,'TransferPairs':len(pairs),'ClubYearIdentities':12800,'AnnualReconciliation':year_reports,
        'InputSha256':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [folder/'summary.json',folder/'seasons.json',folder/'ledger.jsonl.gz',folder/'clubs.json.gz']}}

def category_map():
    import re
    source=(ROOT/'src/Elifoot.Contracts/Enums.cs').read_text(encoding='utf-8-sig')
    body=source.split('enum FinancialTransactionCategory',1)[1].split('{',1)[1].split('}',1)[0]
    result={};index=0
    for line in body.splitlines():
        line=line.split('//',1)[0].strip().rstrip(',')
        if not line:continue
        parts=line.split('=');name=parts[0].strip()
        if len(parts)>1:index=int(parts[1].strip())
        result[index]=name;index+=1
    return result
CATEGORIES=category_map()
if __name__=='__main__':
    cases={}
    for name,folder in [('Seed012D',BASE/'balance-012d-fix/final-199801'),('Male',BASE/'integration-014a/Male'),('Female',BASE/'integration-014a/Female')]:
        cases[name]=reconcile(folder);print(name,cases[name]['Summary']['FinalCash'],'reconciled',cases[name]['LedgerRows'],'transactions',flush=True)
    approved=read(ROOT/'docs/audit-012d-fix/verified/balance-012d-fix/final-199801/summary.json')
    assert D(cases['Seed012D']['Summary']['FinalCash'])==D(approved['FinalCash'])==D('137353919.79')
    assert D(cases['Seed012D']['Summary']['FinalDebt'])==D(approved['FinalDebt'])
    dump(AUDIT/'financial-reconciliation.json',{'Status':'PASS','Engine':'Existing MatchSimulationEngine.SimulateFullMatchReal','Cases':cases,'Approved012DBaselineMatched':True})
