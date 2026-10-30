import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {DatabaseSync} from 'node:sqlite';
const root=path.resolve(import.meta.dirname,'..');
const audit=path.join(root,'docs','audit-009');
const session=JSON.parse(fs.readFileSync(path.join(audit,'dev-session.json'),'utf8').replace(/^\uFEFF/,''));
const db=new DatabaseSync(session.Database,{readOnly:true});
const record=db.prepare('SELECT SnapshotJson FROM ef_worlds WHERE Id = ?').get(session.WorldId.toUpperCase())
  ?? db.prepare('SELECT SnapshotJson FROM ef_worlds LIMIT 1').get();
const world=JSON.parse(record.SnapshotJson);
assert.equal(world.id,session.WorldId);
const get=async route=>{const r=await fetch(`${session.ApiUrl}/api/worlds/${session.WorldId}/${route}`);assert.equal(r.status,200);return r.json();};
const scopes=['general','division1','division2','division3','division4','cup'];
const results=[];
for(const category of ['scorers','goalkeepers','defenders'])for(const scope of scopes){
  const data=await get(`player-rankings?category=${category}&scope=${scope}&period=season&season=1`);
  let source=world.playerStatistics.filter(s=>s.season===1);
  if(scope==='cup')source=source.filter(s=>s.competitionType!=='League');
  if(scope.startsWith('division'))source=source.filter(s=>s.competitionType==='League'&&s.division===['','PrimeiraDivisao','SegundaDivisao','TerceiraDivisao','QuartaDivisao'][+scope.slice(-1)]);
  if(category==='goalkeepers')source=source.filter(s=>s.position==='G');
  if(category==='defenders')source=source.filter(s=>s.position==='D');
  const totals=new Map();for(const s of source){let t=totals.get(s.playerId);if(!t){t={matches:0,goals:0,conceded:0,yellow:0,red:0};totals.set(s.playerId,t);}t.matches+=s.matchesPlayed;t.goals+=s.goalsScored;t.conceded+=s.goalsConceded;t.yellow+=s.yellowCards;t.red+=s.redCards;}
  for(const row of [...data.rows,...data.notEligible]){const t=totals.get(row.playerId);assert.ok(t);assert.equal(row.matchesPlayed,t.matches);assert.equal(row.goalsScored,t.goals);assert.equal(row.goalsConceded,t.conceded);assert.equal(row.yellowCards,t.yellow);assert.equal(row.redCards,t.red);}
  if(category!=='scorers')assert.ok(data.rows.every(r=>r.matchesPlayed>=20));
  assert.equal(data.coverageWarnings.length,0);
  results.push({Category:category,Scope:scope,Rows:data.rows.length,NotEligible:data.notEligible.length,Leader:data.rows[0]??null,PersistedValuesMatch:true});
  fs.writeFileSync(path.join(audit,`api-${category}-${scope}.json`),JSON.stringify(data,null,2)+'\n');
}
const awards=await get('season-top-scorers?season=1');
const ledger=db.prepare("SELECT ClubId, Amount, IdempotencyKey FROM ef_financial_ledger WHERE IdempotencyKey LIKE 'season_top_scorer:%'").all();
assert.equal(awards.awards.length,2);assert.equal(ledger.length,2);
assert.equal(awards.awards.reduce((n,a)=>n+a.awardAmount,0),6_000_000);
assert.ok(awards.awards.every(a=>a.totalGoals===10&&a.awardAmount===3_000_000));
for(const a of awards.awards){assert.ok(ledger.some(t=>t.IdempotencyKey===a.idempotencyKey&&Number(t.Amount)===3_000_000));const h=await get(`players/${a.playerId}/history`);assert.equal(h.player.seasonTopScorerTitles,1);assert.equal(h.awards.length,1);fs.writeFileSync(path.join(audit,`api-player-${a.playerId}.json`),JSON.stringify(h,null,2)+'\n');}
const evidence={WorldId:world.id,Fixture:'Real complete season imported into a private SQLite database',Season:1,Checks:results,Awards:awards,Ledger:ledger,DatabaseIntegrity:db.prepare('PRAGMA integrity_check').get(),AllPersistedValuesMatch:true};
fs.writeFileSync(path.join(audit,'api-persisted-validation.json'),JSON.stringify(evidence,null,2)+'\n');
db.close();console.log(JSON.stringify({Queries:results.length,PersistedValuesMatch:true,Awards:awards.awards.length,PrizeTotal:6_000_000}));
