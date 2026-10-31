"""Record authoritative local server snapshots for automated Flutter visual verification."""
from pathlib import Path
import runpy,json,uuid,urllib.parse
ROOT=Path(__file__).resolve().parents[1];LOCAL=ROOT/'artifacts/014b';AUDIT=ROOT/'docs/audit-014b'
call=runpy.run_path(str(ROOT/'scripts/smoke-final-worlds-014b.py'))['call']
def save(name,value):(LOCAL/name).write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf-8')
c=json.loads((LOCAL/'custom-assemblies-context.json').read_text())[0];owner=c['Owner']
world=call('/api/world-creator/assemblies/'+c['Assembly']+'/confirm',{'idempotencyKey':'ui-capture-'+uuid.uuid4().hex,'maxHumans':2,'worldName':'014B Auditoria de interface'},owner)['id']
clubs=call(f'/api/worlds/{world}/clubs');club=next(x['id'] for x in clubs if x['divisionTier']=='PrimeiraDivisao')
other=call('/api/auth/local-login',{'nickname':'014B Segundo treinador','deviceId':'014b-ui-'+uuid.uuid4().hex})['coachId']
def join(coach,unemployed=False):
 query={'coachId':coach,'nickname':'Treinador 014B','deviceId':'014b-ui-'+coach,'joinUnemployed':str(unemployed).lower()}
 if not unemployed:query['preferredClubId']=club
 return call(f'/api/worlds/{world}/join?'+urllib.parse.urlencode(query),{},coach)
join(owner);join(other,True)
def ready(coach,unemployed=False):
 snap=call(f'/api/worlds/{world}/snapshot');command={'gameWorldId':world,'coachId':coach,'idempotencyKey':uuid.uuid4().hex,'expectedRoundVersion':snap['roundVersion']}
 if not unemployed:
  detail=call(f'/api/worlds/{world}/clubs/{club}');command.update(formation=detail['preferredFormation'],starterPlayerIds=detail['preferredStarterIds'],benchPlayerIds=detail['preferredBenchIds'])
 return call(f'/api/worlds/{world}/'+('ready-unemployed' if unemployed else 'tactics'),command,coach)
def record(label):
 snap=call(f'/api/worlds/{world}/snapshot');save('ui-scene-'+label+'.json',{'coach':owner,'club':club,'clubs':clubs,
  'detail':call(f'/api/worlds/{world}/clubs/{club}'),'snapshot':snap})
 return snap
ready(owner);snap=record('waiting');assert snap['roundState']=='PREPARING' and snap['readyCounter']['readyCount']==1 and snap['readyCounter']['totalHumans']==2
ready(other,True);snap=record('cup');assert snap['roundState']=='RUNNING_FIRST_HALF' and len(snap['matches'])>0
for rnd in range(1,4):
 if rnd>1:ready(owner);ready(other,True)
 if rnd==3:
  snap=record('league');assert snap['competitionType']=='League'
  assert len(snap['matches'])==16
  for d in ['PrimeiraDivisao','SegundaDivisao','TerceiraDivisao','QuartaDivisao']:assert sum(m['divisionTier']==d for m in snap['matches'])==4
 for step in range(30):
  snap=call(f'/api/worlds/{world}/snapshot')
  if snap['roundState']=='RESULTS':break
  if snap['activePauseWindow']:
   if rnd==3:record('pause')
   for coach in [owner,other]:call(f'/api/worlds/{world}/continue',{'gameWorldId':world,'coachId':coach,'idempotencyKey':uuid.uuid4().hex},coach)
  else:call(f'/api/worlds/{world}/tick?ticks=125',{},owner)
 snap=call(f'/api/worlds/{world}/snapshot');assert snap['roundState']=='RESULTS'
 if rnd==3:record('results')
 else:call(f'/api/worlds/{world}/next-round',{},owner)
(AUDIT/'ui-server-scenes.json').write_text(json.dumps({'Status':'PASS','World':world,'AuthoritativeWaiting':'1/2 PREPARING','CupFixtures':len(json.loads((LOCAL/'ui-scene-cup.json').read_text())['snapshot']['matches']),
 'LeagueFixtures':16,'LeagueGamesPerDivision':4,'RealEngineRoundsPlayed':3,'Scenes':['waiting','cup','league','pause','results'],
 'Scope':'Authoritative HTTP responses for automated renderer; not Chrome screenshots.'},indent=2),encoding='utf-8')
print('PASS: real server scenes record readiness 1/2, start, official cup fixtures, 4 x 4 league fixtures, pauses and actual results.',flush=True)
