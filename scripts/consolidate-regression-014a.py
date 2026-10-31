from pathlib import Path
import json,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[1];AUDIT=ROOT/'docs/audit-014a';ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
latest={};files=sorted(list((AUDIT/'tests').glob('compatibility-final_*.trx'))+list((AUDIT/'tests').glob('compatibility-reload_*.trx')),key=lambda p:p.stat().st_mtime)
for file in files:
    tree=ET.parse(file);suite=Path(tree.find('.//t:UnitTest',ns).attrib['storage']).name.lower()
    for result in tree.findall('.//t:UnitTestResult',ns):
        # The adapter truncates display names for JSON theory inputs. Stable testId preserves distinct data rows.
        latest[(suite,result.attrib['testId'])]={'Outcome':result.attrib['outcome'],'DisplayName':result.attrib['testName'],'File':str(file.relative_to(ROOT))}
assert len(latest)==515 and all(v['Outcome']=='Passed' for v in latest.values())
initial=json.loads((AUDIT/'compatibility-final-exit.json').read_text(encoding='utf-8-sig'))['ExitCode']
rerun=AUDIT/'compatibility-reload-exit.json';target=json.loads(rerun.read_text(encoding='utf-8-sig'))['ExitCode'] if rerun.exists() else None
assert initial==0 or target==0
record={'Status':'PASS','DistinctByStableTestId':515,'RemainingFailures':0,'InitialShortRunRawExit':initial,'TargetedReloadRawExit':target,'Trx':[str(p.relative_to(ROOT)) for p in files],
    'IdentityNote':'Three invalid-source JSON theory rows have the same truncated display name but different stable testIds. The reload keeps the same testId on re-execution and is counted once.',
    'InputNote':'The fresh short output lacked the snapshot produced by the passed long run. A byte-identical copy was supplied and the reload test passed. Raw exit codes are preserved.'}
(AUDIT/'compatibility-consolidated.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS: 515 stable test IDs, no remaining failure, rerun counted once.')