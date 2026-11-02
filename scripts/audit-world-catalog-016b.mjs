// Real local HTTP + SignalR audit. No third-party requests and no browser automation.
import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
const root=path.resolve(import.meta.dirname,'..');
assert.equal(root.toLowerCase(),'d:\\elifoot_98_016b');
const area=path.join(root,'artifacts/016b/world-catalog');
const base='http://127.0.0.1:5175/api';
const save=(name,data)=>fs.writeFileSync(path.join(area,name),JSON.stringify(data,null,2));
const load=name=>JSON.parse(fs.readFileSync(path.join(area,name),'utf8').replace(/^\uFEFF/,''));
const events=[];
async function request(route,{body,actor,status=200}={}){
 const response=await fetch(base+route,{method:body===undefined?'GET':'POST',headers:{'content-type':'application/json',...(actor?{authorization:'Bearer '+actor.sessionToken}:{})},...(body===undefined?{}:{body:JSON.stringify(body)})});
 const text=await response.text();let data;try{data=JSON.parse(text)}catch{data=text}
 assert.equal(response.status,status,`${route}: ${JSON.stringify(data).slice(0,600)}`);return data;
}
const pause=ms=>new Promise(r=>setTimeout(r,ms));
async function connect(world,coach){
 const ws=new WebSocket('ws://127.0.0.1:5175/hubs/gameroom');let seq=0;const pending=new Map();
 const opened=new Promise((resolve,reject)=>{ws.addEventListener('open',resolve,{once:true});ws.addEventListener('error',reject,{once:true})});
 ws.addEventListener('message',e=>{for(const frame of String(e.data).split('\x1e').filter(Boolean)){const m=JSON.parse(frame);if(m.type===3&&pending.has(m.invocationId)){const p=pending.get(m.invocationId);pending.delete(m.invocationId);m.error?p.reject(Error(m.error)):p.resolve(m.result)}}});
 await opened;ws.send('{"protocol":"json","version":1}\x1e');
 const call=(target,args)=>new Promise((resolve,reject)=>{const id=String(++seq);const timer=setTimeout(()=>{pending.delete(id);reject(Error('SignalR timeout '+target))},15000);pending.set(id,{resolve:x=>{clearTimeout(timer);resolve(x)},reject:e=>{clearTimeout(timer);reject(e)}});ws.send(JSON.stringify({type:1,invocationId:id,target,arguments:args})+'\x1e')});
 await call('JoinRoom',[world,coach]);return {ws,call,close:()=>ws.close()};
}
const ctx=load('preview-context-final.json');
let state=fs.existsSync(path.join(area,'http-context-final.json'))?load('http-context-final.json'):null;
if(!state){
 const world=await request('/world-creator/confirm/'+ctx.preview.draftId,{body:{},actor:ctx.auth});
 const again=await request('/world-creator/confirm/'+ctx.preview.draftId,{body:{},actor:ctx.auth});assert.equal(world.id,again.id);
 const actors=[ctx.auth];for(let n=1;n<4;n++)actors.push(await request('/auth/local-login',{body:{nickname:['','QA Desempregado','QA Sorteio A','QA Sorteio B'][n],deviceId:'016b-wc-audit-'+n}}));
 state={world,actors};save('http-context-final.json',state);
}
const {world,actors}=state;const id=world.id;const sockets=[];
const lobby=()=>request('/world-lobby-016b/'+id,{actor:actors[0]});
async function record(step,expectedConnected){let v;for(let i=0;i<40;i++){v=await lobby();if(expectedConnected===undefined||v.connected===expectedConnected)break;await pause(100)}if(expectedConnected!==undefined)assert.equal(v.connected,expectedConnected,step);events.push({step,...v});save('live-lobby-evidence.json',{status:'RUNNING',events});return v}
try{
 for(const a of actors)await request(`/worlds/${id}/join?coachId=${a.coachId}&nickname=${encodeURIComponent(a.nickname)}&deviceId=${encodeURIComponent(a.deviceId)}&joinUnemployed=false`,{body:{}});
 await record('Four registered seats, no transport',0);
 for(let n=0;n<actors.length;n++){sockets.push(await connect(id,actors[n].coachId));await record('SignalR participant '+(n+1),n+1)}
 let v=await lobby();
 if(!v.drawCompleted&&v.state!=='IN_GAME'){
  const clubs=await request(`/worlds/${id}/clubs`);const manual=clubs.find(c=>c.divisionTier==='PrimeiraDivisao'&&!c.isHumanControlled);assert.ok(manual);
  const choice=(a,body)=>request(`/world-lobby-016b/${id}/choice`,{body,actor:a});
  await choice(actors[0],{choice:'MANUAL',clubId:manual.id});await choice(actors[1],{choice:'UNEMPLOYED'});
  await request(`/world-lobby-016b/${id}/start`,{body:{idempotencyKey:'audit-premature'},actor:actors[0],status:400});
  for(const a of actors.slice(2))await choice(a,{choice:'FOURTH_DIVISION_DRAW'});
  await request(`/world-lobby-016b/${id}/draw`,{body:{idempotencyKey:'not-host'},actor:actors[1],status:403});
  const duplicate=await connect(id,actors[0].coachId);await record('Duplicate host tab counts once',4);duplicate.close();await record('Closing duplicate preserves host',4);
  sockets[3].close();await record('Disconnect blocks draw',3);assert.equal((await lobby()).canDraw,false);
  await request(`/world-lobby-016b/${id}/draw`,{body:{idempotencyKey:'disconnected'},actor:actors[0],status:400});
  sockets[3]=await connect(id,actors[3].coachId);await record('Reconnect restores same seat',4);
  const draws=await Promise.all(Array.from({length:8},(_,i)=>request(`/world-lobby-016b/${id}/draw`,{body:{idempotencyKey:'concurrent-'+i},actor:actors[0]})));
  assert.ok(draws.every(x=>JSON.stringify(x.participants)===JSON.stringify(draws[0].participants)));
  v=draws[0];assert.equal(v.participants.find(p=>p.coachId===actors[0].coachId).clubId,manual.id);assert.equal(v.participants.find(p=>p.coachId===actors[1].coachId).clubId,null);
  const assigned=v.participants.filter(p=>p.choice==='FOURTH_DIVISION_DRAW');assert.equal(new Set(assigned.map(p=>p.clubId)).size,2);assert.ok(assigned.every(p=>clubs.find(c=>c.id===p.clubId).divisionTier==='QuartaDivisao'));
  state.draw=v;save('http-context-final.json',state);await record('Eight concurrent draws persisted once',4);
 }
 const outsider=await request('/auth/local-login',{body:{nickname:'QA Outside',deviceId:'016b-wc-outside'}});
 const full=await request(`/worlds/${id}/join?coachId=${outsider.coachId}&nickname=Outside&deviceId=016b-wc-outside`,{body:{},status:400});assert.equal(full.code,'ROOM_FULL');
 await request(`/world-lobby-016b/${id}/start`,{body:{idempotencyKey:'start-once'},actor:actors[0]});
 const before=await lobby();const repeat=await request(`/world-lobby-016b/${id}/start`,{body:{idempotencyKey:'repeat-start'},actor:actors[0]});assert.equal(before.version,repeat.version);
 await record('Host start persists and repeats idempotently',4);
 let snap=await request(`/worlds/${id}/snapshot`);
 if(snap.roundState==='PREPARING'&&snap.roundNumber===1){
  for(const a of actors){
   snap=await request(`/worlds/${id}/snapshot`);const p=(await lobby()).participants.find(p=>p.coachId===a.coachId);
   const common={gameWorldId:id,coachId:a.coachId,idempotencyKey:'audit-tactics-'+a.coachId,expectedRoundVersion:snap.roundVersion};
   if(p.clubId){const club=await request(`/worlds/${id}/clubs/${p.clubId}`);assert.equal(club.preferredStarterIds.length,11);await request(`/worlds/${id}/tactics`,{body:{...common,formation:club.preferredFormation,starterPlayerIds:club.preferredStarterIds,benchPlayerIds:club.preferredBenchIds},actor:a})}
   else await request(`/worlds/${id}/ready-unemployed`,{body:common,actor:a});
  }
  snap=await request(`/worlds/${id}/snapshot`);assert.notEqual(snap.roundState,'PREPARING');save('round-start-evidence.json',snap);
 }
 for(let i=0;i<40;i++){
  snap=await request(`/worlds/${id}/snapshot`);
  if(['RESULTS','ROUND_FINISHED'].includes(snap.roundState)||snap.roundNumber>1)break;
  if(snap.activePauseWindow){for(const a of actors)await request(`/worlds/${id}/continue`,{body:{gameWorldId:id,coachId:a.coachId,idempotencyKey:'continue-'+i+'-'+a.coachId}})}
  else if(String(snap.roundState).startsWith('RUNNING_'))await request(`/worlds/${id}/tick?ticks=125`,{body:{}});
  else throw Error('Unexpected live match state '+snap.roundState);
 }
 assert.ok(['RESULTS','ROUND_FINISHED'].includes(snap.roundState));save('round-finished-evidence.json',snap);
 const calendar=await request(`/worlds/${id}/calendar`);save('calendar-evidence.json',calendar);
 for(const country of ['ES','GB']){const status=await request('/world-catalog-016b/refresh',{body:{countryCode:country,gender:'Male',season:'2026'},actor:actors[0]});assert.equal(status.status,'NOT_CONNECTED')}
 const report=await request('/world-catalog-016b/report?country=ES&gender=Male&season=2026',{actor:actors[0]});save('catalog-report-es.json',report);
 save('live-lobby-evidence.json',{status:'PASS',transport:'Real HTTP + WebSocket SignalR on PostgreSQL; no browser UI',worldId:id,events,finishedRound:snap.roundNumber,finishedState:snap.roundState,provider:report.provider});
 console.log('PASS: HTTP/SignalR, concurrent draw, reconnection, host start, synchronized tactics and actual completed round. World '+id);
}finally{for(const c of sockets)c?.close()}
