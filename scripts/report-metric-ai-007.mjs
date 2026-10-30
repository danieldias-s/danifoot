import fs from 'node:fs';
import path from 'node:path';
import readline from 'node:readline';
import zlib from 'node:zlib';
import assert from 'node:assert/strict';

const root = path.resolve(import.meta.dirname, '..');
const docs = path.join(root, 'docs');
const audit = path.join(docs, 'audit-007');
const candidateRoot = process.env.ELIFOOT_007_CANDIDATE ?? path.join(audit, 'final-run');
const read = file => JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
const write = (file, data) => fs.writeFileSync(path.join(docs, file), JSON.stringify(data, null, 2) + '\n');
const cents = value => Math.round(value * 100);
const money = value => value / 100;
const countBy = (rows, key) => rows.reduce((d,r) => { const k=key(r); d[k]=(d[k]??0)+1; return d; }, {});
async function* lines(file) {
  const reader=readline.createInterface({input:fs.createReadStream(file).pipe(zlib.createGunzip()),crlfDelay:Infinity});
  for await(const line of reader) if(line) yield JSON.parse(line);
}
const scope = {seed:199801,seasons:[1,100],clubs:128,aiManagers:128,leagueClubs:32,districtClubs:96,
  matches:35100,rounds:2200,engine:'MatchSimulationEngine.SimulateFullMatchReal; minute by minute',aggregateScores:false};
const definitions = {
  AiPurchaseEvaluations:'Candidate visits in the ranked shortlist, before seller-removal validation. Not every player scanned by the query.',
  AiPurchaseAttempts:'ExecuteTransfer calls after identity/idempotency checks, plus new formal offer submissions. A submission and its later execution are separate stages; bids are excluded.',
  AiOffersSubmitted:'Unique TransferOffer.Id attributed to SubmittedByManagerId at submission.',
  AiOffersAccepted:'Completed TransferPurchase with TransactionType AcceptedOffer, attributed to execution-time ManagerId.',
  AiPurchasesCompleted:'TransferPurchase + AuctionPurchase, counted once by purchase-row Id. Free assignments and renewals excluded.',
  AiAuctionBids:'Actual bid increases recorded as AiAuctionBid, including losing bids.',
  AiAuctionsWon:'Successfully settled AuctionPurchase; not bids or unsold auctions.',
  AiSalesCompleted:'TransferSale attributed to the seller manager at execution.',
  AiContractRenewals:'ContractRenewal ledger entries attributed to execution-time ManagerId.'
};

