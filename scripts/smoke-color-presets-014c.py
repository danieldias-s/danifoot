"""Actual private pre-save palette persistence; no global catalog mutation."""
from pathlib import Path
import runpy,json,copy,sys,uuid
ROOT=Path(__file__).resolve().parents[1];m=runpy.run_path(str(ROOT/'scripts/smoke-club-colors-014c.py'))
call=m['call'];write=m['write'];fingerprint=m['fingerprint'];exported=m['exported'];LOCAL=m['LOCAL'];AUDIT=m['AUDIT']
def run():
 contexts=[]
 for gender in ['Male','Female']:
  owner=m['login']('014C montagem '+gender);base='/api/world-creator/assemblies'
  a=call(base,{'name':'Cores privadas '+gender,'gender':gender,'countries':['BR','PT'],'seed':140014,'allowPersonalizedContent':True},owner)
  assert a['validation']['isValid'];package=copy.deepcopy(a['package']);club=package['clubs'][0]
  club.update(name='Fictício Cores '+gender,primaryColorHex='#0000FF' if gender=='Male' else '#FFFF00',secondaryColorHex='#FFFFFF' if gender=='Male' else '#000000')
  path=base+'/'+a['id'];a=call(path,{'name':a['name'],'countries':a['countries'],'package':package,'expectedVersion':a['version'],'savePreset':True,'allowPersonalizedContent':True},owner)
  assert a['isSaved'] and a['validation']['isValid'];palette=a['package']['clubs'][0]['colorPalette'];assert palette['isPersonalized'] and not palette['colorVerified']
  one=call(path+'/confirm',{'idempotencyKey':'colors-one'},owner)['id'];two=call(path+'/confirm',{'idempotencyKey':'colors-two'},owner)['id']
  p1,p2=exported(one),exported(two);c1=next(c for c in p1['clubs'] if c['name']==club['name']);c2=next(c for c in p2['clubs'] if c['name']==club['name'])
  assert c1['id']!=c2['id'];assert c1['primaryColorHex']==club['primaryColorHex'] and c1['colorPalette']==palette and c2['colorPalette']==palette
  assert len(p1['players'])==2304 and sum(c['cashBalance'] for c in p1['clubs'])==138800000
  before1,before2=fingerprint(one),fingerprint(two)
  changed=copy.deepcopy(a['package']);changed['clubs'][0]['primaryColorHex']='#800080'
  call(path,{'name':a['name'],'countries':a['countries'],'package':changed,'expectedVersion':a['version'],'savePreset':True,'allowPersonalizedContent':True},owner)
  assert fingerprint(one)==before1 and fingerprint(two)==before2
  contexts.append({'Owner':owner,'Assembly':a['id'],'Worlds':[one,two],'Fingerprints':[before1,before2],'Gender':gender,'Palette':palette})
  write(LOCAL/'color-presets-context.json',contexts)
  print('PASS '+gender+': private fictional club colors, saved preset, independent worlds and unchanged old snapshots',flush=True)
 write(AUDIT/'color-presets-http.json',{'Status':'PASS','Cases':[{k:v for k,v in x.items() if k not in ['Owner']} for x in contexts]})
def verify():
 contexts=json.loads((LOCAL/'color-presets-context.json').read_text(encoding='utf-8'))
 for x in contexts:
  a=call('/api/world-creator/assemblies/'+x['Assembly'],owner=x['Owner']);assert a['isSaved'] and a['package']['clubs'][0]['primaryColorHex']=='#800080'
  for w,h in zip(x['Worlds'],x['Fingerprints']):assert fingerprint(w)==h
 write(AUDIT/'color-presets-restart.json',{'Status':'PASS','SavedPresets':2,'IndependentWorldsUnchanged':4})
 print('PASS presets and independent snapshots after API/PostgreSQL restart',flush=True)
if __name__=='__main__':verify() if '--verify-restart' in sys.argv else run()
