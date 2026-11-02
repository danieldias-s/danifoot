"""Local HTTP setup/evidence for the isolated 015B review. No UI automation."""
from pathlib import Path
import json, urllib.request, urllib.error, urllib.parse, sys, uuid
ROOT=Path(__file__).resolve().parents[1]
assert str(ROOT).lower()==r'd:\elifoot_98_015b'
AREA=ROOT/'artifacts/015b'
BASE='http://127.0.0.1:5167/api'
def api(path, body=None, token=None):
    headers={'Content-Type':'application/json'}
    if token: headers['Authorization']='Bearer '+token
    data=None if body is None else json.dumps(body).encode()
    req=urllib.request.Request(BASE+path,data=data,headers=headers)
    try:
        with urllib.request.urlopen(req,timeout=90) as r:return json.load(r)
    except urllib.error.HTTPError as e:raise RuntimeError(f'{e.code}: {e.read().decode()}') from e
def save(name,data): (AREA/name).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
def prepare():
    if (AREA/'browser-context.json').exists():
        print('Existing 015B review context preserved.');return
    auths=[api('/auth/local-login',{'nickname':n,'deviceId':d}) for n,d in [('Daniel 015B','015b-daniel'),('Comprador 015B','015b-comprador'),('Rival 015B','015b-rival')]]
    req=json.loads((AREA/'inputs/Male.input.json').read_text(encoding='utf-8-sig'))['request']
    req.update(name='015B - Mercado e partidas',maxHumans=8,seed=199801)
    preview=api('/world-creator/preview',req,auths[0]['sessionToken'])
    save('browser-preview.json',preview)
    if preview.get('errors'):raise RuntimeError(preview['errors'])
    world=api('/world-creator/confirm/'+preview['draftId'],{},auths[0]['sessionToken'])
    clubs=api('/worlds/'+world['id']+'/clubs')
    selected=[]
    for auth,name in zip(auths,['Flamengo RJ','FC Porto','Arsenal FC']):
        club=next(c for c in clubs if c['name']==name)
        query=urllib.parse.urlencode(dict(coachId=auth['coachId'],nickname=auth['nickname'],deviceId=auth['deviceId'],preferredClubId=club['id']))
        joined=api('/worlds/'+world['id']+'/join?'+query,{})
        selected.append(dict(auth=auth,club=club,join=joined))
    save('browser-context.json',dict(world=world,participants=selected))
    print(json.dumps(dict(world={k:world[k] for k in ['id','name','inviteCode']},participants=[dict(nickname=p['auth']['nickname'],device=p['auth']['deviceId'],club=p['club']['name']) for p in selected]),ensure_ascii=False))
def context(): return json.loads((AREA/'browser-context.json').read_text(encoding='utf-8'))
def tactics(w,p):
    d=api('/worlds/'+w+'/clubs/'+p['club']['id'])
    starters=[]
    for position,count in [('G',1),('D',4),('M',4),('A',2)]:
        eligible=sorted([x for x in d['squad'] if x['position']==position and not x['injuryRoundsRemaining'] and not x['suspensionRoundsRemaining']],key=lambda x:(-x['strength'],x['id']))
        starters += [x['id'] for x in eligible[:count]]
    assert len(starters)==11
    bench=[x['id'] for x in d['squad'] if x['id'] not in starters and not x['injuryRoundsRemaining'] and not x['suspensionRoundsRemaining']][:9]
    snap=api('/worlds/'+w+'/snapshot')
    return dict(gameWorldId=w,coachId=p['auth']['coachId'],formation='4-4-2',starterPlayerIds=starters,benchPlayerIds=bench,idempotencyKey='015b-'+uuid.uuid4().hex,expectedRoundVersion=snap['roundVersion'])
def ready(w,p):
    snap=api('/worlds/'+w+'/snapshot')
    body=dict(gameWorldId=w,coachId=p['auth']['coachId'],idempotencyKey='015b-'+uuid.uuid4().hex,expectedRoundVersion=snap['roundVersion'])
    try:return api('/worlds/'+w+'/ready-unemployed',body)
    except RuntimeError as e:
        if 'partida' not in str(e).lower():raise
        return api('/worlds/'+w+'/tactics',tactics(w,p))
