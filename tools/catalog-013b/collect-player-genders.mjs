import fs from 'node:fs';
import crypto from 'node:crypto';
const root = 'artifacts/013b-coverage';
const bundle = JSON.parse(fs.readFileSync(`${root}/coverage-bundle.json`, 'utf8'));
const ids = [...new Set(bundle.packages.flatMap(p => p.players).map(p => p.playerExternalId))].sort();
const sexByPlayer = {};
for (let i = 0; i < ids.length; i += 100) {
  const group = ids.slice(i, i + 100);
  const file = `${root}/sources/player-genders-${String(i / 100).padStart(3, '0')}.json`;
  const query = `SELECT DISTINCT ?player ?sex WHERE { VALUES ?player { ${group.map(id => 'wd:' + id).join(' ')} } OPTIONAL { ?player wdt:P21 ?sex } } ORDER BY ?player LIMIT 300`;
  const manifest = file.replace('.json', '.manifest.json');
  let data;
  if (fs.existsSync(file) && fs.existsSync(manifest) && JSON.parse(fs.readFileSync(manifest)).query === query) {
    data = JSON.parse(fs.readFileSync(file));
  } else {
    const response = await fetch('https://query.wikidata.org/sparql?format=json&query=' + encodeURIComponent(query), {
      headers: { 'User-Agent': 'ElifootCatalogCoverage013B/1.0 (local-development)', Accept: 'application/sparql-results+json' },
      signal: AbortSignal.timeout(40000),
    });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const body = await response.text();
    data = JSON.parse(body);
    if (!data.results?.bindings || data.results.bindings.length >= 300) throw new Error('Incomplete/capped gender evidence');
    fs.writeFileSync(file, body);
    fs.writeFileSync(manifest, JSON.stringify({ ids: group, query, queriedAt: new Date().toISOString(), sha256: crypto.createHash('sha256').update(body).digest('hex') }, null, 2));
    await new Promise(resolve => setTimeout(resolve, 1300));
  }
  for (const row of data.results.bindings) {
    const id = row.player.value.split('/').pop();
    sexByPlayer[id] ??= [];
    if (row.sex) sexByPlayer[id].push(row.sex.value.split('/').pop());
  }
  console.log(`gender evidence batch ${i / 100}: ${group.length} identities`);
}
fs.writeFileSync(`${root}/player-genders.json`, JSON.stringify(sexByPlayer, null, 2));
