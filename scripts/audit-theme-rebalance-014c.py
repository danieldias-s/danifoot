from pathlib import Path
import json, hashlib, subprocess, datetime
ROOT = Path(__file__).resolve().parents[1]
assert str(ROOT).lower() == r'd:\elifoot_98_014c'
OUT = ROOT / 'docs/audit-014c-rebalance'
def digest(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for chunk in iter(lambda:f.read(1024*1024), b''):h.update(chunk)
    return h.hexdigest()
def write(name, obj):
    (OUT/name).write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding='utf-8')

def inventory():
    refs=[]
    for label, file in [('Elifoot 98', r'C:/Users/Daniel/AppData/Local/Temp/codex-clipboard-b70663ca-5b3b-495e-a6d5-c7f3cbc897fe.png'), ('Liga Clássica 98', r'C:/Users/Daniel/AppData/Local/Temp/codex-clipboard-466a0e22-66de-4de6-893e-6de2b5c7ccaa.png')]:
        p=Path(file)
        if p.exists():refs.append({'Label':label, 'Source':file, 'Sha256':digest(p)})
    renders=[]
    for p in sorted((OUT/'automated-renders').glob('*.png')):
        renders.append({'File':str(p.relative_to(ROOT)).replace('\\','/'), 'Bytes':p.stat().st_size, 'Sha256':digest(p),
                        'Width':int.from_bytes(p.read_bytes()[16:20], 'big'), 'Height':int.from_bytes(p.read_bytes()[20:24], 'big')})
    scenes=[]
    for folder in [ROOT/'artifacts/014c', ROOT/'artifacts/014c-rebalance/scenes']:
        for p in sorted(folder.glob('scene-*.json' if folder.name=='014c' else '*.json')):
            scenes.append({'File':str(p.relative_to(ROOT)).replace('\\','/'), 'Bytes':p.stat().st_size, 'Sha256':digest(p)})
    write('visual-evidence-manifest.json', {'Method':'Flutter renderer of recorded HTTP snapshots; auxiliary screens use explicit SceneApi test adapters; legacy Club preferences render as fixed green.',
          'BrowserScreenshots':len(list((OUT/'browser-captures').glob('*.jpg'))), 'BrowserBlocker':None,
          'BrowserCaptures':[json.loads(p.read_text(encoding='utf-8')) for p in sorted((OUT/'browser-captures').glob('*.jpg.json'))],
          'References':refs, 'Renders':renders, 'LocalRecordedScenes':scenes})
    checks=[]
    for path, expected in [(r'D:\elifoot_98','7a00bdd2dcf2f721ac32d14b2d195e3ea2aebf48'), (r'D:\elifoot_98_013A','47608e9a674c0978c4be4b244de88d961876e197'), (r'D:\elifoot_98_integration','3f12999e16ec0452b4d8889b20f4be18a4ca3f01'), (r'D:\elifoot_98_014B','c0ff28ec510553385438f82dc652c23126bd7689')]:
        head=subprocess.check_output(['git','-C',path,'rev-parse','HEAD'],text=True).strip()
        status=subprocess.check_output(['git','-C',path,'status','--porcelain','--untracked-files=no'],text=True).strip()
        assert head==expected and not status, (path,head,status)
        checks.append({'Worktree':path, 'Head':head,'TrackedChanges':False})
    baseline='9690d9578aba2ec6a35b344921d9429478bd54ab'
    diff=subprocess.check_output(['git','diff','--name-only',baseline,'--','src','client/elifoot_flutter/lib'],text=True).splitlines()
    allowed={'src/Elifoot.Contracts/Catalog/ClubColorDtos.cs','src/Elifoot.Domain/Catalog/ClubColorPolicy.cs','client/elifoot_flutter/lib/appearance_settings.dart','client/elifoot_flutter/lib/club_theme.dart','client/elifoot_flutter/lib/main.dart','client/elifoot_flutter/lib/shell_actions.dart'}
    assert set(diff)<=allowed, diff
    protected=subprocess.check_output(['git','diff',baseline,'--','src/Elifoot.Domain/Engines','src/Elifoot.Application','src/Elifoot.Infrastructure','client/elifoot_flutter/lib/auction_config.dart'])
    assert not protected
    write('scope-preservation.json', {'Base':baseline, 'OtherWorktrees':checks,'ChangedProductionFiles':diff,
          'GameplayAndEconomySourceUnchanged':True, 'AuctionConfigSourceUnchanged':True, 'NoProductionDeployment':True})
    print(f'PASS: {len(renders)} renders inventoried; four other worktrees unchanged; production changes confined to appearance.')

if __name__=='__main__':inventory()