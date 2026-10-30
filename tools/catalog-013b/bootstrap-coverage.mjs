import fs from 'node:fs';
import crypto from 'node:crypto';

// Pinned inputs, structured CC0 only. No credentials or paid endpoints.
const root = 'artifacts/013b-coverage/sources';
fs.mkdirSync(root, { recursive: true });
const repositories = JSON.parse(fs.readFileSync('docs/audit-013b-coverage/source-repositories.json', 'utf8'));
const read = file => JSON.parse(fs.readFileSync(`${root}/${file}`, 'utf8').replace(/^\uFEFF/, ''));
async function cache(url, file) {
  if (fs.existsSync(`${root}/${file}`)) return;
  const response = await fetch(url, {
    headers: { 'User-Agent': 'ElifootCatalogCoverage013B/1.0 (local-development)' },
    signal: AbortSignal.timeout(40000),
  });
  if (!response.ok) throw new Error(`${response.status}: ${file}`);
  const body = await response.text();
  if (Buffer.byteLength(body) > 12e6) throw new Error('Oversized input');
  fs.writeFileSync(`${root}/${file}`, body);
  fs.writeFileSync(`${root}/${file}.download.json`, JSON.stringify({ url, fetchedAt: new Date().toISOString(), sha256: crypto.createHash('sha256').update(body).digest('hex') }, null, 2));
}
for (const repository of repositories) {
  await cache(`https://api.github.com/repos/openfootball/${repository.repo}/git/trees/${repository.sha}?recursive=1`, `${repository.repo}-tree.json`);
  const tree = read(`${repository.repo}-tree.json`);
  if (tree.truncated || tree.sha !== repository.sha) throw new Error('Unpinned/incomplete repository tree');
  const license = tree.tree.find(entry => /^(LICENSE|UNLICENSE)(\.txt|\.md)?$/i.test(entry.path));
  if (!license) throw new Error('Explicit license missing');
  await cache(`https://raw.githubusercontent.com/openfootball/${repository.repo}/${repository.sha}/${license.path}`, `${repository.repo}-LICENSE.txt`);
}
const query = `SELECT DISTINCT ?team ?teamLabel ?country ?type ?league ?leagueLabel ?dissolved WHERE {
 VALUES ?country { wd:Q155 wd:Q414 wd:Q145 wd:Q29 wd:Q183 wd:Q38 wd:Q142 wd:Q45 }
 VALUES ?type { wd:Q51481377 wd:Q28140340 }
 ?team wdt:P17 ?country; wdt:P31 ?type .
 OPTIONAL { ?team wdt:P118 ?league } OPTIONAL { ?team wdt:P576 ?dissolved }
 SERVICE wikibase:label { bd:serviceParam wikibase:language "en,pt,es". }
} ORDER BY ?country ?team LIMIT 3000`;
await cache('https://query.wikidata.org/sparql?format=json&query=' + encodeURIComponent(query), 'wikidata-women-expanded.json');
const women = read('wikidata-women-expanded.json');
if (!women.results?.bindings || women.results.bindings.length >= 3000) throw new Error('Incomplete/capped female discovery');
const ids = JSON.parse(fs.readFileSync('tools/catalog-013b/reviewed-entity-ids.json', 'utf8'));
if (ids.length > 50 || ids.some(id => !/^Q[1-9][0-9]*$/.test(id))) throw new Error('Entity batch invalid');
await cache('https://www.wikidata.org/w/api.php?action=wbgetentities&ids=' + ids.join('%7C') + '&props=labels%7Cclaims%7Cinfo&languages=en%7Cpt&format=json&maxlag=5', 'wikidata-reviewed-men.json');
if (read('wikidata-reviewed-men.json').error) throw new Error('Wikidata maxlag/error');
console.log('Pinned repositories and bounded discovery inputs ready.');