async function collect(directory, isBaseline) {
  const summary=read(path.join(directory,'summary.json'));
  const population=read(path.join(directory,'population.json'));
  const managers=new Map(population.AiManagers.map(m=>[m.Id,m]));
  const perManager=new Map(population.AiManagers.map(m=>[m.Id,{ManagerId:m.Id,Name:m.Name,Identity:m.Identity,
    ...Object.fromEntries(Object.keys(definitions).map(k=>[k,0])),BilateralPurchases:0,AuctionPurchases:0}]));
  const purchases=[], sales=[], seen=new Set(), keys=new Set(), amounts={}, categoryCounts={}, pairs=new Map();
  const minimumCash={}, negativeEntries={}, reasons={}, quality={count:0,belowStarter:0,priceAbove125PercentFair:0,missing:0,maxPriceToFair:0};
  let duplicateIds=0, duplicateKeys=0;
  for await(const record of lines(path.join(directory,'ledger.jsonl.gz'))) {
    const t=record.Transaction;
    if(seen.has(t.Id)) duplicateIds++; seen.add(t.Id);
    const key=t.ClubId+':'+t.IdempotencyKey;
    if(keys.has(key)) duplicateKeys++; keys.add(key);
    amounts[t.Category]=(amounts[t.Category]??0)+cents(t.Amount);
    categoryCounts[t.Category]=(categoryCounts[t.Category]??0)+1;
    minimumCash[t.ClubId]=Math.min(minimumCash[t.ClubId]??Infinity,t.BalanceAfter);
    if(t.BalanceAfter<0) negativeEntries[t.ClubId]=(negativeEntries[t.ClubId]??0)+1;
    const managerId=t.ManagerId ?? (isBaseline?record.ObservedManagerId:null);
    const mgr=perManager.get(managerId);
    if(['TransferPurchase','AuctionPurchase'].includes(t.Category)) {
      purchases.push({...t,AttributedManagerId:managerId,AttributionSource:isBaseline?'observed each round before rotation':'persisted transaction ManagerId'});
      if(mgr) {mgr.AiPurchasesCompleted++;mgr[t.Category==='AuctionPurchase'?'AuctionPurchases':'BilateralPurchases']++;}
      if(t.Category==='AuctionPurchase' && mgr) mgr.AiAuctionsWon++;
      if(t.TransactionType==='AcceptedOffer' && mgr) mgr.AiOffersAccepted++;
      const d=t.SigningDecision;
      if(!isBaseline) {
        if(!d) quality.missing++;
        else { quality.count++; reasons[d.Reason]=(reasons[d.Reason]??0)+1;
          if(d.StarterStrength!=null && d.Strength<d.StarterStrength) quality.belowStarter++;
          const ratio=d.Amount/d.MarketValue;quality.maxPriceToFair=Math.max(quality.maxPriceToFair,ratio);
          if(ratio>1.25) quality.priceAbove125PercentFair++;
        }
      }
    }
    if(t.Category==='TransferSale') {sales.push(t);if(mgr)mgr.AiSalesCompleted++;}
    if(t.Category==='ContractRenewal' && mgr) mgr.AiContractRenewals++;
    if(['TransferPurchase','TransferSale'].includes(t.Category)) {
      const pair=t.TransferTransactionId??t.IdempotencyKey.replace(/:(buy|sell)$/,'');
      const a=pairs.get(pair)??[];a.push(t);pairs.set(pair,a);
    }
  }
  const pairFailures=[...pairs.values()].filter(p=>p.length!==2 || p.reduce((s,t)=>s+cents(t.Amount),0)!==0
    || p.filter(t=>t.Category==='TransferPurchase').length!==1).length;
  if(!isBaseline) {
    const offers=new Map();
    for await(const event of lines(path.join(directory,'actions.jsonl.gz'))) {
      if(event.Kind==='offer') offers.set(event.Offer.Id,event.Offer);
      else {
        const a=event.Action,m=perManager.get(a.ActorCoachId);if(!m)continue;
        const k={AiPurchaseEvaluation:'AiPurchaseEvaluations',AiPurchaseAttempt:'AiPurchaseAttempts',AiAuctionBid:'AiAuctionBids'}[a.ActionType];
        if(k)m[k]++;
      }
    }
    for(const o of offers.values()) {const m=perManager.get(o.SubmittedByManagerId);if(m)m.AiOffersSubmitted++;}
  }
  const managerTotals=Object.fromEntries(Object.keys(definitions).map(k=>[k,[...perManager.values()].reduce((s,m)=>s+m[k],0)]));
  const byProfile=[...new Set([...managers.values()].map(m=>m.Identity))].map(identity=>{
    const group=[...perManager.values()].filter(m=>m.Identity===identity);
    const total=group.reduce((s,m)=>s+m.AiPurchasesCompleted,0);
    return {Identity:identity,Managers:group.length,PurchasesCompleted:total,
      BilateralPurchases:group.reduce((s,m)=>s+m.BilateralPurchases,0),AuctionPurchases:group.reduce((s,m)=>s+m.AuctionPurchases,0),
      MeanCompletedPerManager:total/group.length};
  });
  assert.equal(duplicateIds,0);assert.equal(duplicateKeys,0);assert.equal(pairFailures,0);
  assert.equal(managerTotals.AiPurchasesCompleted,purchases.length);
  assert.equal((amounts.TransferPurchase??0)+(amounts.TransferSale??0),0);
  assert.equal(Object.values(amounts).reduce((s,n)=>s+n,0),cents(summary.FinalCash));
  return {summary,population,purchases,perManager:[...perManager.values()],managerTotals,byProfile,
    categoryCounts,categoryAmounts:Object.fromEntries(Object.entries(amounts).map(([k,v])=>[k,money(v)])),
    reconciliation:{DuplicateTransactionIds:duplicateIds,DuplicateClubIdempotencyKeys:duplicateKeys,
      UnpairedOrUnbalancedBilateralTransfers:pairFailures,CompletedPurchases:purchases.length,
      ManagerPurchases:managerTotals.AiPurchasesCompleted,Delta:managerTotals.AiPurchasesCompleted-purchases.length},quality,reasons,minimumCash,negativeEntries};
}

