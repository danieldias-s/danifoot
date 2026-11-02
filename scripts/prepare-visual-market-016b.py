"""Create review data through the real local API, preserving all older saves."""
from pathlib import Path
import json,urllib.request,urllib.parse,urllib.error,uuid
ROOT=Path(__file__).resolve().parents[1]
assert str(ROOT).lower()==r'd:\elifoot_98_016b'
AREA=ROOT/'artifacts/016b';BASE='http://127.0.0.1:5173/api'
def api(route,body=None,token=None):
    headers={'Content-Type':'application/json'}
    if token:headers['Authorization']='Bearer '+token
    req=urllib.request.Request(BASE+route,headers=headers,data=None if body is None else json.dumps(body).encode())
    try:
        with urllib.request.urlopen(req,timeout=120) as response:return json.load(response)
    except urllib.error.HTTPError as err:raise RuntimeError(str(err.code)+': '+err.read().decode()) from err
def save(name,data):(AREA/name).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
contextFile=AREA/'market-context.json'
if contextFile.exists():context=json.loads(contextFile.read_text(encoding='utf-8'))
else:
    original=json.loads((AREA/'qa-male-context.json').read_text(encoding='utf-8'))
    buyer=api('/auth/local-login',dict(nickname='QA 016B Masculino',deviceId='016b-qa-male'))
    seller=api('/auth/local-login',dict(nickname='QA 016B Vendedor',deviceId='016b-qa-seller'))
    request=dict(original['request']);request.update(name='016B - Revisão visual',maxHumans=2,seed=199818)
    preview=api('/world-creator/preview',request,buyer['sessionToken']);assert not preview['errors'],preview['errors']
    world=api('/world-creator/confirm/'+preview['draftId'],{},buyer['sessionToken']);w=world['id']
    clubs=api('/worlds/'+w+'/clubs')
    for auth,name in [(buyer,'Flamengo RJ'),(seller,'FC Porto')]:
        club=next(c for c in clubs if c['name']==name)
        query=urllib.parse.urlencode(dict(coachId=auth['coachId'],nickname=auth['nickname'],deviceId=auth['deviceId'],preferredClubId=club['id']))
        api('/worlds/'+w+'/join?'+query,{})
    context=dict(world=world,buyer=buyer,seller=seller,sellerClub=next(c for c in clubs if c['name']=='FC Porto'))
    save(contextFile.name,context)
w=context['world']['id'];base='/worlds/'+w
market=api(base+'/market')
if not market['auctions'] and not market['listings']:
    squad=api(base+'/clubs/'+context['sellerClub']['id'])['squad']
    available=[p for p in squad if p['position']!='G']
    for i,p in enumerate(available[-4:]):
        version=api(base+'/snapshot')['roundVersion'];key='016b-market-'+str(i)+'-'+uuid.uuid4().hex
        if i<2:
            result=api(base+'/market/auction',dict(coachId=context['seller']['coachId'],playerId=p['id'],minimumBid=500000,durationRounds=2,idempotencyKey=key,expectedRoundVersion=version))
        else:
            query=urllib.parse.urlencode(dict(coachId=context['seller']['coachId'],playerId=p['id'],askingPrice=650000+150000*(i-2),idempotencyKey=key,expectedRoundVersion=version))
            result=api(base+'/market/list?'+query,{})
        assert result['success'],result
market=api(base+'/market');save('market-api-scene.json',market)
print(json.dumps(dict(worldId=w,code=context['world']['inviteCode'],auctions=len(market['auctions']),listings=len(market['listings']),realClubs=True,players='FICTIONAL_GENERATED',existingSavesPreserved=True),ensure_ascii=False))
