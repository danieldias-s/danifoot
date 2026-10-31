"""ELIFOOT_VISUAL_THEME_REBALANCE_014C — prepare a fresh real-club review world on the own 014C API (5155).

Creates a male REAL_CLUBS_SIMULATED world with Flamengo (red/black) and Manchester City (blue/white) controlled
by two local coaches, advances the real engine and records browser sessions for:
  - round 3 (league, 4 divisions grouped) running,
  - round 5 (cup round of 64 with division clubs) running.
Only the local API is used; game rules are not touched.
"""
from pathlib import Path
import runpy, json, uuid, sys, urllib.parse

ROOT = Path(__file__).resolve().parents[1]
m = runpy.run_path(str(ROOT / 'scripts/smoke-club-colors-014c.py'))
call = m['call']
OUT = ROOT / 'docs/audit-014c-rebalance'
OUT.mkdir(parents=True, exist_ok=True)
stage = sys.argv[1] if len(sys.argv) > 1 else 'create'
STATE = OUT / 'browser-review-world.json'


def login(nick, device):
    return call('/api/auth/local-login', {'nickname': nick, 'deviceId': device})['coachId']


def play_round(w, coaches, stop_running=False):
    for coach, club in coaches:
        snap = call(f'/api/worlds/{w}/snapshot')
        if snap['roundState'] != 'PREPARING':
            break
        d = call(f'/api/worlds/{w}/clubs/{club}')
        call(f'/api/worlds/{w}/tactics', {'gameWorldId': w, 'coachId': coach, 'formation': d['preferredFormation'],
             'starterPlayerIds': d['preferredStarterIds'], 'benchPlayerIds': d['preferredBenchIds'],
             'idempotencyKey': uuid.uuid4().hex, 'expectedRoundVersion': snap['roundVersion']}, coach)
    if stop_running:
        call(f'/api/worlds/{w}/tick?ticks=40', {}, coaches[0][0])
        return call(f'/api/worlds/{w}/snapshot')
    for _ in range(40):
        snap = call(f'/api/worlds/{w}/snapshot')
        if snap['roundState'] == 'RESULTS':
            break
        if snap.get('activePauseWindow') or 'PAUSED' in snap['roundState'] or 'BREAK' in snap['roundState']:
            for coach, _ in coaches:
                call(f'/api/worlds/{w}/continue', {'gameWorldId': w, 'coachId': coach, 'idempotencyKey': uuid.uuid4().hex}, coach)
        else:
            call(f'/api/worlds/{w}/tick?ticks=125', {}, coaches[0][0])
    assert call(f'/api/worlds/{w}/snapshot')['roundState'] == 'RESULTS'
    call(f'/api/worlds/{w}/next-round', {}, coaches[0][0])