async function recoveries(directory, data) {
  const ids=new Set(data.summary.ClubsEverInCrisis);
  const states=new Map([...ids].map(id=>[id,{ClubId:id,Name:data.population.Clubs.find(c=>c.Id===id).Name,
    Episodes:[],Current:null,MinCashAfterFirstRecovery:null,FinalCash:null,ObservedSamplesAfterFirstRecovery:0,
    MinLedgerCash:data.minimumCash[id],NegativeLedgerEntries:data.negativeEntries[id]??0}]));
  const official=new Set(),officialRecovered=new Set();
  for await(const r of lines(path.join(directory,'club-rounds.jsonl.gz'))) {
    if(r.Stage==='round' && r.DivisionTier!=='Distrital') {
      if(r.Cash<150000)official.add(r.ClubId);
      if(r.Cash>=350000 && official.has(r.ClubId))officialRecovered.add(r.ClubId);
    }
    const s=states.get(r.ClubId);if(!s)continue;
    s.FinalCash=r.Cash;s.FinalRoundOrdinal=(r.Season-1)*22+r.Round;
    s.FinalStadium={Built:r.BuiltCapacity,Usable:r.UsableCapacity,Condition:r.ConditionPercent};
    if(s.Episodes.some(e=>e.Recovery)) {
      s.MinCashAfterFirstRecovery=Math.min(s.MinCashAfterFirstRecovery??Infinity,r.Cash);
      s.ObservedSamplesAfterFirstRecovery++;
    }
    if(!s.Current && r.Cash<150000 && r.DivisionTier!=='Distrital' && r.Stage==='round') {
      const e={Entry:{Season:r.Season,Round:r.Round,Cash:r.Cash,Division:r.DivisionTier,SalaryBill:r.SalaryBill,
        CashBefore:r.CashBefore,CauseFlows:r.Flows,Stadium:{Built:r.BuiltCapacity,Usable:r.UsableCapacity,Condition:r.ConditionPercent}},
        Recovery:null,Flows:{},Sales:[],StadiumInvestmentWhileInCrisis:0};s.Episodes.push(e);s.Current=e;continue;
    }
    if(s.Current) {
      const e=s.Current;
      for(const[k,v]of Object.entries(r.Flows))e.Flows[k]=money(cents(e.Flows[k]??0)+cents(v));
      e.Sales.push(...r.Sales);
      e.StadiumInvestmentWhileInCrisis+=-(r.Flows.StadiumExpansion??0)-(r.Flows.StadiumRenovation??0);
      if(r.Cash>=350000) {
        e.Recovery={Season:r.Season,Round:r.Round,Stage:r.Stage,Cash:r.Cash,SalaryBill:r.SalaryBill,
          ElapsedRounds:(r.Season-e.Entry.Season)*22+r.Round-e.Entry.Round};
        const positive=Object.entries(e.Flows).filter(([,v])=>v>0).sort((a,b)=>b[1]-a[1]);
        const dominant=positive[0]?.[0];
        e.PrimaryMechanism=dominant==='TransferSale'?'RECOVERY_BY_SALES':dominant==='PrizeMoney'?'RECOVERY_BY_COMPETITION_REWARD':
          ['Sponsorship','TicketRevenue'].includes(dominant)?'RECOVERY_BY_REVENUE':'RECOVERY_BY_OTHER';
        e.CostReductionObserved=r.SalaryBill<e.Entry.SalaryBill;
        e.ArithmeticBalanceWithoutSponsorship=r.Cash-(e.Flows.Sponsorship??0);
        s.Current=null;
      }
    }
  }
  assert.equal(official.size,data.summary.ClubsEverInCrisis.length);
  assert.equal(officialRecovered.size,data.summary.ClubsRecoveredFromCrisis.length);
  for(const s of states.values()) {
    delete s.Current;
    s.Relapses=Math.max(0,s.Episodes.length-1);
    const recovery=s.Episodes[0].Recovery;
    s.ObservedRoundsAfterFirstRecovery=recovery?s.FinalRoundOrdinal-((recovery.Season-1)*22+recovery.Round):0;
    for(const e of s.Episodes) {
      const expenses=Object.entries(e.Entry.CauseFlows).filter(([,v])=>v<0).sort((a,b)=>a[1]-b[1]);
      e.Entry.LargestOutflowCategory=expenses[0]?.[0]??null;
      e.Entry.CauseInterpretation='Largest recorded outflow in the interval preceding the crisis sample; not proof of an exclusive counterfactual cause.';
    }
    s.SustainableUnderObservedPolicy=s.Episodes.length===1 && s.Episodes[0].Recovery!=null
      && s.MinCashAfterFirstRecovery>=150000 && s.ObservedRoundsAfterFirstRecovery>=22 && s.FinalCash>=350000;
  }
  return {CrisisDefinition:'Cash < 150000 at post-market round sampling, while not district; recovery >=350000. These reproduce 006, not bankruptcy.',
    SustainableDefinition:'After first recovery: no sampled relapse below150000, at least22 subsequent rounds, final cash>=350000; no claim beyond season100.',
    OfficialCrisisClubs:official.size,OfficialRecoveredClubs:officialRecovered.size,
    Clubs:[...states.values()],PrimaryMechanisms:countBy([...states.values()].flatMap(s=>s.Episodes.filter(e=>e.Recovery)),e=>e.PrimaryMechanism),
    SustainableClubs:[...states.values()].filter(s=>s.SustainableUnderObservedPolicy).length,
    ClubsWithRelapse:[...states.values()].filter(s=>s.Relapses>0).length,
    Note:'Flows before the entry sample are recorded as causes; recovery flows start after entry. Ledger minima include intraround states. Without-sponsorship figure is arithmetic with decisions held fixed, not a simulated alternative economy.'};
}

