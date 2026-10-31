"""Local HTTP acceptance of the actual preserved 013B catalog. No network providers."""
from pathlib import Path
import urllib.request, urllib.error, urllib.parse, json, uuid, hashlib, sys
ROOT=Path(__file__).resolve().parents[1]
AUDIT=ROOT/'docs/audit-013c'; LOCAL=ROOT/'artifacts/013c'; PORT=5143
COUNTRIES=['BR','AR','GB','ES','DE','IT','FR','PT']
def save(name,value):
    (AUDIT/name).write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf-8')
def call(path,body=None,coach=None,method=None):
    headers={'Content-Type':'application/json'}
    if coach: headers['Authorization']='Bearer localdev-'+coach.replace('-','')
    req=urllib.request.Request(f'http://127.0.0.1:{PORT}'+path,data=None if body is None else json.dumps(body).encode(),headers=headers,method=method or ('POST' if body is not None else 'GET'))
    try:
        with urllib.request.urlopen(req,timeout=120) as response:
            raw=response.read(); return json.loads(raw) if raw else None
    except urllib.error.HTTPError as error:
        raise RuntimeError(f'{path}: HTTP {error.code} '+error.read().decode()[:1200]) from error

def fingerprint(world,club):
    detail=call(f'/api/worlds/{world}/clubs/{club}')
    finance=call(f'/api/worlds/{world}/clubs/{club}/finances')
    data={'detail':detail,'finance':finance,'snapshot':call(f'/api/worlds/{world}/snapshot')}
    return hashlib.sha256(json.dumps(data,sort_keys=True,ensure_ascii=False,separators=(',',':')).encode()).hexdigest()
