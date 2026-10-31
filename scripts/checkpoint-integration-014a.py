from pathlib import Path
import subprocess,json,hashlib,shutil
ROOT=Path(__file__).resolve().parents[1]; AUDIT=ROOT/'docs/audit-014a'
def git(*args):return subprocess.check_output(['git','-C',str(ROOT),*args])
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
assert git('branch','--show-current').decode().strip()=='feature/integration-012d-013ab'
verification=read(AUDIT/'verification.json');assert verification['Status']=='PASS'
assert all(item['StateUnchanged'] for item in verification['OriginalWorktrees'].values())
assert all(verification['ProtectedFilesUnchanged'].values())
allowed=['docs/audit-007/controlled/behavior-controlled.json','docs/audit-008/browser-fixture.json','docs/audit-009/api-performance-1024.json','docs/audit-009/api-performance-128.json','docs/audit-009/browser-fixture.json']
record=AUDIT/'artifact-restoration.json';restored=read(record) if record.exists() else []
modified=git('diff','--name-only').decode().splitlines()
for relative in allowed:
    if relative not in modified:continue
    target=ROOT/relative; expected=git('show',':'+relative); current=target.read_bytes()
    archive=AUDIT/'test-artifact-overwrites'/relative;archive.parent.mkdir(parents=True,exist_ok=True);archive.write_bytes(current)
    target.write_bytes(expected)
    entry={'Path':relative,'Archive':str(archive.relative_to(ROOT)),'ArchivedSha256':hashlib.sha256(current).hexdigest(),'RestoredIndexBlobSha256':hashlib.sha256(expected).hexdigest()}
    if entry not in restored:restored.append(entry)
record.write_text(json.dumps(restored,indent=2),encoding='utf-8')
for relative in git('diff','--name-only').decode().splitlines():
    if relative.startswith('docs/') and not relative.startswith(('docs/INTEGRATION_','docs/audit-014a/')):
        # Git may report a stat refresh after writing normalized LF bytes. Actual patches must be empty.
        assert not git('diff','--numstat','--',relative).strip(),relative
source_paths=sorted(set(git('ls-files','-co','--exclude-standard','--','src','client','tests','tools','scripts','Elifoot.slnx').decode().splitlines()))
source_paths=[p for p in source_paths if (ROOT/p).is_file() and Path(p).suffix in {'.cs','.csproj','.slnx','.dart','.yaml','.lock','.mjs','.ps1','.py','.json','.csv','.md'} and '__pycache__' not in p]
code=[{'Path':p,'Sha256':sha(ROOT/p)} for p in source_paths]
compact=['source-state.json','verification.json','financial-reconciliation.json','financial-reconciliation.log','economic-review.json','initial-economy.json','initial-economy.log','api-smoke.json','api-smoke-verified.log','api-restart.json','restart-verification.json','restart-verified.log','snapshots-before-restart.json','snapshots-after-restart.json','old-worlds.json','old-worlds-restart.json','old-worlds-verified.log','real-bundle.json','real-import-verified.log','real-catalog-audit.log','build-final.log','build-release-final.log','regression-final.log','dotnet-final-exit.json','compatibility-final.log','compatibility-final-exit.json','compatibility-consolidated.json','compatibility-input.json','compatibility-reload.log','compatibility-reload-exit.json','flutter-analyze.log','flutter-test.log','flutter-build-web.log','flutter-exits.json','node-tests.log','artifact-restoration.json','services-stop.json','services-stop.log']
evidence=[AUDIT/p for p in compact if (AUDIT/p).is_file()]
evidence += list((AUDIT/'tests').glob('final_*.trx'))+list((AUDIT/'tests').glob('compatibility-final_*.trx'))+list((AUDIT/'tests').glob('compatibility-reload_*.trx'))
evidence += [AUDIT/p for p in ['real-catalog/coverage.json','real-catalog/fallback-postgres.json','verified-final/classic-force-012c/replay.json','verified-final/classic-force-012c/100-seasons.json'] if (AUDIT/p).is_file()]
raw=[]
recon=read(AUDIT/'financial-reconciliation.json')
for case in recon['Cases'].values():
    folder=ROOT/case['Directory'];evidence += [folder/'summary.json',folder/'seasons.json']
    raw.append({'Directory':case['Directory'],'Inputs':case['InputSha256'],'GzipLedgersAndClubSeriesRetainedLocally':True})
if (AUDIT/'checkpoint.json').exists():evidence.append(AUDIT/'checkpoint.json')
reports=list((ROOT/'docs').glob('INTEGRATION_*014A.md'))
commit_files=sorted(set(str(p.relative_to(ROOT)).replace('\\','/') for p in evidence+reports))
manifest={'Task':verification['Task'],'Status':'PASS','HeadBeforeCheckpoint':git('rev-parse','HEAD').decode().strip(),
    'SourceFiles':code,'SourcesDigest':hashlib.sha256('\n'.join(f"{x['Path']}:{x['Sha256']}" for x in code).encode()).hexdigest(),
    'CompactEvidence':[{'Path':p,'Sha256':sha(ROOT/p)} for p in commit_files],'RawSimulationInputs':raw,
    'ProtectedFilesUnchanged':verification['ProtectedFilesUnchanged'],'OriginalWorktrees':verification['OriginalWorktrees'],
    'CommitFiles':commit_files+['docs/audit-014a/final-manifest.json'],
    'LongSimulationSourcesUnchangedSinceFullBuild':all(p.stat().st_mtime<=(AUDIT/'build-final.log').stat().st_mtime for p in list((ROOT/'tests/Elifoot.Simulation.Tests').glob('*.cs'))+[ROOT/'tests/Shared/Imported014AWorlds.cs']),
    'NoProductionPublish':True,'OriginalFixturesRestoredFromIntegrationIndexOnly':True}
assert manifest['LongSimulationSourcesUnchangedSinceFullBuild']
(AUDIT/'final-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS:',len(code),'source files;',len(commit_files),'compact evidence/report files;',len(raw),'100-season datasets; original fixtures archived and restored.')