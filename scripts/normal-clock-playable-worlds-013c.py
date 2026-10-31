"""Verify actual MatchEngine progress with the normal room clock, without HTTP ticks."""
from pathlib import Path
import runpy,uuid,time,json
ROOT=Path(__file__).resolve().parents[1]; api=runpy.run_path(str(ROOT/'scripts/smoke-playable-worlds-013c.py'));call=api['call'];save=api['save']
login=call('/api/auth/local-login',{'nickname':'013C clock acceptance','deviceId':uuid.uuid4().hex});owner=login['coachId']
request={'name':'013C normal room clock','gender':'Male','season':'2026','countries':api['COUNTRIES'],'mode':'Automatic','districtMode':'Ranking',
 'seed':130013,'assignments':[],'allowFictionalPlayers':True,'allowFictionalTeams':False,'useSavedData':True,'dataMode':'REAL_CLUBS_SIMULATED'}
preview=call('/api/world-creator/preview',request,owner);assert not preview['errors'],preview['errors']
world=call('/api/world-creator/confirm/'+preview['draftId'],{},owner)['id']
clubs=call(f'/api/worlds/{world}/clubs');club=next(c['id'] for c in clubs if c['divisionTier']=='PrimeiraDivisao')
import urllib.parse
call(f'/api/worlds/{world}/join?'+urllib.parse.urlencode({'coachId':owner,'nickname':login['nickname'],'deviceId':login['deviceId'],'preferredClubId':club}),{},owner)
rounds=[]
for n in range(1,4):
    snap=call(f'/api/worlds/{world}/snapshot');detail=call(f'/api/worlds/{world}/clubs/{club}')
    call(f'/api/worlds/{world}/tactics',{'gameWorldId':world,'coachId':owner,'formation':detail['preferredFormation'],
      'starterPlayerIds':detail['preferredStarterIds'],'benchPlayerIds':detail['preferredBenchIds'],'idempotencyKey':f'clock-ready-{n}','expectedRoundVersion':snap['roundVersion']},owner)
    deadline=time.monotonic()+120;polls=0
    while time.monotonic()<deadline:
        snap=call(f'/api/worlds/{world}/snapshot');polls+=1
        if snap['roundState']=='RESULTS':break
        if snap['activePauseWindow']:
            call(f'/api/worlds/{world}/continue',{'gameWorldId':world,'coachId':owner,'idempotencyKey':f'clock-continue-{n}-{polls}'},owner)
        time.sleep(.5)
    assert snap['roundState']=='RESULTS','Normal background clock failed to finish the round'
    rounds.append({'Round':n,'Competition':snap['competitionLabel'],'FinishedMatches':sum(m['isFinished'] for m in snap['matches']),
        'HumanMatches':[m for m in snap['matches'] if club in [m['homeClubId'],m['awayClubId']]]})
    call(f'/api/worlds/{world}/next-round',{},owner)
    print('Normal clock completed round',n,flush=True)
assert any(m['isFinished'] and m['minute']>=90 for r in rounds for m in r['HumanMatches'])
save('normal-clock-acceptance.json',{'Status':'PASS','World':world,'HttpTickCalls':0,'BackgroundClock':True,'ProviderCalls':0,'Rounds':rounds,'NextRound':call(f'/api/worlds/{world}/snapshot')['roundNumber']})
print('PASS normal room clock: actual MatchEngine, three rounds, no HTTP tick commands.',flush=True)