const baselineDir=path.join(audit,'baseline-capture');
const finalDir=path.join(candidateRoot,'capture-007');
const baseline=await collect(baselineDir,true);
const candidate=await collect(finalDir,false);
assert.equal(baseline.purchases.length,33325);assert.equal(baseline.summary.TotalPurchases,8800);assert.equal(baseline.summary.AuctionsWon,24525);
const baselineRecovery=await recoveries(baselineDir,baseline), candidateRecovery=await recoveries(finalDir,candidate);
write('TRANSFER_RECONCILIATION_007.json',{Task:'ELIFOOT_METRIC_CONSISTENCY_AI_REFINEMENT_007',Scope:scope,Definitions:definitions,
  RootCause:'METRIC_ERROR: bilateral-only total compared with completed bilateral+auction purchases per manager. No evidence these counters included bids, evaluations, attempts or renewals.',
  Baseline:{BilateralPurchases:8800,AuctionPurchases:24525,CompletedPurchases:33325,ByProfile:baseline.byProfile,
    Reconciliation:baseline.reconciliation,AttemptsAndBids:null,UnavailableReason:'006 did not record these stage events; zero must not be inferred.'},
  Candidate:{Metrics:candidate.managerTotals,ByProfile:candidate.byProfile,ByManager:candidate.perManager,
    Reconciliation:candidate.reconciliation,ByMechanism:countBy(candidate.purchases,t=>t.TransactionType),
    OtherFreeMovements:candidate.categoryCounts.FreeAgentSigning??0,Quality:candidate.quality,SigningReasons:candidate.reasons},
  CanonicalTransactions:{Path:path.relative(root,path.join(finalDir,'ledger.jsonl.gz')),Id:'purchase FinancialTransaction.Id / TransferTransactionId',
    Fields:['Id','PlayerId','BuyerClubId','SellerClubId','ManagerId','TransactionType','Amount','SeasonNumber','RoundNumber','SigningDecision'],
    NullSeller:'Auction is for a player whose contract expired; no club seller and no seller credit. FormerClubId is not a seller.',
    AmountConvention:'negative financial debit; paid amount is its absolute value'},
  Retention:'Live world snapshot prunes old transactions; the complete 100-season per-manager evidence is archived in ledger.jsonl.gz and actions.jsonl.gz. Do not query a two-season snapshot as an entire career.'});

