import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { resolve, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import assert from 'node:assert/strict';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const audit = join(root, 'docs', 'audit-012');
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const before = read(join(audit, 'force-012', 'baseline-100-seasons.json'));
const after = read(join(audit, 'force-012', 'candidate-100-seasons.json'));
assert.equal(after.SeasonsCompleted, 100);
assert.equal(after.TotalMatchesCompleted, 35100);
assert.equal(before.FinalStateSignature, after.FinalStateSignature);
assert.deepEqual(before.Distributions, after.Distributions);
const matrix = read(join(audit, 'regression', 'force-012', 'force-one-summary.json'));
assert.equal(matrix.length, 18);
for (const row of matrix) {
  const peer = matrix.find(p => p.Participation === row.Participation && p.Position === 1);
  assert.deepEqual(row.StrengthBySeason, peer.StrengthBySeason);
}
const baseline = read(join(audit, 'baseline', 'manifest.json'));
const protectedNames = [
  'MatchEngine.cs', 'AiManagerBrain.cs', 'MarketAndEconomyEngine.cs', 'TacticsAndSquadRules.cs',
  'CoachCareerEngine.cs', 'WorldSeedFactory.cs', 'DeterministicRng.cs', 'MultiplayerRoundOrchestrator.cs',
  'EfWorldRepository.cs', 'ElifootDbContext.cs',
];
const protectedFiles = baseline.Files.filter(f => protectedNames.some(n => f.Path.endsWith(`\\${n}`))
  || f.Path.startsWith('docs\\'));
const preservation = protectedFiles.map(f => {
  const hash = createHash('sha256').update(readFileSync(join(root, f.Path))).digest('hex').toUpperCase();
  return { Path: f.Path, Before: f.Sha256, After: hash, Identical: f.Sha256 === hash };
});
assert.ok(preservation.every(p => p.Identical));
const changedTrackedDocs = execFileSync('git', ['diff', '--name-only', '--', 'docs'], { cwd: root, encoding: 'utf8' }).trim();
assert.equal(changedTrackedDocs, '', 'A historical tracked document was modified');
assert.equal(execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim(), baseline.Head);
assert.equal(execFileSync('git', ['remote', '-v'], { cwd: root, encoding: 'utf8' }).trim(), '');

const results = new Map();
const trxFiles = readdirSync(join(audit, 'tests')).filter(n => /^(regression012|force012-final).*\.trx$/.test(n));
for (const file of trxFiles.sort()) {
  const text = readFileSync(join(audit, 'tests', file), 'utf8');
  for (const match of text.matchAll(/<UnitTestResult\b[^>]*>/g)) {
    const attrs = Object.fromEntries([...match[0].matchAll(/([\w]+)="([^"]*)"/g)].map(m => [m[1], m[2]]));
    assert.equal(attrs.outcome, 'Passed', `${file}: ${attrs.testName}`);
    results.set(attrs.testName, attrs.outcome);
  }
}
assert.ok(results.size > 238);
const summary = {
  Status: 'PARTIAL', HistoricalFidelityConfirmed: false, HeadPreserved: baseline.Head,
  Seed: after.Seed, BaselineSeasons: 100, CandidateSeasons: 100,
  BaselineMatches: before.TotalMatchesCompleted, CandidateMatches: after.TotalMatchesCompleted,
  Signature: after.FinalStateSignature, IdenticalGameplaySignature: true,
  IdenticalDistributionsByDivisionAndPosition: true, DistributionGroups: after.Distributions.length,
  InitialPopulation: after.Distributions.filter(g => g.Phase === 'initial').reduce((sum, g) => sum + g.Count, 0),
  FinalDistribution: after.Distributions.filter(g => g.Season === 101).map(({ Histogram, ...rest }) => ({ ...rest, At50: Histogram['50'] ?? 0 })),
  ForceOneMatrixScenarios: matrix.length, ControlledPlayerYears: matrix.length * 30,
  ControlledMatches: matrix.reduce((sum, row) => sum + row.Games, 0),
  ControlledRoundRecords: readFileSync(join(audit, 'regression', 'force-012', 'force-one-rounds.jsonl'), 'utf8').trim().split(/\r?\n/).length,
  DotnetUniquePassingCases: results.size, TrxFiles: trxFiles,
  PreservedFiles: preservation.length, Preservation: preservation,
};
writeFileSync(join(audit, 'verification.json'), JSON.stringify(summary, null, 2));
process.stdout.write(JSON.stringify({
  status: summary.Status, signature: summary.Signature, dotnet: results.size,
  seasonsCompared: 200, matchesCompared: 70200, distributions: summary.DistributionGroups,
  controlledMatches: summary.ControlledMatches, protectedFiles: preservation.length,
}, null, 2) + '\n');