def create():
    contexts=[]; results=[]; modes=[]
    for gender in ['Male','Female']:
        login=call('/api/auth/local-login',{'nickname':'013C actual '+gender,'deviceId':'013c-actual-'+uuid.uuid4().hex})
        owner=login['coachId']
        request={'name':'013C clubes reais '+gender,'gender':gender,'season':'2026','countries':COUNTRIES,
          'mode':'Automatic','districtMode':'Random','seed':130013,'assignments':[],
          'fillRemainingDivisions':True,'allowFictionalTeams':False,'allowFictionalPlayers':False,'useSavedData':True,'maxHumans':8,'dataMode':'REAL_COMPLETE'}
        strict=call('/api/world-creator/preview',request,owner)
        assert strict['errors'] and strict['dataSummary']['completeRealSquads']==0
        assert strict['dataSummary']['generatedPlayers']==0
        try: call('/api/world-creator/confirm/'+strict['draftId'],{},owner)
        except RuntimeError as error: assert 'HTTP 400' in str(error)
        else: raise AssertionError('Insufficient strict real world was created')
        request['dataMode']='REAL_CLUBS_SIMULATED'
        refused=call('/api/world-creator/preview',request,owner)
        assert refused['errors'] and refused['dataSummary']['generatedPlayers']==0
        request['allowFictionalPlayers']=True
        catalog=call('/api/world-creator/catalog',request,owner)
        assert len(catalog['clubs'])==({'Male':158,'Female':232}[gender])
        assert all(c['verification']['status']=='REAL_CLUB_VERIFIED' and c['countryCode'] in COUNTRIES and c['gender']==gender for c in catalog['clubs'])
        if gender=='Female':
            ordered=sorted(catalog['clubs'],key=lambda c:(not bool(c['players']),c['rank']))[:128]
            # Full manual assignment, including 96 districts; preserve every requested placement.
            request.update(mode='Custom',districtMode='Manual',fillRemainingDivisions=False,
              assignments=[{'clubKey':c['key'],'division':i//8+1 if i<32 else 5} for i,c in enumerate(ordered)])
        preview=call('/api/world-creator/preview',request,owner)
        assert not preview['errors'], preview['errors']
        assert len(preview['clubs'])==128 and len({c['club']['key'] for c in preview['clubs']})==128
        for d in range(1,6): assert sum(c['division']==d for c in preview['clubs'])==(96 if d==5 else 8)
        if gender=='Female':
            assert {c['club']['key']:c['division'] for c in preview['clubs']}=={a['clubKey']:a['division'] for a in request['assignments']}
        identities=[p['canonicalPlayerId'] for c in preview['clubs'] for p in c['club']['players']]
        assert len(identities)==len(set(identities))==2304
        assert all(p['playerOrigin'] in ['REAL_VERIFIED_CURRENT','FICTIONAL_GENERATED'] for c in preview['clubs'] for p in c['club']['players'])
        repeat=call('/api/world-creator/preview',request,owner)
        assert identities==[p['canonicalPlayerId'] for c in repeat['clubs'] for p in c['club']['players']]
        assert [[p['strength'] for p in c['club']['players']] for c in preview['clubs']]==[[p['strength'] for p in c['club']['players']] for c in repeat['clubs']]
        confirmed=call('/api/world-creator/confirm/'+preview['draftId'],{},owner); world=confirmed['id']
        assert confirmed['catalogOrigin']['generatedPlayerSeed']==130013
        assert confirmed['catalogOrigin']['dataMode']=='REAL_CLUBS_SIMULATED'
        clubs=call(f'/api/worlds/{world}/clubs'); unique=set(); origins={}; varied=0
        for club in clubs:
            detail=call(f"/api/worlds/{world}/clubs/{club['id']}"); squad=detail['squad']
            assert 11<=len(squad)<=24
            assert sum(p['position']=='G' for p in squad)>=1 and sum(p['position']!='G' for p in squad)>=10
            varied+=len({p['strength'] for p in squad})>1
            for player in squad:
                assert player['id'] not in unique; unique.add(player['id'])
                assert 1<=player['strength']<=50 and player['salaryPerRound']>0 and player['fairValue']>0 and player['contractRoundsRemaining']>0
                assert player['behaviorDisplay'] and player['catalogIdentity']['canonicalIdentity']
                origin=player['catalogIdentity']['origin']; origins[origin]=origins.get(origin,0)+1
        assert varied==128 and len(unique)==2304
        club=next(c['id'] for c in clubs if c['divisionTier']=='PrimeiraDivisao')
        joined=call(f'/api/worlds/{world}/join?'+urllib.parse.urlencode({'coachId':owner,'nickname':login['nickname'],'deviceId':login['deviceId'],'preferredClubId':club}),{},owner)
        assert joined['clubId']==club
        loan=f'/api/worlds/{world}/clubs/{club}/loans?'+urllib.parse.urlencode({'coachId':owner,'amount':500000,'rounds':0,'idempotencyKey':'013c-actual-loan'})
        call(loan,{},owner); call(loan,{},owner)
        assert len(call(f'/api/worlds/{world}/clubs/{club}/finances')['loans'])==1
        rounds=[]
        for round_number in range(1,4):
            snap=call(f'/api/worlds/{world}/snapshot'); detail=call(f'/api/worlds/{world}/clubs/{club}')
            call(f'/api/worlds/{world}/tactics',{'gameWorldId':world,'coachId':owner,'formation':detail['preferredFormation'],
                'starterPlayerIds':detail['preferredStarterIds'],'benchPlayerIds':detail['preferredBenchIds'],'idempotencyKey':f'013c-actual-ready-{round_number}','expectedRoundVersion':snap['roundVersion']},owner)
            for step in range(16):
                snap=call(f'/api/worlds/{world}/snapshot')
                if snap['roundState']=='RESULTS': break
                if snap['activePauseWindow']:
                    call(f'/api/worlds/{world}/continue',{'gameWorldId':world,'coachId':owner,'idempotencyKey':f'013c-actual-continue-{round_number}-{step}'},owner)
                else: call(f'/api/worlds/{world}/tick?ticks=125',{},owner)
            snap=call(f'/api/worlds/{world}/snapshot'); assert snap['roundState']=='RESULTS',snap
            matches=[m for m in snap['matches'] if m['homeClubId']==club or m['awayClubId']==club]
            rounds.append({'Round':round_number,'Competition':snap['competitionLabel'],'Matches':matches})
            call(f'/api/worlds/{world}/next-round',{},owner)
        assert any(m['isFinished'] and m['minute']>=90 for r in rounds for m in r['Matches'])
        finance=call(f'/api/worlds/{world}/clubs/{club}/finances')
        assert len(finance['loans'])==1 and finance['loans'][0]['interestCharged']>0
        context={'World':world,'Club':club,'Coach':owner,'Gender':gender,'Draft':preview['draftId'],'Fingerprint':fingerprint(world,club),'LoanPath':loan}
        contexts.append(context); (LOCAL/'smoke-context.json').write_text(json.dumps(contexts,indent=2),encoding='utf-8')
        result={'World':world,'Gender':gender,'Clubs':128,'Players':2304,'Origins':origins,'Assembly':request['mode'],'Districts':request['districtMode'],
          'DataSummary':preview['dataSummary'],'RankingMethods':sorted({c['club']['rankingInfo']['method'] for c in preview['clubs']}),
          'RoundsPlayed':rounds,'NextRound':call(f'/api/worlds/{world}/snapshot')['roundNumber'],'Loan':finance['loans'][0],
          'SnapshotHash':confirmed['catalogOrigin']['dataHash'],'DeterministicGeneration':True,'ManualPlacementsPreserved':gender=='Female'}
        results.append(result); save('actual-worlds.json',{'Status':'RUNNING','Worlds':results})
        save('actual-modes.json',{'Status':'RUNNING','Cases':modes})
        # One country: no silent fictitious clubs; free-world completion needs both consents.
        one={**request,'countries':['BR'],'assignments':[],'mode':'Automatic','districtMode':'Ranking','fillRemainingDivisions':True}
        blocked=call('/api/world-creator/preview',one,owner); assert blocked['errors']
        free={**one,'dataMode':'FREE_WORLD','allowFictionalTeams':True}
        completed=call('/api/world-creator/preview',free,owner); assert not completed['errors'],completed['errors']
        assert len(completed['clubs'])==128 and completed['dataSummary']['selectedFictionalClubs']>0
        assert all(c['club']['countryCode']=='BR' for c in completed['clubs'])
        free_confirm=call('/api/world-creator/confirm/'+completed['draftId'],{},owner)
        assert len(call(f"/api/worlds/{free_confirm['id']}/clubs"))==128
        modes.append({'Gender':gender,'StrictRealBlocked':True,'StrictGenerated':0,'SimulationRefusalBlocked':True,
          'OneCountryRealOnlyBlocked':True,'OneCountryFreeWorld':free_confirm['id'],'OneCountryFreeSummary':completed['dataSummary']})
        save('actual-modes.json',{'Status':'PASS' if len(modes)==2 else 'RUNNING','Cases':modes})
        print(f'PASS actual {gender}: 128 verified clubs, 2304 players, 3 rounds played; authorized one-country free world.',flush=True)
    save('actual-worlds.json',{'Status':'PASS','Worlds':results,'RealCurrentSquadsComplete':0,'ExternalProvidersDuringMatches':False})
def verify():
    contexts=json.loads((LOCAL/'smoke-context.json').read_text(encoding='utf-8'))
    results=[]
    for c in contexts:
        assert fingerprint(c['World'],c['Club'])==c['Fingerprint'],'World changed after process/PostgreSQL restart'
        assert call('/api/world-creator/confirm/'+c['Draft'],{},c['Coach'])['id']==c['World']
        call(c['LoanPath'],{},c['Coach'])
        assert fingerprint(c['World'],c['Club'])==c['Fingerprint'],'Loan replay changed finance'
        results.append({'World':c['World'],'Gender':c['Gender'],'FingerprintUnchanged':True,'DraftIdempotent':True,'LoanIdempotent':True})
    save('actual-restart-http.json',{'Status':'PASS','Worlds':results})
    print('PASS: actual male/female worlds recovered unchanged; confirmation and loan replay idempotent.')
if __name__=='__main__': verify() if '--verify-restart' in sys.argv else create()
