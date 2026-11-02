"""Consolidate real local test executions; individual test timestamps resolve reruns."""
from pathlib import Path
from collections import defaultdict
import json, xml.etree.ElementTree as ET
root=Path(__file__).resolve().parent.parent
area=root/'artifacts/016b/world-catalog'
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
latest={};runs=[]
for path in sorted(area.rglob('*.trx')):
 tree=ET.parse(path).getroot()
 counts=tree.find('.//t:Counters',ns)
 runs.append({'path':str(path.relative_to(area)),'counts':counts.attrib if counts is not None else {}})
 definitions={d.attrib['id']:d.attrib.get('storage','') for d in tree.findall('.//t:UnitTest',ns)}
 for item in tree.findall('.//t:UnitTestResult',ns):
  name=item.attrib['testName'];storage=definitions.get(item.attrib['testId'],'')
  group='Simulation' if 'simulation' in storage.lower() else 'Integration' if 'integration' in storage.lower() else 'Domain' if 'domain' in storage.lower() else 'Application'
  stamp=item.attrib.get('endTime','')
  key=(group,item.attrib['testId'])
  if key not in latest or stamp>latest[key]['at']:latest[key]={'at':stamp,'outcome':item.attrib['outcome'],'run':str(path.relative_to(area))}
counts=defaultdict(lambda:defaultdict(int))
for (group,name),value in latest.items():counts[group][value['outcome']]+=1
read=lambda name:json.loads((area/name).read_text(encoding='utf-8-sig'))
report={'task':'ELIFOOT_WORLD_CATALOG_AND_LOBBY_016B','status':'PARTIAL','base':'00b68c5','branch':'feature/world-catalog-lobby-016b','dotnetLatestByCase':dict(counts),'runs':runs,'flutter':{'analyzerIssues':0,'tests':215,'passed':215,'buildWeb':'PASS'},'node':{'tests':2,'passed':2},'realProvider':{'authenticated':False,'apiKeyPresent':False,'remoteQuotaVerified':False,'realRequests':0},'browser':{'extensionDetected':True,'pageControl':'BLOCKED: Local browser use is disabled in Work settings','actualScreenshots':0},'liveHttpSignalR':read('live-lobby-evidence.json')['status'],'postgresRestart':read('restart-evidence.json'),'originalSaves':read('original-saves-evidence.json'),'manualAccess':read('manual-access.json'),'manualTestApproved':False,'simulations100Seasons':{gender:read(f'new-simulations/world-catalog-016b/{gender}/summary.json') for gender in ['Male','Female']}}
(area/'validation-summary.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'status':report['status'],'dotnetLatestByCase':report['dotnetLatestByCase'],'flutter':report['flutter'],'node':report['node'],'manualTestApproved':False},ensure_ascii=False))
