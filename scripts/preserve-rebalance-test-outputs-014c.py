"""Preserve generated regression outputs, then restore historical tracked evidence.
Run only after the 014C simulation test process exits. No recursive deletion.
"""
from pathlib import Path
import subprocess, json, hashlib, shutil, sys
ROOT=Path(__file__).resolve().parents[1]
assert str(ROOT).lower()==r'd:\elifoot_98_014c'
OUT=ROOT/'docs/audit-014c-rebalance'
ARCHIVE=ROOT/'artifacts/014c-rebalance/recovered-test-outputs'
TRACKED=['docs/COACH_CAREER_50_SEASONS_008.json','docs/audit-007/controlled/behavior-controlled.json','docs/audit-008/browser-fixture.json',
'docs/audit-009/QUERY_LOAD_1024_009.json','docs/audit-009/QUERY_LOAD_128_009.json','docs/audit-009/REAL_SEASON_009.json','docs/audit-009/api-performance-1024.json','docs/audit-009/api-performance-128.json','docs/audit-009/browser-fixture.json','docs/audit-009/real-season-world.json',
'docs/audit-014c/verified-final/api-performance-1024.json','docs/audit-014c/verified-final/api-performance-128.json','docs/audit-014c/verified-final/browser-fixture.json']
GENERATED=['docs/audit-012b/force-012b','docs/audit-014c/verified-final','docs/balance-012d-fix','docs/classic-force-012c','docs/force-012','docs/integration-014b']
def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for chunk in iter(lambda:f.read(1024*1024),b''):h.update(chunk)
    return h.hexdigest()
def safe(path, parent):
    path=path.resolve(); path.relative_to(parent.resolve()); assert path!=parent.resolve(); return path

def run():
    trx=OUT/'test-results/simulation-final.trx'
    assert trx.exists(), 'Simulation TRX must exist before archiving outputs.'
    import xml.etree.ElementTree as ET
    tree=ET.parse(trx); ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    counters=tree.find('.//t:Counters',ns)
    assert counters is not None and counters.attrib['failed']=='0', 'Inspect failed simulation before cleanup.'
    records=[]
    for rel in TRACKED:
        p=safe(ROOT/rel,ROOT)
        if not p.exists():continue
        base=subprocess.check_output(['git','show','HEAD:'+rel],cwd=ROOT)
        if p.read_bytes()==base:continue
        dest=safe(ARCHIVE/'tracked'/rel, ARCHIVE); assert not dest.exists(),dest
        dest.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(p,dest)
        records.append({'File':rel,'Archive':str(dest.relative_to(ROOT)).replace('\\','/'),'Sha256':sha(dest),'Bytes':dest.stat().st_size,'Action':'Preserved generated output; restored historical HEAD bytes'})
        p.write_bytes(base)
    names=subprocess.check_output(['git','ls-files','--others','--exclude-standard','-z','--',*GENERATED],cwd=ROOT).decode('utf-8').split('\0')
    for rel in filter(None,names):
        p=safe(ROOT/rel,ROOT); dest=safe(ARCHIVE/'untracked'/rel,ARCHIVE)
        assert not dest.exists(),dest
        dest.parent.mkdir(parents=True,exist_ok=True)
        size=p.stat().st_size; checksum=sha(p); p.rename(dest)
        records.append({'File':rel,'Archive':str(dest.relative_to(ROOT)).replace('\\','/'),'Sha256':checksum,'Bytes':size,'Action':'Moved generated output; no deletion'})
    raw=OUT/'simulation-verified'
    if raw.exists():
        dest=safe(ROOT/'artifacts/014c-rebalance/simulation-verified-original',ROOT/'artifacts')
        assert not dest.exists(),dest
        files=[]
        for p in sorted(raw.rglob('*')):
            if p.is_file():files.append({'File':str(p.relative_to(raw)).replace('\\','/'),'Bytes':p.stat().st_size,'Sha256':sha(p)})
        # Keep concise original summaries plus a manifest; raw ledgers stay local.
        summaries=OUT/'simulation-results'
        summaries.mkdir(exist_ok=True)
        for p in raw.rglob('summary.json'):
            if p.stat().st_size<20000:
                sub=summaries/p.relative_to(raw); sub.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(p,sub)
        raw.rename(dest)
        (OUT/'simulation-raw-manifest.json').write_text(json.dumps({'Archive':str(dest.relative_to(ROOT)).replace('\\','/'),'Files':files},ensure_ascii=False,indent=2),encoding='utf-8')
    for rel in TRACKED:
        assert (ROOT/rel).read_bytes()==subprocess.check_output(['git','show','HEAD:'+rel],cwd=ROOT),rel
    assert not subprocess.check_output(['git','diff','--','docs/audit-014c'],cwd=ROOT)
    (OUT/'historical-evidence-preservation.json').write_text(json.dumps({'Status':'PASS','Original014CEvidenceUnchanged':True,'NoFilesDeleted':True,'Records':records},ensure_ascii=False,indent=2),encoding='utf-8')
    print(f'PASS: historical evidence restored; {len(records)} generated outputs preserved in artifacts; no deletion.')
if __name__=='__main__':run()