"""Advance only the owned review world through two cup rounds to its real league board."""
from pathlib import Path
import runpy,json,uuid
ROOT=Path(__file__).resolve().parents[1];m=runpy.run_path(str(ROOT/'scripts/smoke-club-colors-014c.py'));call=m['call']
fixtures=json.loads((ROOT/'docs/audit-014c/browser-fixtures.json').read_text(encoding='utf-8'))
w=next(x['World'] for x in fixtures['Worlds'] if x['Name']=='014C Visual Flamengo')
snap=call(f'/api/worlds/{w}/snapshot')
if snap['roundNumber']==3 and snap['competitionType']=='League':
 print('Review league already prepared; no state changes');raise SystemExit(0)
assert snap['roundNumber']==1,'Use a fresh review world; this runner never skips an existing later season/round.'
people=snap['readyCounter']['participants'];assert len(people)==1
owner=people[0]['coachId'];club=people[0]['clubId']
for rnd in range(1,4):
 snap=call(f'/api/worlds/{w}/snapshot')
 if snap['roundState']=='PREPARING':
  d=call(f'/api/worlds/{w}/clubs/{club}')
  call(f'/api/worlds/{w}/tactics',{'gameWorldId':w,'coachId':owner,'formation':d['preferredFormation'],'starterPlayerIds':d['preferredStarterIds'],'benchPlayerIds':d['preferredBenchIds'],'idempotencyKey':uuid.uuid4().hex,'expectedRoundVersion':snap['roundVersion']},owner)
 if rnd==3:break
 for step in range(30):
  snap=call(f'/api/worlds/{w}/snapshot')
  if snap['roundState']=='RESULTS':break
  if snap['activePauseWindow']:call(f'/api/worlds/{w}/continue',{'gameWorldId':w,'coachId':owner,'idempotencyKey':uuid.uuid4().hex},owner)
  else:call(f'/api/worlds/{w}/tick?ticks=125',{},owner)
 assert call(f'/api/worlds/{w}/snapshot')['roundState']=='RESULTS'
 call(f'/api/worlds/{w}/next-round',{},owner)
snap=call(f'/api/worlds/{w}/snapshot');assert snap['competitionType']=='League' and len(snap['matches'])==16
(ROOT/'docs/audit-014c/browser-league-preparation.json').write_text(json.dumps({'Status':'PASS','World':w,'OfficialRoundsPrepared':3,'Fixtures':16,'Note':'Local API advanced real engine; browser displays actual SignalR state, not a mocked board.'},indent=2),encoding='utf-8')
print('Real league board ready in browser review world')
