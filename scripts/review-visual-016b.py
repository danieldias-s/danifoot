"""Real HTTP QA, isolated 016B only. No synthetic provider responses or changes to older saves."""
from pathlib import Path
import json,urllib.request,urllib.error,urllib.parse,uuid,hashlib,sys
ROOT=Path(__file__).resolve().parents[1]
assert str(ROOT).lower()==r'd:\elifoot_98_016b'
AREA=ROOT/'artifacts/016b';BASE='http://127.0.0.1:5173/api'
ADMIN=(AREA/'admin-token.txt').read_text(encoding='utf-8').strip()
def save(name,data):(AREA/name).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
def api(path,body=None,token=None,admin=False):
    headers={'Content-Type':'application/json'}
    if token:headers['Authorization']='Bearer '+token
    if admin:headers['X-Catalog-Admin-Token']=ADMIN
    request=urllib.request.Request(BASE+path,headers=headers,data=None if body is None else json.dumps(body).encode())
    try:
        with urllib.request.urlopen(request,timeout=120) as response:return json.load(response)
    except urllib.error.HTTPError as error:raise RuntimeError(str(error.code)+': '+error.read().decode()) from error

def scene(context,suffix=''):
    w=context['world']['id'];club=context['club']['id'];coach=context['auth']['coachId'];base='/worlds/'+w
    data=dict(coach=coach,club=club,nickname=context['auth']['nickname'],snapshot=api(base+'/snapshot'),detail=api(base+'/clubs/'+club),clubs=api(base+'/clubs'),appearance=api('/world-creator/appearance/'+w+'/'+club,token=context['auth']['sessionToken']))
    for key,route in dict(calendar='/calendar',cup='/cup',scorers='/top-scorers',market='/market',ranking='/ranking',finances='/clubs/'+club+'/finances',invitations='/invitations/'+coach).items():data[key]=api(base+route)
    data['standings']={tier:api(base+'/standings/'+tier) for tier in ['PrimeiraDivisao','SegundaDivisao','TerceiraDivisao','QuartaDivisao']}
    save('world-'+context['gender'].lower()+suffix+'-scene.json',data)
    return data

def ready(context):
    w=context['world']['id'];coach=context['auth']['coachId'];club=context['club']['id'];snapshot=api('/worlds/'+w+'/snapshot')
    body=dict(gameWorldId=w,coachId=coach,idempotencyKey='016b-'+uuid.uuid4().hex,expectedRoundVersion=snapshot['roundVersion'])
    own=any(club in [m['homeClubId'],m['awayClubId']] for m in snapshot['matches'])
    if not own:return api('/worlds/'+w+'/ready-unemployed',body)
    detail=api('/worlds/'+w+'/clubs/'+club)
    body.update(formation=detail['preferredFormation'],starterPlayerIds=detail['preferredStarterIds'],benchPlayerIds=detail['preferredBenchIds'])
    return api('/worlds/'+w+'/tactics',body)

def finish(context):
    w=context['world']['id'];coach=context['auth']['coachId']
    for _ in range(24):
        snapshot=api('/worlds/'+w+'/snapshot')
        if snapshot['roundState']=='RESULTS':return snapshot
        if snapshot.get('activePauseWindow'):api('/worlds/'+w+'/continue',dict(gameWorldId=w,coachId=coach,idempotencyKey='016b-'+uuid.uuid4().hex))
        else:api('/worlds/'+w+'/tick?ticks=125',{})
    raise AssertionError('Official round did not finish')

if '--record-admin' in sys.argv:
    ids=json.loads((AREA/'pilot-squads-apply.json').read_text(encoding='utf-8-sig'))['id']
    save('admin-scene.json',dict(coverage=api('/catalog-016a/coverage',admin=True),run=api('/catalog-016a/reports/'+ids,admin=True)))
    print('Recorded real administrative GET responses. No key is included.');sys.exit(0)

