import json,collections
from decimal import Decimal
from pathlib import Path
import runpy
api=runpy.run_path(str(Path(__file__).with_name('smoke-integration-014a.py')))
ROOT,AUDIT,LOCAL,call=(api[key] for key in ['ROOT','AUDIT','LOCAL','call'])
D=Decimal
context=LOCAL/'initial-baseline-context.json'
if context.exists(): baseline=json.loads(context.read_text())
else:
    response=call(5140,'/api/worlds',{'worldName':'014A referência inicial 012D','seed':199801,'totalClubs':128,'maxHumans':8,'forceEvolutionMode':1})
    baseline={'World':response['gameWorldId'],'Port':5140,'Gender':'Male','Reference012D':True}
    context.write_text(json.dumps(baseline),encoding='utf-8')
contexts=[baseline]+json.loads((LOCAL/'smoke-context.json').read_text())
results=[]
for item in contexts:
    divisions=collections.defaultdict(lambda:{'Clubs':0,'Players':0,'StrengthSum':0,'SalaryPerOfficialMatch':D(0),'RosterFairValue':D(0)})
    for club in call(item['Port'],f"/api/worlds/{item['World']}/clubs"):
        row=divisions[club['divisionTier']];row['Clubs']+=1
        detail=call(item['Port'],f"/api/worlds/{item['World']}/clubs/{club['id']}")
        for player in detail['squad']:
            row['Players']+=1;row['StrengthSum']+=player['strength'];row['SalaryPerOfficialMatch']+=D(str(player['salaryPerRound']));row['RosterFairValue']+=D(str(player['fairValue']))
    for row in divisions.values():row['MeanStrength']=D(row.pop('StrengthSum'))/row['Players']
    results.append({'World':item['World'],'Port':item['Port'],'Gender':item['Gender'],'Reference012D':item.get('Reference012D',False),'BirthCash':138800000,
        'NoSportingRoundAdvanced':True,'HttpLoanProbeDoesNotChangeWagesOrStrength':True,'Divisions':dict(divisions)})
    print(item['Port'],item['Gender'],'reference' if item.get('Reference012D') else 'created world','initial squad economics measured',flush=True)
(AUDIT/'initial-economy.json').write_text(json.dumps(results,default=str,ensure_ascii=False,indent=2),encoding='utf-8')
