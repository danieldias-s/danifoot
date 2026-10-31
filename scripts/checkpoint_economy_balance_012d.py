"""Preserve test artifacts and inventory this task before its local Git checkpoint."""
from pathlib import Path
import hashlib,json,subprocess

root=Path(__file__).resolve().parents[1];audit=root/'docs/audit-012d-fix'
def git(*args):return subprocess.check_output(['git',*args],cwd=root)
assert git('branch','--show-current').decode().strip()=='master'
assert git('merge-base','--is-ancestor','7529e69','HEAD')==b''
original_status=json.loads((audit/'baseline/manifest.json').read_text(encoding='utf-8-sig'))['TrackedStatus'];assert not original_status.strip()
historical=['docs/audit-007/controlled/behavior-controlled.json','docs/audit-008/browser-fixture.json','docs/audit-009/api-performance-1024.json',
    'docs/audit-009/api-performance-128.json','docs/audit-009/browser-fixture.json']
restored=[]
for rel in historical:
    p=root/rel;original=git('show','7529e69:'+rel);current=p.read_bytes()
    if original.replace(b'\r\n',b'\n')==current.replace(b'\r\n',b'\n'):continue
    copy=audit/'test-artifact-overwrites'/rel;copy.parent.mkdir(parents=True,exist_ok=True);copy.write_bytes(current)
    p.write_bytes(original)
    restored.append({'Path':rel,'OriginalSha256':hashlib.sha256(original).hexdigest(),'RunSha256':hashlib.sha256(current).hexdigest(),'RunCopy':str(copy.relative_to(root))})
restore_file=audit/'historical-artifacts-restored.json'
prior=json.loads(restore_file.read_text()) if restore_file.exists() else []
restore_file.write_text(json.dumps(prior+restored,indent=2),encoding='utf-8')
tracked=git('diff','--name-only','7529e69').decode().splitlines()
untracked=git('ls-files','--others','--exclude-standard','src','tests','scripts','client/elifoot_flutter/lib','client/elifoot_flutter/test').decode().splitlines()
deliverables=['docs/ECONOMY_CASH_ROOT_CAUSE_012D.md','docs/ECONOMY_CASH_FLOW_012D.json','docs/ECONOMY_100_SEASONS_BEFORE_AFTER_012D.md',
    'docs/ECONOMY_BALANCE_FIX_012D.md','docs/ECONOMY_FINAL_VALIDATION_012D.md']
paths=sorted(set(tracked+untracked+deliverables));files=[]
for rel in paths:
    if rel.startswith('docs/audit-') or not (root/rel).is_file():continue
    files.append({'Path':rel,'Sha256':hashlib.sha256((root/rel).read_bytes()).hexdigest()})
evidence=[]
for area in [audit/'experiments/balance-012d-fix',audit/'verified/balance-012d-fix']:
    if not area.exists():continue
    for folder in area.iterdir():
        if not (folder/'summary.json').exists():continue
        hashes={}
        for name in ['summary.json','seasons.json','ledger.jsonl.gz','clubs.json.gz']:
            p=folder/name;h=hashlib.sha256()
            with p.open('rb') as stream:
                for chunk in iter(lambda:stream.read(1024*1024),b''):h.update(chunk)
            hashes[name]=h.hexdigest()
        evidence.append({'Directory':str(folder.relative_to(root)),'Sha256':hashes})
manifest={'Task':'ELIFOOT_ECONOMY_LONG_TERM_BALANCE_012D_FIX','Baseline':'7529e69','Policy':'CLASSIC_OPERATING_BALANCE_012D_FIX_V2',
    'Files':files,'Evidence':evidence,'OtherWorktrees':git('worktree','list','--porcelain').decode(),
    '013B':'No checkout, reset, merge or write to the separate worktree by this task; its branch may progress independently.'}
(audit/'final-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print('Inventoried',len(files),'task files and',len(evidence),'datasets; restored',len(restored),'historical test artifacts.')
