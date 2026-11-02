"""Own isolated QA room for live-screen evidence; never alters the owner's rooms."""
from pathlib import Path
import importlib.util,json,urllib.parse,sys
ROOT=Path(__file__).resolve().parents[1]
assert str(ROOT).lower()==r'd:\elifoot_98_015b'
spec=importlib.util.spec_from_file_location('review015b',ROOT/'scripts/review-classic-desktop-015b.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
path=m.AREA/'live-review-context.json'
if path.exists():
    context=json.loads(path.read_text(encoding='utf-8'))
else:
    auths=[m.api('/auth/local-login',{'nickname':name,'deviceId':device}) for name,device in [('QA Jogos 015B A','015b-live-review-a'),('QA Jogos 015B B','015b-live-review-b')]]
    request=json.loads((m.AREA/'inputs/Male.input.json').read_text(encoding='utf-8-sig'))['request']
    request.update(name='015B QA - Jogos ao vivo',maxHumans=2,seed=199803,tickIntervalMs=60000,pauseTimeoutSeconds=120)
    preview=m.api('/world-creator/preview',request,auths[0]['sessionToken'])
    assert not preview.get('errors'),preview.get('errors')
    world=m.api('/world-creator/confirm/'+preview['draftId'],{},auths[0]['sessionToken'])
    clubs=m.api('/worlds/'+world['id']+'/clubs');people=[]
    for auth,name in zip(auths,['Flamengo RJ','FC Porto']):
        club=next(c for c in clubs if c['name']==name)
        query=urllib.parse.urlencode(dict(coachId=auth['coachId'],nickname=auth['nickname'],deviceId=auth['deviceId'],preferredClubId=club['id']))
        m.api('/worlds/'+world['id']+'/join?'+query,{})
        people.append(dict(auth=auth,club=club))
    context=dict(world=world,participants=people);m.save('live-review-context.json',context)
w=context['world']['id'];people=context['participants']
assert context['world']['name']=='015B QA - Jogos ao vivo'
assert w not in ['af4dcfb3-5207-48ae-92db-5cd3623e7270','a580544f-f88e-41fd-89b4-d2e6c467ce42']
results='--results' in sys.argv
if (m.AREA/'live-review-scene.json').exists() and not results:
    print((m.AREA/'live-review-setup.json').read_text(encoding='utf-8'));sys.exit(0)
for target in [1,2]:
    snap=m.api('/worlds/'+w+'/snapshot')
    if snap['roundNumber']>target:continue
    assert snap['roundNumber']==target
    if snap['roundState']=='PREPARING':
        for p in people:m.ready(w,p)
    final,_=m.finish(w,people);assert final['roundState']=='RESULTS'
    m.api('/worlds/'+w+'/next-round',{})
snap=m.api('/worlds/'+w+'/snapshot');assert snap['roundNumber']==3
if snap['roundState']=='PREPARING':
    for p in people:m.ready(w,p)
    m.api('/worlds/'+w+'/tick?ticks=28',{})
if results:
    snap,_=m.finish(w,people)
    assert snap['roundState']=='RESULTS'
else:
    snap=m.api('/worlds/'+w+'/snapshot')
p=people[0];owner=p['auth']['coachId'];club=p['club']['id'];base='/worlds/'+w
scene=dict(coach=owner,club=club,nickname=p['auth']['nickname'],snapshot=snap,detail=m.api(base+'/clubs/'+club),clubs=m.api(base+'/clubs'),appearance=m.api('/world-creator/appearance/'+w+'/'+club,token=p['auth']['sessionToken']))
for name,route in dict(calendar='/calendar',cup='/cup',scorers='/top-scorers',market='/market',ranking='/ranking',finances='/clubs/'+club+'/finances',invitations='/invitations/'+owner).items():scene[name]=m.api(base+route)
scene['standings']={tier:m.api(base+'/standings/'+tier) for tier in ['PrimeiraDivisao','SegundaDivisao','TerceiraDivisao','QuartaDivisao']}
m.save('live-review-results-scene.json' if results else 'live-review-scene.json',scene)
report=dict(worldId=w,worldName=context['world']['name'],inviteCode=context['world']['inviteCode'],state=snap['roundState'],round=snap['roundNumber'],minute=snap['currentMinute'],humans=snap['readyCounter']['totalHumans'],matches=len(snap['matches']),events=sum(len(x['events']) for x in snap['matches']),clubs=[p['club']['name'] for p in people],ownerSavesModified=False)
m.save('live-review-results-setup.json' if results else 'live-review-setup.json',report);print(json.dumps(report,ensure_ascii=False))
