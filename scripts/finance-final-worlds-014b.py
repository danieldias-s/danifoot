"""Actual HTTP finance/contract/auction flows in the two real-catalog 014B worlds."""
from pathlib import Path
from decimal import Decimal as D
import runpy,json,uuid,urllib.parse
ROOT=Path(__file__).resolve().parents[1];api=runpy.run_path(str(ROOT/'scripts/smoke-final-worlds-014b.py'));call=api['call'];save=api['save'];fingerprint=api['fingerprint'];local=api['LOCAL']
contexts=[];results=[]
original_save=save
save=lambda name,value:original_save(name.replace('actual-financial-flow','actual-financial-ux'),value)
def post(path,owner):return call(path,{},owner)
def query(path,values):return path+'?'+urllib.parse.urlencode(values)
def money(value):return D(str(value))
def retry_without_duplication(action):
    try:return action()
    except RuntimeError as error:
        if 'HTTP 400' not in str(error):raise
        return {'repeatRejected':True}
def play(world,coaches,label):
    for coach,club in coaches:
        snap=call(f'/api/worlds/{world}/snapshot');detail=call(f'/api/worlds/{world}/clubs/{club}')
        call(f'/api/worlds/{world}/tactics',{'gameWorldId':world,'coachId':coach,'formation':detail['preferredFormation'],
         'starterPlayerIds':detail['preferredStarterIds'],'benchPlayerIds':detail['preferredBenchIds'],'idempotencyKey':label+'-ready-'+coach,'expectedRoundVersion':snap['roundVersion']},coach)
    for step in range(20):
        snap=call(f'/api/worlds/{world}/snapshot')
        if snap['roundState']=='RESULTS':break
        if snap['activePauseWindow']:
            for coach,_ in coaches:call(f'/api/worlds/{world}/continue',{'gameWorldId':world,'coachId':coach,'idempotencyKey':f'{label}-continue-{step}-{coach}'},coach)
        else:post(f'/api/worlds/{world}/tick?ticks=125',coaches[0][0])
    assert snap['roundState']=='RESULTS','Actual MatchEngine round did not finish'
    assert all(m['isFinished'] for m in snap['matches'])
    post(f'/api/worlds/{world}/next-round',coaches[0][0]);return snap
for gender in ['Male','Female']:
    login=call('/api/auth/local-login',{'nickname':'014B finanças '+gender,'deviceId':uuid.uuid4().hex});owner=login['coachId']
    frozen=json.loads((local/'inputs'/f'{gender}.input.json').read_text(encoding='utf-8'))
    request=frozen['request'];request['name']='014B finanças '+gender
    preview=call('/api/world-creator/preview',request,owner);assert not preview['errors'],preview['errors']
    # Set the controlled expiry contract before world confirmation. No active-save editor call.
    assembly=call('/api/world-creator/assemblies',{'name':request['name'],'gender':gender,'countries':request['countries'],
      'seed':request['seed'],'sourcePreviewId':preview['draftId']},owner)
    package=assembly['package'];first_division=[c for c in package['clubs'] if c['divisionTier']=='PrimeiraDivisao']
    seller_template=first_division[1];squad=[p for p in package['players'] if p['clubId']==seller_template['id']]
    mean=sum(p['strength'] for p in squad)/len(squad)
    probe=min((p for p in squad if p['position'] in ['D','M'] and sum(q['position']==p['position'] for q in squad)>3),key=lambda p:p['strength'])
    assert probe['strength']<mean-3;probe['contractRoundsRemaining']=2
    assembly=call('/api/world-creator/assemblies/'+assembly['id'],{'name':assembly['name'],'countries':assembly['countries'],
      'package':package,'expectedVersion':assembly['version'],'savePreset':True},owner)
    assert assembly['validation']['isValid'],assembly['validation']
    world=call('/api/world-creator/assemblies/'+assembly['id']+'/confirm',{'idempotencyKey':'finance-world-'+uuid.uuid4().hex},owner)['id']
    first_division=[c for c in call(f'/api/worlds/{world}/clubs') if c['divisionTier']=='PrimeiraDivisao'];buyer=first_division[0]['id'];seller=first_division[1]['id']
    seller_login=call('/api/auth/local-login',{'nickname':'014B vendedor '+gender,'deviceId':uuid.uuid4().hex});seller_coach=seller_login['coachId']
    post(query(f'/api/worlds/{world}/join',{'coachId':owner,'nickname':login['nickname'],'deviceId':login['deviceId'],'preferredClubId':buyer}),owner)
    post(query(f'/api/worlds/{world}/join',{'coachId':seller_coach,'nickname':seller_login['nickname'],'deviceId':seller_login['deviceId'],'preferredClubId':seller}),seller_coach)
    loanpath=query(f'/api/worlds/{world}/clubs/{buyer}/loans',{'coachId':owner,'amount':500000,'rounds':0,'idempotencyKey':'014b-finance-opening-loan'})
    post(loanpath,owner);post(loanpath,owner)
    for n in range(3):play(world,[(owner,buyer),(seller_coach,seller)],f'014b-finance-opening-{n}')
    contexts.append({'World':world,'Club':buyer,'Coach':owner,'Gender':gender,'Draft':preview['draftId'],'LoanPath':loanpath,'Seller':seller,'SellerCoach':seller_coach,'ProbeIdentity':probe['catalogIdentity']['canonicalIdentity']})
