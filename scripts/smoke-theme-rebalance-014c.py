"""Local HTTP/PostgreSQL checks for the 014C appearance rebalance (API 5155 only)."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import json, runpy, sys, hashlib
ROOT = Path(__file__).resolve().parents[1]
assert str(ROOT).lower() == r'd:\elifoot_98_014c'
m = runpy.run_path(str(ROOT / 'scripts/smoke-club-colors-014c.py'))
call, fingerprint, appearance = m['call'], m['fingerprint'], m['appearance']
OUT = ROOT / 'docs/audit-014c-rebalance'
STATE = OUT / 'http-restart-state.json'
st = json.loads((OUT / 'browser-review-world.json').read_text(encoding='utf-8'))
w, red, blue = st['World'], st['Red'], st['Blue']
p = appearance(w, red['club'])

def write(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')

def record(label):
    folder = ROOT / 'artifacts/014c-rebalance/scenes'
    folder.mkdir(parents=True, exist_ok=True)
    snap = call(f'/api/worlds/{w}/snapshot')
    for color, club in [('red', red), ('blue', blue)]:
        scene = {'coach': club['coach'], 'club': club['club'], 'clubs': call(f'/api/worlds/{w}/clubs'),
                 'detail': call(f'/api/worlds/{w}/clubs/{club["club"]}'), 'snapshot': snap,
                 'appearance': call(appearance(w, club['club']), owner=club['coach'])}
        file = folder / f'{label}-{color}.json'
        write(file, scene)
    return {'Label': label, 'Round': snap['roundNumber'], 'Competition': snap['competitionType'],
            'Matches': len(snap['matches']), 'State': snap['roundState']}

stage = sys.argv[1]
if stage == 'old-saves':
    contexts = json.loads((ROOT / 'artifacts/014c/appearance-context.json').read_text(encoding='utf-8'))
    results = []
    for row in contexts:
        actual = call(appearance(row['world'], row['club']), owner=row['owner'])['preferences']
        original = row['preferences']
        added = {k: v for k, v in actual.items() if k not in original}
        compatible = all(actual.get(k) == v for k, v in original.items()) and all(
            k in ('themeVariant', 'brightness', 'matchColors') and v is None for k, v in added.items())
        results.append({'World': row['world'], 'Club': row['club'], 'AllOriginalPreferenceFieldsPreserved': compatible,
                        'AddedOptionalNullFields': list(added), 'WorldFingerprintUnchanged': fingerprint(row['world']) == row['fingerprint'],
                        'Mode': actual['mode']})
    passed = all(r['AllOriginalPreferenceFieldsPreserved'] and r['WorldFingerprintUnchanged'] for r in results)
    write(OUT / 'old-save-http-verification.json', {'Status': 'PASS' if passed else 'PARTIAL',
          'Method': 'Read existing saved 014C worlds after rebalance API restart; original fields must match and only optional null fields may be added. No writes.', 'Contexts': results})
    assert passed, results
    print(f'PASS: {len(contexts)} private preference contexts in two old saved worlds preserved; optional fields added as null.')
elif stage == 'save':
    before = fingerprint(w)
    original = call(p, owner=red['coach'])['preferences']
    other = call(appearance(w, blue['club']), owner=blue['coach'])['preferences']
    pref = {'mode': 'Classic', 'themeVariant': 'Escuro', 'brightness': 1, 'matchColors': 'Neutral'}
    cmd = {'preferences': pref, 'expectedVersion': original['version']}
    def attempt(_):
        try:
            return call(p, cmd, red['coach'])['preferences']['version']
        except RuntimeError as e:
            assert 'HTTP 409' in str(e), str(e)
            return 'CONFLICT'
    with ThreadPoolExecutor(max_workers=4) as pool:
        outcomes = list(pool.map(attempt, range(4)))
    saved = call(p, owner=red['coach'])['preferences']
    assert outcomes.count(saved['version']) == 1 and outcomes.count('CONFLICT') == 3, outcomes
    assert all(saved[k] == v for k, v in pref.items())
    assert call(appearance(w, blue['club']), owner=blue['coach'])['preferences'] == other
    assert fingerprint(w) == before
    write(STATE, {'Original': original, 'Saved': saved, 'Other': other, 'Fingerprint': before, 'Outcomes': outcomes})
    print('PASS: four concurrent HTTP writes; one accepted, three conflicts; world and other coach unchanged.')
elif stage == 'verify':
    data = json.loads(STATE.read_text(encoding='utf-8'))
    assert call(p, owner=red['coach'])['preferences'] == data['Saved']
    assert call(appearance(w, blue['club']), owner=blue['coach'])['preferences'] == data['Other']
    assert fingerprint(w) == data['Fingerprint']
    restored = call(p, {'preferences': data['Original'], 'expectedVersion': data['Saved']['version']}, red['coach'])['preferences']
    assert all(restored[k] == v for k, v in data['Original'].items() if k != 'version')
    assert fingerprint(w) == data['Fingerprint']
    write(OUT / 'http-postgres-acceptance.json', {'Status': 'PASS', 'World': w, 'ApiRestart': True,
          'Storage': 'PostgreSQL 18 / own 014C 55438', 'ConcurrentWrites': 4, 'Accepted': 1, 'Conflicts': 3,
          'NewFieldsRestored': data['Saved'], 'PrivateCoachScope': True, 'WorldUnchanged': True,
          'WorldFingerprint': data['Fingerprint'], 'OriginalPreferencesRestored': True})
    print('PASS: process restart retained all new fields, private scope and unchanged saved world; original preference restored.')
elif stage in ('record-league', 'record-cup'):
    result = record(stage.removeprefix('record-'))
    write(OUT / f'{stage}-scene.json', result)
    print(json.dumps(result))
else:
    raise SystemExit('Use save, verify, record-league or record-cup.')