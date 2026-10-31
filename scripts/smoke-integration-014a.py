from pathlib import Path
import urllib.request, urllib.error, urllib.parse, json, uuid, hashlib, sys
ROOT=Path(__file__).resolve().parents[1]
AUDIT=ROOT/'docs/audit-014a'; LOCAL=ROOT/'artifacts/014a'
COUNTRIES=['BR','AR','GB','ES','DE','IT','FR','PT']

def call(port,path,body=None,coach=None,method=None):
    headers={'Content-Type':'application/json'}
    if coach: headers['Authorization']='Bearer localdev-'+coach.replace('-','')
    data=None if body is None else json.dumps(body).encode()
    request=urllib.request.Request(f'http://127.0.0.1:{port}'+path,data=data,headers=headers,method=method or ('POST' if body is not None else 'GET'))
    try:
        with urllib.request.urlopen(request,timeout=90) as response:
            raw=response.read(); return json.loads(raw) if raw else None
    except urllib.error.HTTPError as error:
        raise RuntimeError(f'{port} {path}: HTTP {error.code} '+error.read().decode()[:1000]) from error

def fingerprint(port,world,club):
    finance=call(port,f'/api/worlds/{world}/clubs/{club}/finances')
    detail=call(port,f'/api/worlds/{world}/clubs/{club}')
    data={'cash':finance['cashBalance'],'loans':finance['loans'],'contracts':finance['contracts'],
          'salaryRequests':finance['salaryRequests'],'ledger':finance['ledger'],
          'players':[{key:p.get(key) for key in ['id','clubId','strength','salaryPerRound','contractRoundsRemaining']} for p in detail['squad']]}
    return hashlib.sha256(json.dumps(data,sort_keys=True,ensure_ascii=False,separators=(',',':')).encode()).hexdigest()

def verify():
    contexts=json.loads((LOCAL/'smoke-context.json').read_text(encoding='utf-8'))
    results=[]
    for item in contexts:
        port=item['Port']; world=item['World']; club=item['Club']; coach=item['Coach']
        assert fingerprint(port,world,club)==item['FinancialHash'], 'Financial state changed on restart'
        confirmation=call(port,'/api/world-creator/confirm/'+item['Draft'],{},coach)
        assert confirmation['id']==world
        call(port,f'/api/worlds/{world}/clubs/{club}/loans?'+urllib.parse.urlencode({'coachId':coach,'amount':500000,'rounds':0,'idempotencyKey':'014a-live-loan'}),{},coach)
        assert fingerprint(port,world,club)==item['FinancialHash'], 'Reconnection duplicated loan'
        assert len(call(port,f'/api/worlds/{world}/clubs/{club}/finances')['loans'])==1
        results.append({'World':world,'Port':port,'Gender':item['Gender'],'FinancialStateUnchanged':True,'DraftIdempotent':True,'LoanReplayIdempotent':True})
    for item in contexts:
        foreign=next(c for c in contexts if c['Port']==item['Port'] and c['World']!=item['World'])
        try:
            call(item['Port'],f"/api/worlds/{foreign['World']}/clubs/{foreign['Club']}/loans?"+urllib.parse.urlencode({'coachId':item['Coach'],'amount':500000,'rounds':0,'idempotencyKey':'014a-foreign-room'}),{},item['Coach'])
        except RuntimeError as error:
            assert 'HTTP 400' in str(error) or 'HTTP 403' in str(error)
        else: raise AssertionError('Foreign-room financial command was accepted')
        assert fingerprint(item['Port'],foreign['World'],foreign['Club'])==foreign['FinancialHash']
    (AUDIT/'api-restart.json').write_text(json.dumps({'Status':'PASS','CrossRoomFinancialAuthorization':'PASS','Worlds':results},indent=2),encoding='utf-8')
    print('PASS: four HTTP worlds, snapshots and negotiated financial state unchanged after real restart.')

