"""Actual local HTTP + PostgreSQL acceptance. Only own 014C API on 5155."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import urllib.request,urllib.error,urllib.parse,json,uuid,hashlib,copy,sys
ROOT=Path(__file__).resolve().parents[1];LOCAL=ROOT/'artifacts/014c';AUDIT=ROOT/'docs/audit-014c'
def write(p,x):p.write_text(json.dumps(x,ensure_ascii=False,indent=2),encoding='utf-8')
def call(path,body=None,owner=None):
 headers={'Content-Type':'application/json'}
 if owner:headers['Authorization']='Bearer localdev-'+owner.replace('-','')
 req=urllib.request.Request('http://127.0.0.1:5155'+path,data=None if body is None else json.dumps(body).encode(),headers=headers)
 try:
  with urllib.request.urlopen(req,timeout=120) as r:return json.loads(r.read())
 except urllib.error.HTTPError as e:raise RuntimeError('HTTP '+str(e.code)+' '+e.read().decode()[:600])
def denied(path,body,owner,status):
 try:call(path,body,owner)
 except RuntimeError as e:assert f'HTTP {status}' in str(e),str(e)
 else:raise AssertionError('Expected rejection '+path)
def login(name):return call('/api/auth/local-login',{'nickname':name,'deviceId':'014c-'+uuid.uuid4().hex})['coachId']
def exported(w):
 p=call(f'/api/worlds/{w}/editor/export-json');p.pop('exportedAtUtc',None);return p
def fingerprint(w):return hashlib.sha256(json.dumps(exported(w),sort_keys=True).encode()).hexdigest()
def appearance(w,c):return f'/api/world-creator/appearance/{w}/{c}'
def join(w,c,o):return call(f'/api/worlds/{w}/join?'+urllib.parse.urlencode({'coachId':o,'nickname':'Treinador 014C','deviceId':'014c-'+o,'preferredClubId':c}),{},o)
def scene(w,c,o,name):
 write(LOCAL/('scene-'+name+'.json'),{'coach':o,'club':c,'clubs':call(f'/api/worlds/{w}/clubs'),'detail':call(f'/api/worlds/{w}/clubs/{c}'),'snapshot':call(f'/api/worlds/{w}/snapshot'),'appearance':call(appearance(w,c),owner=o)})
def verify():
 contexts=json.loads((LOCAL/'appearance-context.json').read_text(encoding='utf-8'))
 for x in contexts:
  assert call(appearance(x['world'],x['club']),owner=x['owner'])['preferences']==x['preferences']
  assert fingerprint(x['world'])==x['fingerprint']
 write(AUDIT/'appearance-restart.json',{'Status':'PASS','ApiAndDatabaseRestart':True,'PreferenceRows':len(contexts),'UnchangedWorldFingerprints':True})
 print('PASS: real process/database restart restored private preferences and unchanged worlds',flush=True)
def run():
 contexts=[];results=[]
 for gender in ['Male','Female']:
  owner=login('014C '+gender);other=login('014C outro '+gender)
  req={'name':'014C '+gender,'gender':gender,'season':'2026','countries':['BR','AR','GB','ES','DE','IT','FR','PT'],'mode':'Custom','districtMode':'Manual','seed':140014,'assignments':[],'fillRemainingDivisions':False,'allowFictionalTeams':False,'allowFictionalPlayers':True,'useSavedData':True,'maxHumans':8,'dataMode':'REAL_CLUBS_SIMULATED'}
  catalog=call('/api/world-creator/catalog',req,owner)['clubs']
  if gender=='Male':
   red=next(c for c in catalog if c['externalId']=='qid-Q17479');blue=next(c for c in catalog if c['externalId']=='qid-Q50602')
   assert red['colorPalette']['primaryColor']=='#C3281E' and blue['colorPalette']['secondaryColor']=='#FFFFFF'
   chosen=[red,blue]+[c for c in catalog if c['key'] not in [red['key'],blue['key']]]
  else:chosen=sorted(catalog,key=lambda c:not bool(c.get('colorPalette')))
  chosen=chosen[:128];req['assignments']=[{'clubKey':c['key'],'division':i//8+1 if i<32 else 5} for i,c in enumerate(chosen)]
  preview=call('/api/world-creator/preview',req,owner);assert not preview['errors'],preview['errors']
  w=call('/api/world-creator/confirm/'+preview['draftId'],{},owner)['id'];clubs=call(f'/api/worlds/{w}/clubs')
  if gender=='Male':
   red=next(c for c in clubs if (c.get('colorPalette') or {}).get('externalId')=='qid-Q17479');blue=next(c for c in clubs if (c.get('colorPalette') or {}).get('externalId')=='qid-Q50602')
  else:red,blue=[c for c in clubs if c['divisionTier']=='PrimeiraDivisao'][:2]
  c,b=red['id'],blue['id'];join(w,c,owner);join(w,b,other)
  before=fingerprint(w);path=appearance(w,c);assert call(path,owner=owner)['preferences']['mode']=='Club'
  prefs={'mode':'Custom','background':'#000000','primary':'#C3281E','secondary':'#000000','text':'#FFFFFF','accent':'#FF0000'}
  cmd={'preferences':prefs,'expectedVersion':0}
  def save(_):
   try:return call(path,cmd,owner)['preferences']['version']
   except RuntimeError as e:assert 'HTTP 409' in str(e);return 'CONFLICT'
  with ThreadPoolExecutor(max_workers=4) as pool:attempts=list(pool.map(save,range(4)))
  assert attempts.count(1)==1 and attempts.count('CONFLICT')==3,attempts
  state=call(path,owner=owner);assert state['preferences']['background']=='#000000'
  assert call(path,owner=other)['preferences']['mode']=='Club'
  denied(path,cmd,other,400);denied(path,None,None,401);denied(path,None,str(uuid.uuid4()),404)
  ownblue=call(appearance(w,b),owner=other);blueprefs={**prefs,'primary':'#0000FF','secondary':'#FFFFFF','accent':'#77B5FE'}
  call(appearance(w,b),{'preferences':blueprefs,'expectedVersion':ownblue['preferences']['version']},other)
  assert call(path,owner=owner)['preferences']==state['preferences']
  assert fingerprint(w)==before
  reset=call(path,{'preferences':{'mode':'Club'},'expectedVersion':1},owner);assert reset['preferences']['mode']=='Club'
  assert fingerprint(w)==before
  scene(w,c,owner,gender.lower()+'-red');scene(w,b,other,gender.lower()+'-blue')
  if gender=='Male':
   # Advance only three official rounds to record real grouped live matches.
   for rnd in range(1,4):
    for coach,club in [(owner,c),(other,b)]:
     snap=call(f'/api/worlds/{w}/snapshot');d=call(f'/api/worlds/{w}/clubs/{club}')
     call(f'/api/worlds/{w}/tactics',{'gameWorldId':w,'coachId':coach,'formation':d['preferredFormation'],'starterPlayerIds':d['preferredStarterIds'],'benchPlayerIds':d['preferredBenchIds'],'idempotencyKey':uuid.uuid4().hex,'expectedRoundVersion':snap['roundVersion']},coach)
    if rnd==3:
     scene(w,c,owner,'matches-red');scene(w,b,other,'matches-blue')
     snap=call(f'/api/worlds/{w}/snapshot');assert len(snap['matches'])==16
     assert any(m.get('homeColorPalette') or m.get('awayColorPalette') for m in snap['matches'])
    for step in range(30):
     snap=call(f'/api/worlds/{w}/snapshot')
     if snap['roundState']=='RESULTS':break
     if snap['activePauseWindow']:
      for coach in [owner,other]:call(f'/api/worlds/{w}/continue',{'gameWorldId':w,'coachId':coach,'idempotencyKey':uuid.uuid4().hex},coach)
     else:call(f'/api/worlds/{w}/tick?ticks=125',{},owner)
    assert call(f'/api/worlds/{w}/snapshot')['roundState']=='RESULTS'
    if rnd<3:call(f'/api/worlds/{w}/next-round',{},owner)
  contexts.append({'owner':owner,'club':c,'world':w,'preferences':call(path,owner=owner)['preferences'],'fingerprint':fingerprint(w)})
  contexts.append({'owner':other,'club':b,'world':w,'preferences':call(appearance(w,b),owner=other)['preferences'],'fingerprint':fingerprint(w)})
  write(LOCAL/'appearance-context.json',contexts)
  results.append({'Gender':gender,'World':w,'PrivateTrainers':2,'ConcurrentSaves':attempts,'NoGameplayWritesByAppearance':True,'ResetOriginal':True,'UnauthorizedRejected':True,'DatabasePersistence':True})
  write(AUDIT/'appearance-http.json',{'Status':'PASS' if len(results)==2 else 'RUNNING','Cases':results})
  print('PASS actual HTTP '+gender+': colors, private preferences, concurrent writes, reset, unchanged gameplay snapshot',flush=True)
if __name__=='__main__':verify() if '--verify-restart' in sys.argv else run()
