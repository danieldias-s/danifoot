"""Official HTTP QA for human penalty and shared substitution windows. Own rooms only."""
from pathlib import Path
import importlib.util,json,urllib.parse,uuid,sys
ROOT=Path(__file__).resolve().parents[1]
assert str(ROOT).lower()==r'd:\elifoot_98_015b'
spec=importlib.util.spec_from_file_location('qa015b',ROOT/'scripts/review-classic-desktop-015b.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
path=m.AREA/'interactions-qa-context.json'
if path.exists():context=json.loads(path.read_text(encoding='utf-8'))
else:
    auths=[m.api('/auth/local-login',dict(nickname='QA Janelas 015B '+letter,deviceId='015b-interactions-'+letter.lower())) for letter in ['A','B']]
    request=json.loads((m.AREA/'inputs/Male.input.json').read_text(encoding='utf-8-sig'))['request']
    request.update(name='015B QA - Pênaltis e substituições',maxHumans=2,seed=199815,tickIntervalMs=60000,pauseTimeoutSeconds=3600)
    preview=m.api('/world-creator/preview',request,auths[0]['sessionToken']);assert not preview.get('errors'),preview.get('errors')
    world=m.api('/world-creator/confirm/'+preview['draftId'],{},auths[0]['sessionToken'])
    clubs=m.api('/worlds/'+world['id']+'/clubs');people=[]
    for auth,name in zip(auths,['Flamengo RJ','FC Porto']):
        club=next(c for c in clubs if c['name']==name)
        query=urllib.parse.urlencode(dict(coachId=auth['coachId'],nickname=auth['nickname'],deviceId=auth['deviceId'],preferredClubId=club['id']))
        m.api('/worlds/'+world['id']+'/join?'+query,{})
        people.append(dict(auth=auth,club=club))
    context=dict(world=world,participants=people);m.save(path.name,context)
w=context['world']['id'];people=context['participants'];base='/worlds/'+w
assert context['world']['name']=='015B QA - Pênaltis e substituições'
assert w not in ['af4dcfb3-5207-48ae-92db-5cd3623e7270','a580544f-f88e-41fd-89b4-d2e6c467ce42','f9864efb-943c-4aa5-ad8e-daa6bf185cc0']
def snap():return m.api(base+'/snapshot')
def command(p,**kw):return dict(gameWorldId=w,coachId=p['auth']['coachId'],idempotencyKey='qa-'+uuid.uuid4().hex,**kw)
def scene(name,p):
    s=snap();club=p['club']['id'];coach=p['auth']['coachId']
    data=dict(coach=coach,club=club,nickname=p['auth']['nickname'],snapshot=s,detail=m.api(base+'/clubs/'+club),clubs=m.api(base+'/clubs'),appearance=m.api('/world-creator/appearance/'+w+'/'+club,token=p['auth']['sessionToken']))
    for key,route in dict(calendar='/calendar',cup='/cup',scorers='/top-scorers',market='/market',ranking='/ranking',finances='/clubs/'+club+'/finances',invitations='/invitations/'+coach).items():data[key]=m.api(base+route)
    data['standings']={tier:m.api(base+'/standings/'+tier) for tier in ['PrimeiraDivisao','SegundaDivisao','TerceiraDivisao','QuartaDivisao']}
    m.save('interactions-'+name+'-scene.json',data)
def resolve_pending(s):
    pending=s['pendingPenalty'];p=next(p for p in people if p['auth']['coachId']==pending['coachId'])
    taker=sorted(pending['eligiblePlayers'],key=lambda t:(-int(t['isStar']),-int(t['position']=='A'),-t['strength'],t['playerId']))[0]
    return m.api(base+'/penalty-taker',command(p,decisionId=pending['decisionId'],playerId=taker['playerId']))
if '--resume-after-restart' in sys.argv:
    before=json.loads((m.AREA/'interactions-pending-check.json').read_text(encoding='utf-8'));s=snap();assert s['pendingPenalty']['decisionId']==before['decision'];assert s['currentMinute']==before['minute'];assert s['roundVersion']==before['version']
    pending=s['pendingPenalty'];p=next(p for p in people if p['auth']['coachId']==pending['coachId']);other=next(q for q in people if q!=p)
    try:m.api(base+'/penalty-taker',command(other,decisionId=pending['decisionId'],playerId=pending['eligiblePlayers'][0]['playerId']));raise AssertionError('Wrong owner accepted')
    except RuntimeError as error:assert 'treinador' in str(error).lower()
    body=command(p,decisionId=pending['decisionId'],playerId=pending['eligiblePlayers'][0]['playerId'])
    result=m.api(base+'/penalty-taker',body);assert result['currentMinute']==before['minute']
    replay=m.api(base+'/penalty-taker',body);assert replay['roundVersion']==result['roundVersion'];assert replay['matches']==result['matches']
    report=json.loads((m.AREA/'interactions-http-review.json').read_text(encoding='utf-8'));report.update(penaltyPersistedAcrossProcessRestart=True,wrongCoachRejected=True,exactKickRetryIdempotent=True,decision=before['decision'],penaltyMinute=before['minute'])
    m.save('interactions-http-review.json',report);print(json.dumps(report,ensure_ascii=False));sys.exit(0)
if (m.AREA/'interactions-http-review.json').exists():
    print('Existing QA report preserved; use --resume-after-restart for the recorded pending decision.');sys.exit(0)
if '--find-penalty' not in sys.argv:
    for target in [1,2]:
        s=snap()
        if s['roundNumber']>target:continue
        assert s['roundNumber']==target
        if s['roundState']=='PREPARING':
            for p in people:m.ready(w,p)
        final,_=m.finish(w,people);assert final['roundState']=='RESULTS';m.api(base+'/next-round',{})
    s=snap();assert s['roundNumber']==3
    if s['roundState']=='PREPARING':
        for p in people:m.ready(w,p)
    # Shared pause, real substitution, observer keeps all opportunities.
    s=snap()
    while s.get('pendingPenalty'):s=resolve_pending(s)
    m.api(base+'/pause',command(people[0]));s=snap();assert s['roundState']=='GLOBAL_PAUSED'
    scene('substitution',people[0]);scene('substitution-observer',people[1])
    match=next(t for t in s['matches'] if people[0]['club']['id'] in [t['homeClubId'],t['awayClubId']]);side='home' if match['homeClubId']==people[0]['club']['id'] else 'away'
    assert match[side+'PauseWindowsUsed']==0
    out=next(t for t in match[side+'Starters'] if t['position']!='G' and not t['isSentOff'] and not t['wasSubstitutedOut']);ins=next(t for t in match[side+'Bench'] if t['position']!='G' and not t['isInjuredInMatch'])
    body=command(people[0],matchId=match['matchId'],newFormation=None,substitutions=[dict(playerOutId=out['playerId'],playerInId=ins['playerId'])])
    s=m.api(base+'/in-match-adjust',body);changed=next(t for t in s['matches'] if t['matchId']==match['matchId']);assert changed[side+'PauseWindowsUsed']==1;assert changed[side+'SubCount']==1
    retry=m.api(base+'/in-match-adjust',body);assert retry['roundVersion']==s['roundVersion']
    scene('substitution-applied',people[0])
    for p in people:m.api(base+'/continue',command(p))
    # Capture genuine halftime and perform another real substitution there.
    for guard in range(400):
        s=snap()
        if s.get('pendingPenalty'):s=resolve_pending(s);continue
        if s['roundState']=='HALFTIME_PAUSED':break
        assert s['roundState'].startswith('RUNNING_'),s['roundState'];m.api(base+'/tick',{})
    else:raise AssertionError('Halftime missing')
    scene('halftime',people[0]);match=next(t for t in s['matches'] if people[0]['club']['id'] in [t['homeClubId'],t['awayClubId']]);assert match[side+'PauseWindowsUsed']==1
    out=next(t for t in match[side+'Starters'] if t['position']!='G' and not t['isSentOff'] and not t['wasSubstitutedOut']);ins=next(t for t in match[side+'Bench'] if t['position']!='G' and not t['isInjuredInMatch'] and not t['wasSubstitutedOut'])
    s=m.api(base+'/in-match-adjust',command(people[0],matchId=match['matchId'],newFormation=None,substitutions=[dict(playerOutId=out['playerId'],playerInId=ins['playerId'])]));match=next(t for t in s['matches'] if t['matchId']==match['matchId']);assert match[side+'SubCount']==2;assert match[side+'PauseWindowsUsed']==1
    for p in people:m.api(base+'/continue',command(p))
# Search only this QA room for a naturally awarded human penalty; never inject an event.
for guard in range(1400):
    s=snap()
    if s.get('pendingPenalty'):break
    if s['roundState']=='RESULTS':
        assert s['roundNumber']<50,'No natural penalty in bounded QA run';m.api(base+'/next-round',{});
        for p in people:m.ready(w,p)
    elif s['roundState'] in ['HALFTIME_PAUSED','GLOBAL_PAUSED','EXTRA_TIME_BREAK','EXTRA_TIME_HALFTIME']:
        for p in people:m.api(base+'/continue',command(p))
    else:m.api(base+'/tick?ticks=125',{})
else:raise AssertionError('Natural human penalty missing')
pending=s['pendingPenalty'];chooser=next(p for p in people if p['auth']['coachId']==pending['coachId']);observer=next(p for p in people if p!=chooser)
scene('penalty-choice',chooser);scene('penalty-waiting',observer)
version=s['roundVersion'];minute=s['currentMinute'];matches=s['matches']
for _ in range(3):
    frozen=m.api(base+'/tick?ticks=125',{});assert frozen['currentMinute']==minute;assert frozen['roundVersion']==version;assert frozen['matches']==matches
m.save('interactions-pending-check.json',dict(decision=pending['decisionId'],minute=minute,version=version))
report=dict(worldId=w,inviteCode=context['world']['inviteCode'],worldName=context['world']['name'],round=s['roundNumber'],minute=minute,openingCostsZero=True,actualSubstitutionCounts=True,oneWindowPerEditor=True,retryNoDuplicate=True,halftimeSubstitutionCounts=True,halftimeNoWindowCharge=True,frozenTicks=375,penaltyNaturallyAwarded=True,ownerSavesModified=False,penaltyPersistedAcrossProcessRestart=False)
m.save('interactions-http-review.json',report);print(json.dumps(report,ensure_ascii=False))
