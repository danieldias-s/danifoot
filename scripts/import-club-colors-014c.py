"""Validate authorized CSV -> identity-keyed JSON for the restricted 014c-colors importer.
No network, images, database or gameplay writes. A source/evidence is mandatory.
"""
import csv,json,re,argparse,datetime
from pathlib import Path

def convert(source):
 rows=[];seen=set()
 with Path(source).open(encoding='utf-8-sig',newline='') as f:
  for line,r in enumerate(csv.DictReader(f),2):
   required=['countryCode','gender','provider','externalId','primaryColor','colorSource','evidence','version','lastColorUpdate','colorConfidence','colorVerified']
   if any(not r.get(k,'').strip() for k in required):raise ValueError(f'Line {line}: missing identity, RGB or provenance')
   r={k:v.strip() for k,v in r.items()}
   if r['gender'] not in ['Male','Female'] or not re.fullmatch('[A-Z]{2}',r['countryCode']):raise ValueError(f'Line {line}: invalid country/gender')
   for k in ['primaryColor','secondaryColor','preferredTextColor']:
    if r.get(k) and not re.fullmatch('#[0-9a-fA-F]{6}',r[k]):raise ValueError(f'Line {line}: invalid RGB')
   confidence=float(r['colorConfidence'])
   if not 0<=confidence<=1 or r['colorVerified'].lower() not in ['true','false']:raise ValueError(f'Line {line}: invalid confidence/verification')
   date=datetime.datetime.fromisoformat(r['lastColorUpdate'].replace('Z','+00:00'))
   if date.tzinfo is None:raise ValueError(f'Line {line}: source timestamp needs timezone')
   identity={k:r[k] for k in ['countryCode','gender','provider','externalId']};key=tuple(identity.values())
   if key in seen:raise ValueError(f'Line {line}: duplicated scoped identity')
   seen.add(key)
   palette={**identity,**{k:r.get(k) or None for k in ['primaryColor','secondaryColor','preferredTextColor','colorSource','evidence','version','lastColorUpdate']},'colorConfidence':confidence,'colorVerified':r['colorVerified'].lower()=='true','precision':r.get('precision') or 'TraditionalRepresentative'}
   rows.append({**identity,'palette':palette})
 return rows
if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('csv');parser.add_argument('json');a=parser.parse_args()
 Path(a.json).write_text(json.dumps(convert(a.csv),ensure_ascii=False,indent=2),encoding='utf-8')
 print('Validated CSV; import requires reviewed identity aliases and the isolated 014C database.')
