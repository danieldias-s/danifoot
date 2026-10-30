// Read-only public provenance collection. Never downloads or executes game binaries.
import { writeFileSync, mkdirSync } from 'node:fs';
import { resolve, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import assert from 'node:assert/strict';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const output = join(root, 'docs/audit-012b/research');
mkdirSync(output, { recursive: true });
const api = 'https://api.github.com/repos/trsthales/desvendando-elifoot-98';
const revision = process.argv[2] ?? '2f7ff68f50bf3af8a57d0baf5a4938f65abf0f03';
const headers = { 'User-Agent': 'Elifoot-012B-research', Accept: 'application/vnd.github+json' };
async function get(url) {
  const response = await fetch(url, { headers });
  if (!response.ok) throw new Error(`${response.status}: ${url}`);
  return { data: await response.json(), url, nextPage: /rel="next"/.test(response.headers.get('link') ?? '') };
}
const [commit, history, issues, forks] = await Promise.all([
  get(`${api}/commits/${encodeURIComponent(revision)}`), get(`${api}/commits?sha=${encodeURIComponent(revision)}&per_page=100`),
  get(`${api}/issues?state=all&per_page=100`), get(`${api}/forks?per_page=100`),
]);
const tree = await get(`${api}/git/trees/${commit.data.sha}?recursive=1`);
assert.equal(tree.data.truncated, false);
for (const list of [history, issues, forks]) {
  assert.ok(Array.isArray(list.data));
  assert.equal(list.nextPage, false);
}
const paths = ['README.md', 'ROADMAP.md', 'docs/parte2.md', 'src/player_evolution.c', 'src/match_engine.c', 'src/team_calc.c'];
const sourceManifest = await Promise.all(paths.map(async path => {
  // A fixed allowlist of six text files; tree entries never become download targets.
  const sourceResponse = await fetch(`https://raw.githubusercontent.com/trsthales/desvendando-elifoot-98/${commit.data.sha}/${path}`, { headers });
  if (!sourceResponse.ok) throw new Error(`Could not read public research text: ${path}`);
  const bytes = Buffer.from(await sourceResponse.arrayBuffer());
  const scratchPath = join(root, '.tools/research-012b/upstream', path);
  mkdirSync(dirname(scratchPath), { recursive: true });
  writeFileSync(scratchPath, bytes);
  const gitBlob = createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex');
  const entry = tree.data.tree.find(row => row.path === path);
  assert.equal(gitBlob, entry.sha, `Research file does not match pinned commit: ${path}`);
  const content = bytes.toString('utf8');
  const lines = content.split(/\r?\n/);
  return { path, bytes: bytes.length, gitBlob, sha256: createHash('sha256').update(bytes).digest('hex'),
    url: `https://github.com/trsthales/desvendando-elifoot-98/blob/${commit.data.sha}/${path}`,
    observations: lines.flatMap((line, index) => /AvaliarRendimento|nivelBaseTime|desempenhoPartida|FUN_17fb_4a41|Fábrica de Craques/.test(line)
      ? [{ line: index + 1, tokens: [...line.matchAll(/AvaliarRendimento|nivelBaseTime|desempenhoPartida|FUN_17fb_4a41|Fábrica de Craques/g)].map(m => m[0]) }] : []) };
}));
const introductions = [];
for (const entry of history.data) {
  const detail = await get(`${api}/commits/${entry.sha}`);
  introductions.push({ sha: entry.sha, date: entry.commit.committer.date,
    message: entry.commit.message, files: detail.data.files.map(f => ({ path: f.filename, status: f.status, sha: f.sha })) });
}
const forkInspections = [];
for (const fork of forks.data) {
  const tip = await get(`https://api.github.com/repos/${fork.full_name}/commits/${fork.default_branch}`);
  const forkTree = await get(`https://api.github.com/repos/${fork.full_name}/git/trees/${tip.data.sha}?recursive=1`);
  forkInspections.push({ repo: fork.full_name, sha: tip.data.sha, treeTruncated: forkTree.data.truncated,
    playerEvolutionBlob: forkTree.data.tree.find(f => f.path === 'src/player_evolution.c')?.sha ?? null,
    paths: forkTree.data.tree.filter(f => f.type === 'blob').map(f => f.path) });
}
const result = {
  collectedAtUtc: new Date().toISOString(), commit: commit.data.sha, commitDate: commit.data.commit.committer.date,
  sourceManifest, treePaths: tree.data.tree.map(({ path, type, sha, size }) => ({ path, type, sha, size })),
  commits: introductions, issues: issues.data.map(({ number, title, body, html_url, state }) => ({ number, title, body, html_url, state })),
  forks: forkInspections, apiPaginationComplete: true,
};
writeFileSync(join(output, 'provenance.json'), JSON.stringify(result, null, 2));
writeFileSync(join(output, 'upstream-history.json'), JSON.stringify({ Commits: result.commits, Issues: result.issues, Forks: result.forks }, null, 2));
process.stdout.write(JSON.stringify({ commit: result.commit, files: sourceManifest.length,
  commits: result.commits.length, issues: result.issues.length, forks: result.forks, verifiedGitBlobs: true }, null, 2) + '\n');