for c in contexts:
    world=c['World'];buyer=c['Club'];owner=c['Coach']
    seller=c['Seller'];seller_coach=c['SellerCoach']
    sd=call(f'/api/worlds/{world}/clubs/{seller}');bd=call(f'/api/worlds/{world}/clubs/{buyer}')
    target=next(p for p in reversed(sd['squad']) if p['position']=='A');price=target['fairValue'];seller_cash=money(sd['cashBalance']);buyer_cash=money(bd['cashBalance'])
    origin=target['catalogIdentity']
    post(query(f'/api/worlds/{world}/market/list',{'coachId':seller_coach,'playerId':target['id'],'askingPrice':price,'idempotencyKey':'014b-live-list'}),seller_coach)
    purchase=query(f'/api/worlds/{world}/market/buy-listed',{'coachId':owner,'playerId':target['id'],'idempotencyKey':'014b-live-purchase'})
    post(purchase,owner);retry_without_duplication(lambda:post(purchase,owner))
    assert money(call(f'/api/worlds/{world}/clubs/{seller}')['cashBalance'])==seller_cash+money(price)
    bd=call(f'/api/worlds/{world}/clubs/{buyer}');assert money(bd['cashBalance'])==buyer_cash-money(price)
    assert next(p for p in bd['squad'] if p['id']==target['id'])['catalogIdentity']==origin
    renewed=next(p for p in bd['squad'] if p['id']!=target['id']);salary=renewed['salaryPerRound'];contract=renewed['contractRoundsRemaining']
    renewal=query(f"/api/worlds/{world}/players/{renewed['id']}/renew",{'coachId':owner,'additionalRounds':5,'idempotencyKey':'014b-live-renew'})
    post(renewal,owner);retry_without_duplication(lambda:post(renewal,owner))
    changed=next(p for p in call(f'/api/worlds/{world}/clubs/{buyer}')['squad'] if p['id']==renewed['id'])
    assert changed['salaryPerRound']==salary and changed['contractRoundsRemaining']==contract+5 and changed['negotiatedContractProtected']
    finance=call(f'/api/worlds/{world}/clubs/{buyer}/finances');loan=finance['loans'][0];before_repay=money(finance['cashBalance'])
    repay=f"/api/worlds/{world}/clubs/{buyer}/loans/{loan['id']}/repay";body={'coachId':owner,'idempotencyKey':'014b-live-repay'}
    call(repay,body,owner);retry_without_duplication(lambda:call(repay,body,owner))
    finance=call(f'/api/worlds/{world}/clubs/{buyer}/finances');assert finance['loans'][0]['remainingBalance']==0 and finance['loans'][0]['principalPaid']==500000
    assert money(finance['cashBalance'])==before_repay-D(500000)
    # The probe expires through official played matches, with its initial contract fixed pre-save.
    sd=call(f'/api/worlds/{world}/clubs/{seller}')
    expiring=next(p for p in sd['squad'] if p['catalogIdentity']['canonicalIdentity']==c['ProbeIdentity'])
    assert expiring['contractRoundsRemaining']==1
    coaches=[(owner,buyer),(seller_coach,seller)]
    played=[play(world,coaches,'014b-expiry')]
    market=call(f'/api/worlds/{world}/market');auction=next(a for a in market['auctions'] if a['playerId']==expiring['id'] and not a['isClosed'])
    bid=auction['minimumBid'] if not auction['currentBid'] else round(auction['currentBid']*1.06)
    bidding=query(f"/api/worlds/{world}/market/auction/{auction['auctionId']}/bid",{'coachId':owner,'bidAmount':bid,'idempotencyKey':'014b-live-auction'})
    post(bidding,owner);retry_without_duplication(lambda:post(bidding,owner))
    accepted=next(a for a in call(f'/api/worlds/{world}/market')['auctions'] if a['auctionId']==auction['auctionId']);assert accepted['highestBidderClubId']==buyer and accepted['currentBid']==bid
    for step in range(3):
        current=next(a for a in call(f'/api/worlds/{world}/market')['auctions'] if a['auctionId']==auction['auctionId'])
        if current['isClosed']:break
        played.append(play(world,coaches,'014b-auction-settle-'+str(step)))
    closed=next(a for a in call(f'/api/worlds/{world}/market')['auctions'] if a['auctionId']==auction['auctionId']);assert closed['isClosed']
    finances=[]
    for club in [buyer,seller]:
        detail=call(f'/api/worlds/{world}/clubs/{club}');f=call(f'/api/worlds/{world}/clubs/{club}/finances')
        assert 11<=len(detail['squad'])<=24 and sum(p['position']=='G' for p in detail['squad'])>=1 and sum(p['position']!='G' for p in detail['squad'])>=10
        assert sum((money(t['amount']) for t in f['ledger']),D(0))==money(f['cashBalance'])
        assert len({t['id'] for t in f['ledger']})==len(f['ledger'])
        finances.append({'Club':club,'Cash':f['cashBalance'],'LedgerRows':len(f['ledger']),'LedgerDifference':'0','Squad':len(detail['squad'])})
    c['Fingerprint']=fingerprint(world,buyer);c['Seller']=seller;c['SellerCoach']=seller_coach
    (local/'finance-ux-context.json').write_text(json.dumps(contexts,indent=2),encoding='utf-8')
    result={'World':world,'Gender':c['Gender'],'PurchasePrice':price,'TransferMoneyReconciled':True,'TransferOriginPreserved':True,
       'RenewedSalary':salary,'RenewalRoundsAddedOnce':5,'PrincipalPaidOnce':500000,'InterestAlreadyCharged':loan['interestCharged'],
       'Auction':closed,'ExpiryProbe':'Initial contract set to 2 before save confirmation; expiry by official matches; no active-world edits.',
       'AdditionalRealRounds':len(played),'NextRound':call(f'/api/worlds/{world}/snapshot')['roundNumber'],'ClubReconciliations':finances}
    results.append(result);save('actual-financial-flow.json',{'Status':'PASS' if len(results)==2 else 'RUNNING','Worlds':results})
    print('PASS actual finance',c['Gender'],': buy/sell, protected renewal, loan payoff, auction, real matches and ledgers reconciled.',flush=True)