def finish(w,participants):
    stages=[]
    for _ in range(128):
        snap=api('/worlds/'+w+'/snapshot');stage=snap['roundState'];stages.append(stage)
        if stage in ['RESULTS','FINISHED']:return snap,stages
        if snap.get('pendingPenalty'):
            pending=snap['pendingPenalty']
            assert pending['coachId'] in [p['auth']['coachId'] for p in participants], 'QA may choose only its own coaches'
            taker=sorted(pending['eligiblePlayers'],key=lambda p:(-int(p['isStar']),-int(p['position']=='A'),-p['strength'],p['playerId']))[0]
            api('/worlds/'+w+'/penalty-taker',dict(gameWorldId=w,coachId=pending['coachId'],decisionId=pending['decisionId'],playerId=taker['playerId'],idempotencyKey='qa-penalty-'+pending['decisionId']))
        elif stage.startswith('RUNNING_'):api('/worlds/'+w+'/tick?ticks=125',{})
        elif stage in ['HALFTIME_PAUSED','GLOBAL_PAUSED','EXTRA_TIME_BREAK','EXTRA_TIME_HALFTIME']:
            for p in participants:
                api('/worlds/'+w+'/continue',dict(gameWorldId=w,coachId=p['auth']['coachId'],idempotencyKey='015b-'+uuid.uuid4().hex))
        else:raise RuntimeError('Unexpected stage '+stage)
    raise RuntimeError('Round did not finish')
def preliminaries():
    c=context();w=c['world']['id'];evidence=[]
    for target in [1,2]:
        snap=api('/worlds/'+w+'/snapshot')
        if snap['roundNumber']>target:continue
        assert snap['roundNumber']==target and snap['roundState']=='PREPARING'
        for p in c['participants']:ready(w,p)
        snap,stages=finish(w,c['participants']);market=api('/worlds/'+w+'/market')
        evidence.append(dict(round=target,stages=stages,results=snap,market=market))
        api('/worlds/'+w+'/next-round',{})
    save('browser-preliminaries.json',evidence)
    print('Official preliminary rounds completed; league preparation:',api('/worlds/'+w+'/snapshot')['roundNumber'])
def multiplayer():
    reports=[]
    for count in [2,4,6,8]:
        created=api('/worlds',dict(worldName=f'015B HTTP {count} humanos',maxHumans=count,totalClubs=128,seed=199801+count,tickIntervalMs=5000,pauseTimeoutSeconds=3600))
        w=created['gameWorldId'];clubs=api('/worlds/'+w+'/clubs');eligible=[x for x in clubs if x['divisionTier']!='Distrital'];participants=[]
        for i in range(count):
            auth=api('/auth/local-login',dict(nickname=f'QA015B {count}-{i+1}',deviceId=f'015b-http-{count}-{i+1}'))
            unemployed=count>=4 and i==count-1
            club=None if unemployed else eligible[i]
            query=dict(coachId=auth['coachId'],nickname=auth['nickname'],deviceId=auth['deviceId'],joinUnemployed=str(unemployed).lower())
            if club:query['preferredClubId']=club['id']
            api('/worlds/'+w+'/join?'+urllib.parse.urlencode(query),{})
            participants.append(dict(auth=auth,club=club))
        history=[]
        for round_no in [1,2,3]:
            for p in participants[:-1]:ready(w,p)
            waiting=api('/worlds/'+w+'/snapshot')
            assert waiting['roundState']=='PREPARING' and waiting['readyCounter']['readyCount']==count-1
            ready(w,participants[-1]);started=api('/worlds/'+w+'/snapshot')
            assert started['roundState'].startswith('RUNNING_') and started['readyCounter']['readyCount']==count
            if round_no==3:
                api('/worlds/'+w+'/pause',dict(gameWorldId=w,coachId=participants[0]['auth']['coachId'],idempotencyKey='qa-pause-'+uuid.uuid4().hex))
                paused=api('/worlds/'+w+'/snapshot');assert paused['roundState']=='GLOBAL_PAUSED'
                if participants[-1]['club'] is None:
                    match=next(x for x in paused['matches'] if participants[0]['club']['id'] in [x['homeClubId'],x['awayClubId']])
                    try:
                        api('/worlds/'+w+'/in-match-adjust',dict(gameWorldId=w,coachId=participants[-1]['auth']['coachId'],matchId=match['matchId'],newFormation='4-4-2',substitutions=[],idempotencyKey='bad-unemployed-'+uuid.uuid4().hex))
                        raise AssertionError('Unemployed adjustment accepted')
                    except RuntimeError as e:assert 'clube' in str(e).lower() or 'treinador' in str(e).lower()
                save(f'multiplayer-{count}-live.json',paused)
            finished,stages=finish(w,participants);assert finished['roundState']=='RESULTS'
            history.append(dict(round=round_no,readyBefore=count-1,readyAfter=count,stages=stages,finalState=finished['roundState']))
            if round_no<3:api('/worlds/'+w+'/next-round',{})
        reports.append(dict(humans=count,worldId=w,inviteCode=clubs[0].get('inviteCode'),rounds=history,unemployedProtected=count>=4,status='PASS'))
        save('multiplayer-http.json',reports)
        print(f'{count} humanos: waiting/start/pause/results verified.',flush=True)

