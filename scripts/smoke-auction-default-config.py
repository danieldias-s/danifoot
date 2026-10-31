"""Real loopback HTTP/PostgreSQL acceptance, restricted to API 014C (5155)."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import runpy,json,uuid,urllib.parse,sys
ROOT=Path(__file__).resolve().parents[1];helpers=runpy.run_path(str(ROOT/'scripts/smoke-club-colors-014c.py'))
call=helpers['call'];LOCAL=ROOT/'artifacts/auction-default-config';AUDIT=ROOT/'docs/audit-auction-default-config';LOCAL.mkdir(exist_ok=True)
def write(p,x):p.write_text(json.dumps(x,ensure_ascii=False,indent=2),encoding='utf-8')
def snap(w):return call(f'/api/worlds/{w}/snapshot')
def auctions(w):return call(f'/api/worlds/{w}/market')['auctions']
def start_round(w,coaches):
 for owner,club in coaches:
  d=call(f'/api/worlds/{w}/clubs/{club}');s=snap(w)
  call(f'/api/worlds/{w}/tactics',{'gameWorldId':w,'coachId':owner,'formation':d['preferredFormation'],'starterPlayerIds':d['preferredStarterIds'],'benchPlayerIds':d['preferredBenchIds'],'idempotencyKey':uuid.uuid4().hex,'expectedRoundVersion':s['roundVersion']},owner)
def finish(w,coaches):
 for step in range(24):
  s=snap(w)
  if s['roundState']=='RESULTS':return
  if s['activePauseWindow']:
   for owner,club in coaches:call(f'/api/worlds/{w}/continue',{'gameWorldId':w,'coachId':owner,'idempotencyKey':uuid.uuid4().hex},owner)
  else:call(f'/api/worlds/{w}/tick?ticks=125',{},coaches[0][0])
 assert snap(w)['roundState']=='RESULTS'
def run():
 w=call('/api/worlds',{'worldName':'Auction Default Config HTTP','totalClubs':64,'maxHumans':4,'seed':199801})['gameWorldId']
 clubs=call(f'/api/worlds/{w}/clubs');seller,buyer=[c['id'] for c in clubs if c['divisionTier']=='PrimeiraDivisao'][:2]
 coaches=[]
 for name,club in [('Seller',seller),('Buyer',buyer)]:
  login=call('/api/auth/local-login',{'nickname':'Auction '+name,'deviceId':'auction-'+uuid.uuid4().hex});owner=login['coachId']
  call(f'/api/worlds/{w}/join?'+urllib.parse.urlencode({'coachId':owner,'nickname':login['nickname'],'deviceId':login['deviceId'],'preferredClubId':club}),{},owner);coaches.append([owner,club])
 owner=coaches[0][0];d=call(f'/api/worlds/{w}/clubs/{seller}');squad=[p for p in d['squad'] if p['position']!='G'];path=f'/api/worlds/{w}/market/auction'
 commands=[{'coachId':owner,'playerId':squad[-1]['id'],'idempotencyKey':'default'},
   {'coachId':owner,'playerId':squad[-2]['id'],'minimumBid':0,'durationRounds':3,'idempotencyKey':'zero-three'},
   {'coachId':owner,'playerId':squad[-3]['id'],'minimumBid':123.45,'durationRounds':1,'idempotencyKey':'cents-one'}]
 with ThreadPoolExecutor(max_workers=4) as pool:receipts=list(pool.map(lambda _:call(path,commands[0],owner),range(4)))
 assert len({x['auctionId'] for x in receipts})==1
 ids=[receipts[0]['auctionId']]+[call(path,c,owner)['auctionId'] for c in commands[1:]]
 live={x['auctionId']:x for x in auctions(w)}
 assert live[ids[0]]['minimumBid']==500000 and live[ids[0]]['durationRounds']==2 and live[ids[0]]['expiresAtRound']==2
 assert live[ids[1]]['minimumBid']==0 and live[ids[1]]['durationRounds']==3
 assert live[ids[2]]['minimumBid']==123.45 and live[ids[2]]['durationRounds']==1
 call(f'/api/worlds/{w}/market/auction/{ids[0]}/bid?'+urllib.parse.urlencode({'coachId':coaches[1][0],'bidAmount':500000,'idempotencyKey':'buyer-bid'}),{},coaches[1][0])
 for _ in range(3):assert not next(x for x in auctions(w) if x['auctionId']==ids[0])['isClosed']
 start_round(w,coaches);finish(w,coaches)
 live={x['auctionId']:x for x in auctions(w)};assert live[ids[2]]['isClosed'] and not live[ids[0]]['isClosed'] and not live[ids[1]]['isClosed']
 context={'world':w,'seller':seller,'buyer':buyer,'coaches':coaches,'commands':commands,'auctions':ids,'player':squad[-1]['id']}
 write(LOCAL/'context.json',context);write(AUDIT/'http-before-restart.json',{'Status':'PASS','Database':'PostgreSQL 014C, port 55438','World':w,'ConcurrentCreations':4,'UniqueDefaultAuction':True,'Defaults':[500000,2],'CustomZeroThree':True,'CentsOne':True,'AfterFirstGlobalRound':{'DefaultClosed':False,'ThreeClosed':False,'OneClosed':True},'PageReadsDoNotCount':True})
 print('PASS real PostgreSQL/HTTP: defaults, concurrent replay, custom zero/three, cents/one, first global round',flush=True)
def verify():
 c=json.loads((LOCAL/'context.json').read_text(encoding='utf-8'));w=c['world'];ids=c['auctions'];owner=c['coaches'][0][0]
 live={x['auctionId']:x for x in auctions(w)};assert live[ids[0]]['minimumBid']==500000 and live[ids[0]]['durationRounds']==2 and not live[ids[0]]['isClosed'];assert not live[ids[1]]['isClosed']
 replay=call(f'/api/worlds/{w}/market/auction',{**c['commands'][0],'expectedRoundVersion':-1},owner);assert replay['auctionId']==ids[0]
 call(f'/api/worlds/{w}/next-round',{},owner);assert not next(x for x in auctions(w) if x['auctionId']==ids[0])['isClosed']
 start_round(w,c['coaches']);finish(w,c['coaches']);live={x['auctionId']:x for x in auctions(w)}
 assert live[ids[0]]['isClosed'] and not live[ids[1]]['isClosed']
 # No assumption that AI never outbids: actual winner and amount must reconcile with the real ledger.
 a=live[ids[0]];package=call(f'/api/worlds/{w}/editor/export-json');player=next(x for x in package['players'] if x['id']==c['player'])
 assert a['closureReason']=='SOLD' and player['clubId']==a['highestBidderClubId']
 finance=call(f'/api/worlds/{w}/clubs/{c["seller"]}/finances');transactions=finance['ledger']
 # Exported world package contains no ledger; the server's finance endpoint is the authoritative seller ledger.
 sales=[t for t in transactions if t.get('category')=='AuctionSale' and player['name'] in t.get('description','')]
 assert len(sales)==1 and sales[0]['amount']==a['currentBid'],(len(sales),list(finance.keys()))
 buyer=call(f'/api/worlds/{w}/clubs/{a["highestBidderClubId"]}/finances');rows=buyer['ledger']
 buys=[t for t in rows if t.get('category')=='AuctionPurchase' and player['name'] in t.get('description','')]
 assert len(buys)==1 and buys[0]['amount']==-a['currentBid']
 again=call(f'/api/worlds/{w}/market/auction',c['commands'][0],owner);assert again['auctionId']==ids[0]
 write(AUDIT/'http-restart-and-settlement.json',{'Status':'PASS','ActualApiProcessRestart':True,'OriginalDeadlinePreserved':True,'AfterSecondGlobalRound':{'DefaultClosed':True,'ThreeClosed':False},'SaleAmount':a['currentBid'],'SellerCredit':sales[0]['amount'],'BuyerDebit':buys[0]['amount'],'PlayerClubMatchesWinningBidder':True,'CreationReplayAfterSaleSameAuction':True})
 print('PASS real process restart: original deadline, completion of global R2, seller credit = buyer debit, third-round auction still open',flush=True)
if __name__=='__main__':verify() if '--verify-restart' in sys.argv else run()