write('AI_ADAPTABILITY_007.json',{Scope:scope,Classification:'BUG',Threshold65:{IntentionalIn005:true,
  Source:'GAMEPLAY_BALANCE_100_SEASONS_005.md BALANCEAMENTO-002 and original AiManagerBrain.cs',
  Original:'Only adaptability>=65 and second preference; defensive identities could not chase a deficit. A second preference could be less attacking. Sub quota also prevented free formation-only decisions.',
  Reproduction:'docs/audit-007/reproductions.log: all11 attribute cases and persisted-round test failed before changes.',Removed:true},
  Correction:{ChasingMinute:'clamp(84 - Adaptability*0.28 - max(0,deficit-1)*8,55,84)',
    ProtectingMinute:'clamp(88 - Adaptability*0.14 - max(0,lead-1)*3,70,88)',
    DecisionPoints:[62,74,83,112,'free intervals','injury'],CooldownMinutes:10,
    Constraints:'Only own weighted preferred formations, directional score response or reduced position shortage, fit with available bench and remaining quotas. No random draw; no preference/identity mutation.'},
  Controlled:read(path.join(candidateRoot,'adaptability-controlled.json')),
  BaselineManagersWhoShifted:baseline.summary.Managers.filter(m=>m.InMatchTacticalShifts>0).length,
  CandidateManagersWhoShifted:candidate.summary.Managers.filter(m=>m.InMatchTacticalShifts>0).length,
  MandatoryAnswers:{
    Q1:'Yes, explicitly intentional in005, to preserve tactical identity.',Q2:'Below65 could select a different pre-match formation and substitute, but could never execute the in-match formation branch.',
    Q3:'Yes, a three-goal deficit at83 was ignored; reproduced.',Q4:'Yes,64 never entered the branch,65 did; fixed by gradual timing.',
    Q5:'Own weighted preferences outrank alternatives; random variation remains only in unchanged pre-match selection.',
    Q6:'Before only list index1, not the third; now all valid weighted preferences can be considered for context and fit.',
    Q7:'Now every tested adaptability can react to an extreme deficit when it has a suitable preferred formation and resources.',
    Q8:'Injuries trigger immediate substitutions; shortages can favor a better-fit preference. Red cards do not create a substitute slot. No suitable option means preserving current tactic.',
    Q9:'Existing ApplyInMatchAdjustments remains authority for5/3,6/4 and free intervals. Covered by old and new tests.',
    Q10:'Manager identity, attributes and preference weights remain on the same persistent manager entity when clubs rotate.'}});

const discipline=data=>({Matches:data.summary.TotalMatchesCompleted,RealMinutes:data.summary.RealMinutesSimulated,
  ByBehavior:Object.entries(data.summary.Discipline.ByBehavior).map(([Behavior,v])=>({Behavior,...v,
    Fouls:null,FoulAttribution:'Not recorded per offender for non-card/penalty fouls; cannot reconstruct honestly.',
    RedsPer10000Minutes:(v.DirectReds+v.SecondYellowReds)*10000/v.Minutes})),
  UnattributedTeamFouls:data.summary.Discipline.UnattributedFouls,
  RedEvents:data.summary.Discipline.RedEvents,
  RedBySide:countBy(data.summary.Discipline.RedEvents,e=>e.IsHome?'Home':'Away'),
  RedByFormation:countBy(data.summary.Discipline.RedEvents,e=>e.Formation),
  RedByDivision:countBy(data.summary.Discipline.RedEvents,e=>e.DivisionTier),
  RedByStrength:countBy(data.summary.Discipline.RedEvents,e=>String(e.Strength)),
  RedByMinute:countBy(data.summary.Discipline.RedEvents,e=>String(e.Minute)),
  PlayerExposure:data.summary.Discipline.ByPlayer});
