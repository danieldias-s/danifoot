"""Archive only test side effects, then record exact 013C sources/evidence for a local checkpoint."""
from pathlib import Path
import json,subprocess,hashlib
ROOT=Path(__file__).resolve().parents[1];AUDIT=ROOT/'docs/audit-013c'
def git(*args):return subprocess.check_output(['git','-C',str(ROOT),*args])
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
verification=read(AUDIT/'verification.json');assert verification['Status']=='PASS'
assert git('branch','--show-current').decode().strip()=='feature/playable-real-club-worlds-013c'
assert all(verification['ProtectedFilesUnchanged'].values())
assert all(v['StateUnchanged'] for v in verification['OriginalWorktrees'].values())
restore=['docs/audit-007/controlled/behavior-controlled.json','docs/audit-008/browser-fixture.json','docs/audit-009/api-performance-1024.json','docs/audit-009/api-performance-128.json','docs/audit-009/browser-fixture.json','docs/audit-013a/world-creator-390.png','docs/audit-013a/world-creator-1366.png']
changed=git('diff','--name-only').decode().splitlines();restored=[]
for relative in restore:
    if relative not in changed:continue
    target=ROOT/relative;archive=AUDIT/'test-artifact-overwrites'/relative
    archive.parent.mkdir(parents=True,exist_ok=True);current=target.read_bytes();archive.write_bytes(current)
    expected=git('show',':'+relative);target.write_bytes(expected)
    restored.append({'Path':relative,'Archive':str(archive.relative_to(ROOT)),'ArchivedSha256':hashlib.sha256(current).hexdigest(),'RestoredIndexBlobSha256':hashlib.sha256(expected).hexdigest()})
restoration=AUDIT/'artifact-restoration.json'
if restored or not restoration.exists():restoration.write_text(json.dumps(restored,indent=2),encoding='utf-8')
tracked=sorted(set(git('diff','--name-only').decode().splitlines()+git('diff','--cached','--name-only').decode().splitlines()))
for relative in tracked:
    assert relative.startswith(('src/','client/','tests/','tools/','scripts/','docs/audit-013c/')) or Path(relative).name in ['PLAYABLE_WORLDS_013C.md','REAL_AND_SIMULATED_ROSTERS_013C.md','WORLD_DATA_MODES_013C.md','WORLD_CREATION_ACCEPTANCE_013C.md','KNOWN_ISSUES_013C.md'],relative
newcode=[
 'src/Elifoot.Contracts/Catalog/PlayableWorldDtos.cs','src/Elifoot.Domain/Catalog/PlayableWorldPolicy.cs','src/Elifoot.Domain/Catalog/PlayerProvenance.cs',
 'tests/Elifoot.Domain.Tests/PlayableWorld013CTests.cs','tests/Elifoot.Integration.Tests/PlayableWorld013CIntegrationTests.cs','client/elifoot_flutter/test/playable_worlds_013c_test.dart']
newcode += [str(p.relative_to(ROOT)).replace('\\','/') for p in (ROOT/'scripts').glob('*playable-worlds-013c*') if p.suffix in ['.ps1','.py']]
reports=[str(ROOT/'docs'/name) for name in ['PLAYABLE_WORLDS_013C.md','REAL_AND_SIMULATED_ROSTERS_013C.md','WORLD_DATA_MODES_013C.md','WORLD_CREATION_ACCEPTANCE_013C.md','KNOWN_ISSUES_013C.md']]
evidence=list(AUDIT.glob('*.json'))+[p for p in AUDIT.glob('*.log') if p.name!='checkpoint-preparation.log']+list((AUDIT/'tests').glob('final-013c_*.trx'))+list((AUDIT/'tests').glob('boundary-013c_*.trx'))
for relative in ['balance-012d-fix/final-199801','integration-014a/Male','integration-014a/Female']:
    evidence.extend([AUDIT/'verified-final'/relative/'summary.json',AUDIT/'verified-final'/relative/'seasons.json'])
raw=[]
for p in sorted((AUDIT/'verified-final').rglob('*')):
    if p.is_file():raw.append({'Path':str(p.relative_to(ROOT)).replace('\\','/'),'Bytes':p.stat().st_size,'Sha256':sha(p)})
files=sorted(set(tracked+newcode+[str(p.relative_to(ROOT)).replace('\\','/') for p in evidence]+[str(Path(p).relative_to(ROOT)).replace('\\','/') for p in reports]))
files=[p for p in files if p!='docs/audit-013c/final-manifest.json']
assert all((ROOT/p).is_file() for p in files)
sourcefiles=git('ls-files','-co','--exclude-standard','--','src','client','tests','tools','scripts','Elifoot.slnx').decode().splitlines()
sourcefiles=sorted(set(p for p in sourcefiles if (ROOT/p).is_file() and Path(p).suffix in ['.cs','.csproj','.slnx','.dart','.yaml','.lock','.mjs','.ps1','.py'] and '__pycache__' not in p))
source=[{'Path':p,'Sha256':sha(ROOT/p)} for p in sourcefiles]
manifest={'Task':verification['Task'],'Status':'PASS','ApprovedBase':verification['ApprovedBase'],'HeadBeforeCheckpoint':git('rev-parse','HEAD').decode().strip(),
 'SourceFiles':source,'SourcesDigest':hashlib.sha256('\n'.join(f"{p['Path']}:{p['Sha256']}" for p in source).encode()).hexdigest(),
 'CompactEvidence':[{'Path':p,'Bytes':(ROOT/p).stat().st_size,'Sha256':sha(ROOT/p)} for p in files if p.startswith('docs/')],
 'RawSimulationArtifacts':raw,'CommitFiles':files+['docs/audit-013c/final-manifest.json'],'TotalCommitBytes':sum((ROOT/p).stat().st_size for p in files),
 'OriginalWorktrees':verification['OriginalWorktrees'],'ProtectedFilesUnchanged':verification['ProtectedFilesUnchanged'],
 'NoProductionPublish':True,'NoPush':True,'NoAutomaticMerge':True,
 'SimulationSourcesUnchangedSinceBuild':all(p.stat().st_mtime <= (AUDIT/'full-build.log').stat().st_mtime for p in list((ROOT/'tests/Elifoot.Simulation.Tests').glob('*.cs'))+[ROOT/'tests/Shared/Imported014AWorlds.cs'])}
assert manifest['SimulationSourcesUnchangedSinceBuild']
(AUDIT/'final-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS:',len(source),'sources;',len(files),'checkpoint paths;',len(raw),'raw artifacts hashed;',manifest['TotalCommitBytes'],'bytes selected.')