contexts=[];reports=[]
for gender,targetId,label in [('Male','Q17479','Masculino'),('Female','Q522899','Feminino')]:
    path=AREA/('qa-'+gender.lower()+'-context.json')
    if path.exists():context=json.loads(path.read_text(encoding='utf-8'))
    else:
        auth=api('/auth/local-login',dict(nickname='QA 016B '+label,deviceId='016b-qa-'+gender.lower()))
        request=dict(name='016B QA - '+label,gender=gender,season='2026',countries=['BR','AR','GB','ES','DE','IT','FR','PT'],mode='Custom',districtMode='Ranking',seed=199816 if gender=='Male' else 199817,assignments=[],maxHumans=1,fillRemainingDivisions=True,allowFictionalTeams=False,allowFictionalPlayers=True,useSavedData=True,dataMode='REAL_CLUBS_SIMULATED',allowProviderReportedPlayers=False)
        selection=api('/world-creator/catalog',request,token=auth['sessionToken'])
        target=next(c for c in selection['clubs'] if c['verification']['wikidataId']==targetId)
        request['assignments']=[dict(clubKey=target['key'],division=1)]
        preview=api('/world-creator/preview',request,token=auth['sessionToken']);save('world-'+gender.lower()+'-preview.json',preview);assert not preview['errors'],preview['errors'];assert len(preview['clubs'])==128
        for entry in preview['clubs']:
            players=entry['club']['players'];assert 11<=len(players)<=24;assert any(p['position']=='G' for p in players);assert sum(p['position']!='G' for p in players)>=10
            assert all(1<=p['strength']<=50 for p in players);assert all(p['playerOrigin']=='FICTIONAL_GENERATED' for p in players)
        world=api('/world-creator/confirm/'+preview['draftId'],{},token=auth['sessionToken'])
        clubs=api('/worlds/'+world['id']+'/clubs');matching=[c for c in clubs if c['name']==target['name']];assert len(matching)==1
        club=matching[0];query=urllib.parse.urlencode(dict(coachId=auth['coachId'],nickname=auth['nickname'],deviceId=auth['deviceId'],preferredClubId=club['id']))
        api('/worlds/'+world['id']+'/join?'+query,{})
        context=dict(gender=gender,request=request,world=world,club=club,auth=auth);save(path.name,context)
    w=context['world']['id'];club=context['club']['id'];detail=api('/worlds/'+w+'/clubs/'+club)
    expected=next(c['club']['players'] for c in context['world']['catalogOrigin']['clubs'] if c['club']['name']==context['club']['name'])
    assert sorted((p['playerName'],p['position'],p['strength']) for p in expected)==sorted((p['name'],p['position'],p['strength']) for p in detail['squad'])
    assert all(p['salaryPerRound']>0 and p['contractRoundsRemaining']>0 for p in detail['squad'])
    save('world-'+gender.lower()+'-frozen-squad.json',detail['squad'])
    if '--start-matches' in sys.argv:
        for target in [1,2]:
            snapshot=api('/worlds/'+w+'/snapshot')
            if snapshot['roundNumber']>target:continue
            if snapshot['roundState']=='PREPARING':ready(context)
            assert finish(context)['roundState']=='RESULTS';api('/worlds/'+w+'/next-round',{})
        snapshot=api('/worlds/'+w+'/snapshot');assert snapshot['roundNumber']==3
        if snapshot['roundState']=='PREPARING':ready(context)
        started=api('/worlds/'+w+'/snapshot');assert started['roundState'] in ['RUNNING_FIRST_HALF','HALFTIME_PAUSED','RUNNING_SECOND_HALF','RESULTS']
        save('world-'+gender.lower()+'-started.json',started)
        final=finish(context);assert final['roundState']=='RESULTS';assert all(m['isFinished'] for m in final['matches'])
        scene(context,'-results')
    else:scene(context)
    reports.append(dict(gender=gender,worldId=w,inviteCode=context['world']['inviteCode'],clubName=context['club']['name'],clubs=128,selectedSquad=len(detail['squad']),realPlayers=0,generatedPlayers=sum(len(c['club']['players']) for c in context['world']['catalogOrigin']['clubs']),fictionalComplementExplicitlyAuthorized=True,positionsAndStrengthMatchFrozenPreview=True,simulatedContracts=True,officialRoundCompleted='--start-matches' in sys.argv))
save('worlds-http-report.json',reports);print(json.dumps(reports,ensure_ascii=False))
