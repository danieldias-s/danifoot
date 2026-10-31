import json,urllib.request,urllib.parse,time
from pathlib import Path
R=Path(__file__).resolve().parents[1];A=R/'artifacts/014c/color-research';A.mkdir(parents=True,exist_ok=True)
b=json.loads((R/'artifacts/014c/coverage-bundle.json').read_text(encoding='utf-8-sig'))
clubs=[(p['scope'],c) for p in b['packages'] if p['scope']['dataType']=='Clubs' for c in p['clubs']]
ids=sorted({c.get('verification',{}).get('wikidataId') for _,c in clubs}-{None});print('Club records:',len(clubs),'reviewed QIDs:',len(ids),flush=True)
root='https://www.wikidata.org/w/api.php?'
all_entities={};failures=[]
for i in range(0,len(ids),40):
 batch=ids[i:i+40];path=A/('entities-'+str(i//40)+'.json')
 if path.exists():data=json.loads(path.read_text())
 else:
  uri=root+urllib.parse.urlencode({'action':'wbgetentities','ids':'|'.join(batch),'props':'claims|info|labels','languages':'en|pt','format':'json','maxlag':5})
  try:
   req=urllib.request.Request(uri,headers={'User-Agent':'ElifootClubColors014C/1.0 (local color research; no images)','Accept':'application/json'})
   with urllib.request.urlopen(req,timeout=25) as response:data=json.load(response)
   assert 'entities' in data,data.get('error');path.write_text(json.dumps(data,ensure_ascii=False),encoding='utf-8')
  except Exception as e:
   failures.append({'batch':i//40,'error':str(e)[:300]});print('Provider unavailable; stopping batch run to honor cooldown.',flush=True);break
  time.sleep(1)
 all_entities.update(data.get('entities',{}));print('Batch',i//40,'entities',len(all_entities),flush=True)
(A/'all-entities.json').write_text(json.dumps(all_entities,ensure_ascii=False),encoding='utf-8')
(A/'fetch-report.json').write_text(json.dumps({'clubs':len(clubs),'Qids':len(ids),'Fetched':len(all_entities),'Failures':failures},indent=2),encoding='utf-8')