def create():
    evidence=[]; contexts=[]
    for real in [False,True]:
        port=5141 if real else 5140
        assert call(port,'/api/health')
        for gender in ['Male','Female']:
            manual=gender=='Female'; owner=str(uuid.uuid4())
            request={'name':f'014A {gender} '+('clubes reais com complemento fictício' if real else 'fixtures fictícias'),
                'gender':gender,'season':'2026','countries':COUNTRIES,'mode':'Custom' if manual else 'Automatic',
                'districtMode':'Manual' if manual else 'Random','seed':199801,'assignments':[],
                'fillRemainingDivisions':not manual,'allowFictionalTeams':False,'allowFictionalPlayers':real,'useSavedData':True,'maxHumans':8}
            options=call(port,'/api/world-creator/countries?season=2026',coach=owner)
            assert len(options)==8
            selection=call(port,'/api/world-creator/catalog',request,owner)
            clubs=selection['clubs']
            eligible=[c for c in clubs if c.get('verification') is None or c['verification']['status']=='REAL_CLUB_VERIFIED']
            assert len(eligible)>=128
            if manual:
                chosen=sorted(eligible,key=lambda c:(c.get('rank') or 2**30,c['key']))[:128][::-1]
                request['assignments']=[{'clubKey':c['key'],'division':i//8+1 if i<32 else 5} for i,c in enumerate(chosen)]
            preview=call(port,'/api/world-creator/preview',request,owner)
            assert not preview['errors'], preview['errors']; assert len(preview['clubs'])==128
            for division in range(1,6): assert sum(c['division']==division for c in preview['clubs'])==(96 if division==5 else 8)
            confirmed=call(port,'/api/world-creator/confirm/'+preview['draftId'],{},owner); world=confirmed['id']
            assert call(port,'/api/world-creator/confirm/'+preview['draftId'],{},owner)['id']==world
            saved=call(port,'/api/world-creator/last-configuration',coach=owner)
            assert all(saved[key]==value for key,value in request.items())
            actual=call(port,f'/api/worlds/{world}/clubs'); assert len(actual)==128
            assert sum(c['cashBalance'] for c in actual)==138800000
            unique=set(); minimum=25; total=0
            for club in actual:
                detail=call(port,f"/api/worlds/{world}/clubs/{club['id']}")
                squad=detail['squad']; assert 11<=len(squad)<=24
                assert sum(p['position']=='G' for p in squad)>=1 and sum(p['position']!='G' for p in squad)>=10
                assert detail['stadium']['usableCapacity']>0
                for player in squad:
                    assert player['id'] not in unique; unique.add(player['id'])
                    assert 1<=player['strength']<=50 and player['salaryPerRound']>0 and player['contractRoundsRemaining']>0
                minimum=min(minimum,len(squad)); total+=len(squad)
            club=actual[0]['id']
            call(port,f'/api/worlds/{world}/join?'+urllib.parse.urlencode({'coachId':owner,'nickname':'014A smoke','deviceId':'014a','preferredClubId':club}),{},owner)
            finance=call(port,f'/api/worlds/{world}/clubs/{club}/finances'); assert not finance['loans']
            assert finance['annualSponsorshipBudget']==185000 and finance['salaryPeriod']=='OFFICIAL_MATCH'
            for _ in range(2): call(port,f'/api/worlds/{world}/clubs/{club}/loans?'+urllib.parse.urlencode({'coachId':owner,'amount':500000,'rounds':0,'idempotencyKey':'014a-live-loan'}),{},owner)
            financed=call(port,f'/api/worlds/{world}/clubs/{club}/finances')
            assert len(financed['loans'])==1 and financed['cashBalance']==finance['cashBalance']+500000
            assert financed['loans'][0]['remainingBalance']==500000 and financed['loans'][0]['interestPerMatch']==25000
            context={'Port':port,'World':world,'Club':club,'Coach':owner,'Gender':gender,'Draft':preview['draftId'],'FinancialHash':fingerprint(port,world,club)}
            contexts.append(context); (LOCAL/'smoke-context.json').write_text(json.dumps(contexts,indent=2),encoding='utf-8')
            generated=sum(p.get('identityOrigin')=='Generated' for c in preview['clubs'] for p in c['club']['players'])
            evidence.append({'World':world,'Gender':gender,'Port':port,'Countries':8,'Clubs':128,'Players':total,'MinimumSquad':minimum,
                'Assembly':request['mode'],'DistrictMode':request['districtMode'],'InitialCash':138800000,'RealClubCatalog':real,
                'GeneratedPlayersExplicitlyAuthorized':real,'GeneratedPlayers':generated,'RealCurrentSquadsComplete':False,
                'Snapshot':confirmed['catalogOrigin']['dataHash'],'LoanCommands':2,'LoansCreated':1,'SalaryField':'SalaryPerRound','Warnings':preview['warnings']})
            (AUDIT/'api-smoke.json').write_text(json.dumps({'Status':'PASS' if len(evidence)==4 else 'RUNNING','Worlds':evidence},ensure_ascii=False,indent=2),encoding='utf-8')
            print(f"Created {gender}: {'real clubs + authorized generated players' if real else 'fictional fixtures'}, {total} players, 128 valid clubs.",flush=True)

if __name__=='__main__': verify() if '--verify-restart' in sys.argv else create()
