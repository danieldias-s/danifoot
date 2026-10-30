import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const root='artifacts/013b-coverage/sources';fs.mkdirSync(root,{recursive:true});
const decode=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const manifestFile=path.join(root,'downloads-manifest.json');const log=fs.existsSync(manifestFile)?decode(manifestFile):[];
async function get(url,file){const target=path.join(root,file);if(fs.existsSync(target))return fs.readFileSync(target,'utf8');
 if(!['raw.githubusercontent.com','query.wikidata.org','www.wikidata.org'].includes(new URL(url).hostname))throw Error('Unapproved source host');
 const response=await fetch(url,{headers:{'User-Agent':'ElifootCatalogCoverage013B/1.0 (local-development)','Accept':'application/json, text/plain'},signal:AbortSignal.timeout(40000)});
 if(!response.ok)throw Error('HTTP '+response.status);const text=await response.text();if(Buffer.byteLength(text)>12e6)throw Error('Oversized');fs.writeFileSync(target,text);log.push({url,file,sha256:crypto.createHash('sha256').update(text).digest('hex'),fetchedAt:new Date().toISOString()});return text;}
const countries=[['BR','Brasil','south-america/brazil/br'],['AR','Argentina','south-america/argentina/ar'],['GB','Inglaterra','europe/england/eng'],['ES','Espanha','europe/spain/es'],['DE','Alemanha','europe/germany/de'],['IT','Itália','europe/italy/it'],['FR','França','europe/france/fr'],['PT','Portugal','europe/portugal/pt']];
const ct=decode(root+'/clubs-tree.json');for(const [code,,prefix]of countries){await get(`https://raw.githubusercontent.com/openfootball/clubs/${ct.sha}/${prefix}.clubs.txt`,'clubs-'+prefix.replaceAll('/','_')+'.clubs.txt');console.log('clubs '+code);}
const jt=decode(root+'/football.json-tree.json');const matchFiles=jt.tree.filter(e=>e.type==='blob'&&/^(2025|2025-26|2026|2026-27)\/(br\.[12]|ar\.1|en\.[1234]|es\.[12]|de\.[12]|it\.[12]|fr\.[12]|pt\.1)\.json$/.test(e.path));
for(const entry of matchFiles){await get(`https://raw.githubusercontent.com/openfootball/football.json/${jt.sha}/${entry.path}`,'football.json-'+entry.path.replaceAll('/','_'));console.log('matches '+entry.path);}
const qCountries=[['BR','Q155'],['AR','Q414'],['GB','Q145'],['ES','Q29'],['DE','Q183'],['IT','Q38'],['FR','Q142'],['PT','Q45']];
for(const [code,qid]of qCountries){const query=`SELECT DISTINCT ?team ?teamLabel ?league ?leagueLabel ?dissolved WHERE { ?team wdt:P17 wd:${qid}; wdt:P31 wd:Q476028 . OPTIONAL { ?team wdt:P118 ?league } OPTIONAL { ?team wdt:P576 ?dissolved } FILTER NOT EXISTS { ?team wdt:P31 ?female . VALUES ?female { wd:Q51481377 wd:Q28140340 } } SERVICE wikibase:label { bd:serviceParam wikibase:language "en,pt,es". } } ORDER BY ?team LIMIT 900`;
 const data=JSON.parse(await get('https://query.wikidata.org/sparql?format=json&query='+encodeURIComponent(query),'wikidata-men-'+code+'.json'));console.log('wikidata men '+code+' rows '+(data.results?.bindings?.length??'error'));await new Promise(r=>setTimeout(r,1100));}
fs.writeFileSync(manifestFile,JSON.stringify([...new Map(log.map(item=>[item.file,item])).values()],null,2));
