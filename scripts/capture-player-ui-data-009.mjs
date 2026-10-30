import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
const root=path.resolve(import.meta.dirname,'..');
const audit=path.join(root,'docs','audit-009');
const session=JSON.parse(fs.readFileSync(path.join(audit,'dev-session.json'),'utf8').replace(/^\uFEFF/,''));
const responses={};
const key=url=>{const u=new URL(url);u.searchParams.sort();return `${u.pathname}${u.search}`;};
async function capture(route){const url=`${session.ApiUrl}/api/worlds/${session.WorldId}/${route}`;const r=await fetch(url,{signal:AbortSignal.timeout(10000)});assert.equal(r.status,200);const body=await r.text();responses[key(url)]={status:r.status,body,sha256:crypto.createHash('sha256').update(body).digest('hex')};return JSON.parse(body);}
for(const category of ['scorers','goalkeepers','defenders'])for(const scope of ['general','division1','division2','division3','division4','cup'])for(const period of ['career','current','season'])
 await capture(`player-rankings?category=${category}&scope=${scope}&period=${period}${period==='season'?'&season=1':''}`);
const awards=await capture('season-top-scorers');
await capture('season-top-scorers?season=1');
await capture('season-top-scorers?season=2');
for(const award of awards.awards)await capture(`players/${award.playerId}/history`);
assert.equal(awards.awards.length,2);assert.ok(awards.awards.every(a=>a.totalGoals===10&&a.awardAmount===3000000));
const result={method:'Verbatim JSON HTTP responses captured from running QA API backed by the real SQLite completed season. Flutter test replays transport in memory; no invented sporting data.',capturedAt:new Date().toISOString(),browserUsed:false,sourceApi:session.ApiUrl,worldId:session.WorldId,responses};
fs.writeFileSync(path.join(audit,'live-ui-response-snapshot.json'),JSON.stringify(result,null,2)+'\n');
console.log(JSON.stringify({HttpResponses:Object.keys(responses).length,World:session.WorldId,Awards:awards.awards.length}));