write('PLAYER_BEHAVIOR_DISCIPLINE_007.json',{Scope:scope,Classification:'DESIGN_CHOICE',FairPlay:'Documented project rule: zero intentional foul/card weight. The engine has no other card-generating event category. This audit does not certify historical proprietary Elifoot rules.',
  ModelWeights:{FairPlay:0,Cordeirinho:.35,Cavalheiro:.75,Caneleiro:1.45,Caceteiro:2.35,Sarrafeiro:3.6},
  DocumentationError:'MOTOR_DEFESA.md still described005 weights and direct-red probabilities; updated to actual006 code. 006 prose also says0.0082 while the reproduced code uses0.0128; code unchanged.',
  ExposureDefinition:'Player-minute present at the start of each simulated minute. Red-card minute counts; incoming substitute starts next minute. Appearances require at least one minute. SuspensionAwards counts red sanctions plus distinct yellow-accumulation sanctions, not missed games.',
  Baseline:discipline(baseline),Candidate:discipline(candidate),
  HomogeneousControlled:{Scope:'400 league matches per behavior, identical seeds7001..7400,home strength30 behavior under test,away strength30FairPlay,no AI reactions/substitutions',
    Rows:read(path.join(candidateRoot,'behavior-controlled.json'))},
  ChangesToMatchEngine:false});
write('FINANCIAL_RECOVERY_007.json',{Scope:scope,Baseline:baselineRecovery,Candidate:candidateRecovery,
  Rules:{SponsorshipPerRound:{Primeira:185000,Segunda:125000,Terceira:92000,Quarta:72000,Distrital:20000},
    All22RoundsFunded:true,SalaryAndContractAgingOnlyWhenClubHasMatch:true,
    Meaning:'Documented005 design funds eliminated clubs; this asymmetry materially supports recovery. Sponsorship is external revenue, not an inter-club transfer or hidden emergency credit.'},
  Classification:'DESIGN_CHOICE; long-term growing cash and guaranteed sponsorship deserve balance observation, not an unrequested coefficient change.'});
write('ECONOMIC_INTEGRITY_007.json',{Scope:scope,
  Baseline:{Reconciliation:baseline.reconciliation,Amounts:baseline.categoryAmounts,Counts:baseline.categoryCounts,
    OpeningCash:baseline.summary.InitialCash,ClosingCash:baseline.summary.FinalCash,LedgerNetIncludingOpening:baseline.summary.FinalCash},
  Candidate:{Reconciliation:candidate.reconciliation,Amounts:candidate.categoryAmounts,Counts:candidate.categoryCounts,
    OpeningCash:candidate.summary.InitialCash,ClosingCash:candidate.summary.FinalCash,LedgerNetIncludingOpening:candidate.summary.FinalCash,
    RoundCashVsLedgerMismatches:candidate.summary.CashLedgerMismatches},
  MoneyPrecision:'Reconciled in integer cents; initial balance is opening stock, excluded from operating income.',
  ExternalIncome:['TicketRevenue','Sponsorship','PrizeMoney'],
  ExternalExpenses:['PlayerSalary','ContractRenewal','StadiumFixedCost','StadiumVariableCost','StadiumMaintenance','StadiumRenovation','StadiumExpansion','AuctionPurchase'],
  AuctionMeaning:'Contract-expiry auction fee leaves the club economy; no seller club exists. Bilateral TransferPurchase+TransferSale is the conserved inter-club flow.',
  Fixes:['Persisted round settlement guard +006 snapshot reconstruction','Preflight both bilateral ledger legs before debiting','Complete SQLite ledger persistence: regression reproduced256 expected versus200 stored, then passed after removal of truncation'],
  BaselineObserverCorrection:'First capture counted128 seed InitialBalance entries as new operating flows: its raw summary reports128 mismatches and its observer assertion failed. Ledger and match run completed; this script separately reconciles opening stock and all recorded flows. Final observer excludes opening entries and asserts0 mismatches.',
  SourceLedger:path.relative(root,path.join(finalDir,'ledger.jsonl.gz'))});
console.log(JSON.stringify({baseline:baseline.reconciliation,candidate:candidate.reconciliation,metrics:candidate.managerTotals,
  baselineRecovery:{clubs:baselineRecovery.OfficialCrisisClubs,recovered:baselineRecovery.OfficialRecoveredClubs,sustainable:baselineRecovery.SustainableClubs,relapses:baselineRecovery.ClubsWithRelapse},
  candidateRecovery:{clubs:candidateRecovery.OfficialCrisisClubs,recovered:candidateRecovery.OfficialRecoveredClubs,sustainable:candidateRecovery.SustainableClubs,relapses:candidateRecovery.ClubsWithRelapse},quality:candidate.quality},null,2));
