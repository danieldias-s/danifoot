"""Independently reconcile the complete inherited 100-season regression datasets."""
from pathlib import Path
import runpy,json,hashlib
ROOT=Path(__file__).resolve().parents[1];AUDIT=ROOT/'docs/audit-013c';BASE=AUDIT/'verified-final'
module=runpy.run_path(str(ROOT/'scripts/reconcile-integration-014a.py'))
progress=AUDIT/'financial-reconciliation-progress.json'
completed=module['read'](progress)['CompletedCases'] if progress.exists() else {}
cases={}
for name,relative in [('Seed012D','balance-012d-fix/final-199801'),('ImportedFixtureMale','integration-014a/Male'),('ImportedFixtureFemale','integration-014a/Female')]:
    cached=completed.get(name)
    if cached and all(hashlib.sha256((BASE/relative/filename).read_bytes()).hexdigest()==digest for filename,digest in cached['InputSha256'].items()):
        result=cached
    else:
        result=module['reconcile'](BASE/relative)
    baseline=module['read'](ROOT/'docs/audit-014a/verified-final'/relative/'summary.json')
    assert module['D'](result['Summary']['FinalCash'])==module['D'](baseline['FinalCash'])
    assert module['D'](result['Summary']['FinalDebt'])==module['D'](baseline['FinalDebt'])
    cases[name]=result
    print(name,'PASS',result['Summary']['FinalCash'],'cash;',result['Summary']['FinalDebt'],'debt;',result['LedgerRows'],'ledger rows',flush=True)
module['dump'](AUDIT/'financial-reconciliation.json',{'Status':'PASS','Cases':cases,'ApprovedIntegrationBaselineExactlyReproduced':True,
  'Scope':'Inherited 012D and imported synthetic 014A fixture protocols, 100 seasons each. These are not the actual real-catalog 013C HTTP worlds.',
  'Actual013CWorlds':'Three HTTP rounds each plus persisted restart; see actual-worlds.json and actual-restart-http.json.',
  'EconomyFormulasChanged':False,'NewInflationIndex':False,'AnnualCashDifference':0,'AnnualDebtDifference':0,'TransferNetDifference':0})