def solo():
    """Prepare a separate one-human world without altering prior saves or coaches."""
    path=AREA/'solo-context.json'
    if path.exists():
        c=json.loads(path.read_text(encoding='utf-8'))
    else:
        auth=api('/auth/local-login',dict(nickname='Daniel 015B',deviceId='015b-daniel'))
        req=json.loads((AREA/'inputs/Male.input.json').read_text(encoding='utf-8-sig'))['request']
        req.update(name='015B - Teste individual Daniel',maxHumans=1,seed=199801)
        preview=api('/world-creator/preview',req,auth['sessionToken'])
        if preview.get('errors'):raise RuntimeError(preview['errors'])
        world=api('/world-creator/confirm/'+preview['draftId'],{},auth['sessionToken'])
        club=next(x for x in api('/worlds/'+world['id']+'/clubs') if x['name']=='Flamengo RJ')
        query=urllib.parse.urlencode(dict(coachId=auth['coachId'],nickname=auth['nickname'],deviceId=auth['deviceId'],preferredClubId=club['id']))
        api('/worlds/'+world['id']+'/join?'+query,{})
        c=dict(world=world,participants=[dict(auth=auth,club=club)])
        save('solo-context.json',c)
    w=c['world']['id']
    for target in [1,2]:
        snap=api('/worlds/'+w+'/snapshot')
        if snap['roundNumber']>target:continue
        assert snap['roundNumber']==target
        if snap['roundState']=='PREPARING':ready(w,c['participants'][0])
        finished,_=finish(w,c['participants'])
        assert finished['roundState']=='RESULTS'
        api('/worlds/'+w+'/next-round',{})
    snap=api('/worlds/'+w+'/snapshot')
    assert snap['readyCounter']['totalHumans']==1
    public=dict(worldId=w,worldName=c['world']['name'],inviteCode=c['world']['inviteCode'],
        nickname=c['participants'][0]['auth']['nickname'],deviceId=c['participants'][0]['auth']['deviceId'],
        clubName=c['participants'][0]['club']['name'],roundNumber=snap['roundNumber'],roundState=snap['roundState'],
        readyCounter=snap['readyCounter']['counterLabel'])
    save('solo-playtest.json',public)
    print(json.dumps(public,ensure_ascii=False))

if __name__=='__main__':
    if len(sys.argv)>1 and sys.argv[1]=='solo':solo()
    elif len(sys.argv)>1 and sys.argv[1]=='multiplayer':multiplayer()
    elif len(sys.argv)>1 and sys.argv[1]=='preliminaries':preliminaries()
    else:prepare()
