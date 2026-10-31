"""Real HTTP/PostgreSQL acceptance of private pre-save assemblies, without editing existing saves."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import runpy,json,uuid,hashlib,copy,sys
ROOT=Path(__file__).resolve().parents[1]
m=runpy.run_path(str(ROOT/'scripts/smoke-final-worlds-014b.py'));call=m['call']
AUDIT=ROOT/'docs/audit-014b';LOCAL=ROOT/'artifacts/014b'
def write(path,value):path.write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf-8')
def rejected(path,body,owner,status):
 try:call(path,body,owner)
 except RuntimeError as e: assert f'HTTP {status}' in str(e),str(e)
 else:raise AssertionError('Expected rejection: '+path)
def exported(world):
 p=call(f'/api/worlds/{world}/editor/export-json');p.pop('exportedAtUtc',None)
 return p
def fingerprint(world):return hashlib.sha256(json.dumps(exported(world),sort_keys=True).encode()).hexdigest()
def verify():
 for c in json.loads((LOCAL/'custom-assemblies-context.json').read_text(encoding='utf-8')):
  assert fingerprint(c['World'])==c['Fingerprint']
  a=call('/api/world-creator/assemblies/'+c['Assembly'],coach=c['Owner']);assert a['isSaved'] and a['validation']['isValid']
  r=call('/api/world-creator/assemblies/'+c['Assembly']+'/confirm',{'idempotencyKey':'same-request'},c['Owner'])
  assert r['id']==c['World'] and fingerprint(c['World'])==c['Fingerprint']
 write(AUDIT/'custom-assemblies-restart.json',{'Status':'PASS','PrivatePresetsPersisted':True,'IndependentWorldsUnchanged':True,'ConfirmationReplayUnchanged':True})
 print('PASS: private assemblies and independent snapshots survive process/database restart',flush=True)
def run():
 results=[];contexts=[]
 for gender in ['Male','Female']:
  owner=call('/api/auth/local-login',{'nickname':'014B editor '+gender,'deviceId':'014b-editor-'+uuid.uuid4().hex})['coachId']
  stranger=str(uuid.uuid4());base='/api/world-creator/assemblies'
  request={'name':'Montagem privada '+gender,'gender':gender,'countries':['BR','PT'],'seed':199801,'allowPersonalizedContent':False}
  rejected(base,request,owner,400);request['allowPersonalizedContent']=True
  a=call(base,request,owner);assert a['validation']['isValid'],a['validation'];path=base+'/'+a['id'];package=copy.deepcopy(a['package'])
  rejected(path,None,stranger,404);rejected(path+'/confirm',{'idempotencyKey':'theft'},stranger,404)
  assert call(base,coach=stranger)==[]
  club=package['clubs'][0];club['name']='Clube Montagem '+gender
  first=next(p for p in package['players'] if p['clubId']==club['id']);first.update(name='Jogador Montagem '+gender,strength=43,salaryPerRound=12345,contractRoundsRemaining=45,isStar=True)
  second=next(c for c in package['clubs'] if c['divisionTier']=='SegundaDivisao');club['divisionTier'],second['divisionTier']=second['divisionTier'],club['divisionTier']
  def update(p,version):return {'name':request['name'],'countries':a['countries'],'package':p,'expectedVersion':version,'savePreset':True,'allowPersonalizedContent':True}
  cmd=update(package,a['version']);a=call(path,cmd,owner);assert a['isSaved'] and a['validation']['isValid'],a['validation'];rejected(path,cmd,owner,409)
  bad=copy.deepcopy(a['package']);bad['players']=[p for p in bad['players'] if not(p['clubId']==club['id'] and p['position']=='G')]
  a=call(path,update(bad,a['version']),owner);assert not a['validation']['isValid'];rejected(path+'/confirm',{'idempotencyKey':'invalid'},owner,400)
  assert not call(path,coach=owner)['validation']['isValid']
  a=call(path,update(package,a['version']),owner);assert a['validation']['isValid'],a['validation']
  csv=call(base+'/players/export-csv',{'package':a['package']},owner)['csv']
  parsed=call(base+'/players/parse-csv',{'csv':csv},owner);assert not parsed['errors'];assert len(parsed['players'])==2304
  with ThreadPoolExecutor(max_workers=6) as pool:
   receipts=list(pool.map(lambda _:call(path+'/confirm',{'idempotencyKey':'same-request','maxHumans':4},owner),range(6)))
  assert len({r['id'] for r in receipts})==1;one=receipts[0]['id'];two=call(path+'/confirm',{'idempotencyKey':'another-game','maxHumans':4},owner)['id']
  p1=exported(one);p2=exported(two)
  for collection in ['clubs','players','aiManagers']:
   assert not ({x['id'] for x in p1[collection]} & {x['id'] for x in p2[collection]})
  assert len(p1['clubs'])==128 and len(p1['players'])==2304
  actual=next(p for p in p1['players'] if p['name']==first['name']);assert actual['salaryPerRound']==12345 and actual['contractRoundsRemaining']==45
  before=fingerprint(one);before2=fingerprint(two)
  liveclub=p1['clubs'][0];liveplayer=next(p for p in p1['players'] if p['clubId']==liveclub['id'])
  for route,payload in [('update-club',liveclub),('create-club',liveclub),('update-player',liveplayer),('update-manager',p1['aiManagers'][0])]:
   rejected(f'/api/worlds/{one}/editor/'+route,payload,owner,409)
  edited=copy.deepcopy(a['package']);edited['players'][0]['strength']=1
  a=call(path,update(edited,a['version']),owner)
  assert fingerprint(one)==before and fingerprint(two)==before2
  assert any(x['id']==a['id'] for x in call(base,coach=owner));assert call(base,coach=stranger)==[]
  contexts.append({'Owner':owner,'Assembly':a['id'],'World':one,'OtherWorld':two,'Fingerprint':before})
  write(LOCAL/'custom-assemblies-context.json',contexts)
  # Frozen real server responses feed renderer tests; these are not claimed as Chrome screenshots.
  capture={'assembly':a,'world':one,'snapshot':call(f'/api/worlds/{one}/snapshot'),'package':p1}
  write(LOCAL/('custom-ui-'+gender+'.json'),capture)
  results.append({'Gender':gender,'PrivateOwnership':True,'SavedAndRestored':True,'InvalidDraftRetained':True,'InvalidConfirmationBlocked':True,
   'IndependentWorlds':[one,two],'UniquePlayersPerWorld':2304,'ContractsAndSalaryPreserved':True,'ConcurrentConfirmationRetries':6,
   'ExistingWorldsUnchanged':True,'LiveEditorMutationsBlocked':4,'CsvRoundTrip':2304})
  write(AUDIT/'custom-assemblies-http.json',{'Status':'PASS' if len(results)==2 else 'RUNNING','Cases':results})
  print('PASS private '+gender+': 128 clubs, 2304 players; private draft, invalid roster, independent IDs, salary, concurrent retry, JSON/CSV and active-save guards',flush=True)
if __name__=='__main__':verify() if '--verify-restart' in sys.argv else run()