if stage == 'create':
    red_dev, blue_dev = 'rebal014c-red-' + uuid.uuid4().hex[:8], 'rebal014c-blue-' + uuid.uuid4().hex[:8]
    owner, other = login('Rebalance Vermelho', red_dev), login('Rebalance Azul', blue_dev)
    req = {'name': '014C Rebalance Verde', 'gender': 'Male', 'season': '2026',
           'countries': ['BR', 'AR', 'GB', 'ES', 'DE', 'IT', 'FR', 'PT'], 'mode': 'Custom', 'districtMode': 'Manual',
           'seed': 140015, 'assignments': [], 'fillRemainingDivisions': False, 'allowFictionalTeams': False,
           'allowFictionalPlayers': True, 'useSavedData': True, 'maxHumans': 8, 'dataMode': 'REAL_CLUBS_SIMULATED'}
    catalog = call('/api/world-creator/catalog', req, owner)['clubs']
    red = next(c for c in catalog if c['externalId'] == 'qid-Q17479')
    blue = next(c for c in catalog if c['externalId'] == 'qid-Q50602')
    # Prefer clubs with registered palettes in the four divisions so the board shows real colors.
    rest = sorted([c for c in catalog if c['key'] not in (red['key'], blue['key'])], key=lambda c: not bool(c.get('colorPalette')))
    chosen = ([red, blue] + rest)[:128]
    req['assignments'] = [{'clubKey': c['key'], 'division': i // 8 + 1 if i < 32 else 5} for i, c in enumerate(chosen)]
    preview = call('/api/world-creator/preview', req, owner)
    assert not preview['errors'], preview['errors']
    w = call('/api/world-creator/confirm/' + preview['draftId'], {}, owner)['id']
    clubs = call(f'/api/worlds/{w}/clubs')
    rc = next(c for c in clubs if (c.get('colorPalette') or {}).get('externalId') == 'qid-Q17479')
    bc = next(c for c in clubs if (c.get('colorPalette') or {}).get('externalId') == 'qid-Q50602')
    for coach, club, nick, dev in [(owner, rc, 'Rebalance Vermelho', red_dev), (other, bc, 'Rebalance Azul', blue_dev)]:
        call(f'/api/worlds/{w}/join?' + urllib.parse.urlencode({'coachId': coach, 'nickname': nick, 'deviceId': dev,
             'preferredClubId': club['id']}), {}, coach)
    coaches = [(owner, rc['id']), (other, bc['id'])]
    # Rounds 1-2: district cup preliminaries; round 3: first league round (left running).
    play_round(w, coaches); play_round(w, coaches)
    snap = play_round(w, coaches, stop_running=True)
    assert snap['competitionType'] == 'League' and len(snap['matches']) == 16, snap['competitionType']
    painted = sum(1 for x in snap['matches'] for k in ('homeColorPalette', 'awayColorPalette') if x.get(k))
    state = {'World': w, 'Red': {'coach': owner, 'club': rc['id'], 'name': rc['name'], 'nickname': 'Rebalance Vermelho', 'deviceId': red_dev},
             'Blue': {'coach': other, 'club': bc['id'], 'name': bc['name'], 'nickname': 'Rebalance Azul', 'deviceId': blue_dev},
             'Round': snap['roundNumber'], 'Matches': len(snap['matches']), 'TeamBlocksWithPalette': painted,
             'Clubs': len(clubs), 'ClubsWithPalette': sum(1 for c in clubs if c.get('colorPalette'))}
    STATE.write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(state, ensure_ascii=False))
elif stage == 'cup':
    st = json.loads(STATE.read_text(encoding='utf-8'))
    w = st['World']; coaches = [(st['Red']['coach'], st['Red']['club']), (st['Blue']['coach'], st['Blue']['club'])]
    # Finish round 3, play round 4 and leave round 5 (cup round of 64) running.
    snap = call(f'/api/worlds/{w}/snapshot')
    while snap['roundNumber'] < 5:
        play_round(w, coaches)
        snap = call(f'/api/worlds/{w}/snapshot')
    snap = play_round(w, coaches, stop_running=True)
    assert snap['competitionType'] != 'League', snap['competitionType']
    st['CupRound'] = snap['roundNumber']; st['CupMatches'] = len(snap['matches']); st['CupLabel'] = snap.get('competitionLabel')
    STATE.write_text(json.dumps(st, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({k: st[k] for k in ('CupRound', 'CupMatches', 'CupLabel')}, ensure_ascii=False))

elif stage == 'league-next':
    st = json.loads(STATE.read_text(encoding='utf-8'))
    w = st['World']; coaches = [(st['Red']['coach'], st['Red']['club']), (st['Blue']['coach'], st['Blue']['club'])]
    snap = call(f'/api/worlds/{w}/snapshot')
    assert snap['roundNumber'] in (5, 6), 'Review world must be in cup R5 or league R6.'
    if snap['roundNumber'] == 5:
        play_round(w, coaches)
    snap = call(f'/api/worlds/{w}/snapshot')
    if snap['roundState'] == 'PREPARING':
        snap = play_round(w, coaches, stop_running=True)
    assert snap['competitionType'] == 'League' and len(snap['matches']) == 16
    st['ReviewLeagueRound'] = snap['roundNumber']
    STATE.write_text(json.dumps(st, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'Round': snap['roundNumber'], 'Competition': snap['competitionType'], 'Matches': len(snap['matches'])}))
