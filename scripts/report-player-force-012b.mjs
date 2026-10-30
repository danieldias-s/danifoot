import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { resolve, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import assert from 'node:assert/strict';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const audit = join(root, 'docs/audit-012b');
const output = join(audit, 'regression/force-012b');
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const parseLines = path => readFileSync(path, 'utf8').trim().split(/\r?\n/).filter(Boolean).map(line => JSON.parse(line));
const rows = [1, 2, 3, 4].flatMap(d => read(join(output, `summary-division-${d}.json`)));
assert.equal(rows.length, 720);
assert.equal(new Set(rows.map(r => r.Scenario)).size, 720);
const rounds = [1, 2, 3, 4].flatMap(d => parseLines(join(output, `rounds-division-${d}.jsonl`)));
const injuries = [1, 2, 3, 4].flatMap(d => parseLines(join(output, `injuries-division-${d}.jsonl`)));
const probes = read(join(output, 'injury-probe-summary.json'));
const probeEvents = parseLines(join(output, 'injury-probe-events.jsonl'));
assert.equal(probes.length, 80);
assert.equal(probeEvents.length, 80);
assert.equal(rounds.length, 31_680);
for (const row of rounds) {
  assert.equal(row.OriginalObserved, false);
  assert.equal(row.OriginalStrengthBefore, null);
  assert.equal(row.OriginalStrengthAfter, null);
  if (row.ActualEntryMinute === null) assert.equal(row.StrengthAfterEntry, null);
  else assert.equal(row.StrengthAfterEntry, row.StrengthBefore);
  for (const column of ['StrengthAfterWhistle', 'StrengthAfterSettlement'])
    assert.equal(row[column], row.StrengthBefore, `${row.Scenario} ${row.Season}/${row.Round} ${column}`);
  assert.ok(row.StrengthAfter >= 1 && row.StrengthAfter <= 50);
  if (row.StrengthBefore !== row.StrengthAfter) assert.equal(row.Round, 22);
}
for (const row of injuries) assert.equal(row.StrengthBefore, row.StrengthAfter);
for (const row of probes) {
  assert.equal(row.StrengthAfterInjury, row.InitialStrength);
  assert.equal(row.StrengthAfterRecovery, row.InitialStrength);
  assert.equal(row.OriginalObserved, false);
}
for (const row of rows) {
  const peer = rows.find(p => p.Division === row.Division && p.Position === row.Position
    && p.InitialStrength === row.InitialStrength && p.ParticipationPlan === row.ParticipationPlan && p.ClubPosition === 1);
  assert.deepEqual(row.StrengthBySeason, peer.StrengthBySeason);
}
const boundaries = read(join(output, 'published-model-counterexamples.json'));
assert.equal(boundaries.Comparisons, 30_000);
assert.equal(boundaries.Counterexamples.length, 220);
const previous = read(join(root, 'docs/audit-012/force-012/candidate-100-seasons.json'));
const current = read(join(audit, 'regression/force-012/candidate-100-seasons.json'));
assert.deepEqual(current.Distributions, previous.Distributions);
assert.equal(current.FinalStateSignature, previous.FinalStateSignature);

const checkpoint = read(join(audit, 'baseline/checkpoint-012-manifest.json'));
const production = read(join(audit, 'baseline/production-manifest.json'));
const preserved = [...checkpoint.Files, ...production].map(file => {
  const hash = createHash('sha256').update(readFileSync(join(root, file.Path))).digest('hex').toUpperCase();
  assert.equal(hash, file.Sha256, `Preserved 012 file changed: ${file.Path}`);
  return { Path: file.Path, Sha256: hash };
});
assert.equal(execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim(), checkpoint.Head);
assert.equal(execFileSync('git', ['remote', '-v'], { cwd: root, encoding: 'utf8' }).trim(), '');
const results = new Map();
const trx = readdirSync(join(audit, 'tests')).filter(n => /^(regression012b|closing012b).*\.trx$/.test(n));
for (const file of trx) {
  for (const tag of readFileSync(join(audit, 'tests', file), 'utf8').matchAll(/<UnitTestResult\b[^>]*>/g)) {
    const attrs = Object.fromEntries([...tag[0].matchAll(/(\w+)="([^"]*)"/g)].map(m => [m[1], m[2]]));
    assert.equal(attrs.outcome, 'Passed', `${file}: ${attrs.testName}`);
    results.set(attrs.testName, 'Passed');
  }
}
assert.equal(results.size, 276);
const distribution = new Map();
for (const row of rows) row.StrengthBySeason.forEach((strength, index) => {
  const key = `${row.Division}|${row.Position}|${index + 1}`;
  const bucket = distribution.get(key) ?? { Division: row.Division, Position: row.Position, Season: index + 1, Strengths: [] };
  bucket.Strengths.push(strength); distribution.set(key, bucket);
});
const controlledDistributions = [...distribution.values()].map(({ Strengths, ...bucket }) => ({
  ...bucket, Count: Strengths.length, Min: Math.min(...Strengths), Max: Math.max(...Strengths),
  Mean: Strengths.reduce((a, b) => a + b, 0) / Strengths.length,
  Histogram: Object.fromEntries([...new Set(Strengths)].sort((a, b) => a - b)
    .map(s => [s, Strengths.filter(v => v === s).length])),
}));
const plan = {
  Scope: 'ORIGINAL_CAPTURE_PLAN', ExecutionStatus: 'NOT_RUN_NO_LOCAL_ORIGINAL', OriginalExecutions: 0,
  RequiredProvenance: ['ExecutableVersion', 'ExecutableSHA256', 'LicenseContextReference', 'SaveBeforeSHA256', 'RngControlMethod'],
  RequiredStages: ['BEFORE_LINEUP', 'AFTER_LINEUP', 'BEFORE_ENTRY', 'AFTER_ENTRY', 'EACH_INJURY', 'HALFTIME',
    'FINAL_WHISTLE_BEFORE_ASSESSMENT', 'AFTER_ASSESSMENT', 'EACH_ABSENCE', 'RECOVERED', 'SECOND_INJURY'],
  Cases: rows.map(r => ({ Scenario: r.Scenario, InitialStrength: r.InitialStrength, Position: r.Position,
    Division: r.Division, ClubPosition: r.ClubPosition, ParticipationPlan: r.ParticipationPlan,
    OriginalObservations: [], OriginalSeed: null, CloneSeed: r.Seed })),
};
writeFileSync(join(audit, 'original-capture-plan.json'), JSON.stringify(plan, null, 2));
const summary = {
  Status: 'PARTIAL', ProductionBehaviorChanged: false, OriginalExecutions: 0,
  ResearchCommit: read(join(audit, 'research/provenance.json')).commit,
  MatrixScenarios: rows.length, ControlledPlayerSeasons: rows.length * 2,
  ControlledMatches: rows.reduce((sum, r) => sum + r.Matches, 0), RecoveryWarmupMatches: rows.filter(r => r.ParticipationPlan === 'RECOVERED').length,
  RoundObservations: rounds.length, TargetInjuryEvents: injuries.length,
  TargetInjuryEventsByPosition: Object.fromEntries(['A', 'G'].map(p => [p, injuries.filter(r => r.Scenario.includes(`:${p}:`)).length])),
  SelectedInjuryProbeEpisodes: probes.length, SelectedInjuryProbeEvents: probeEvents.length,
  ProbeEventsByPosition: Object.fromEntries(['A', 'G'].map(p => [p, probes.filter(r => r.Position === p).length])),
  NominalLossOnEntry: 0, NominalLossAtInjury: 0, DirectTablePositionEffect: false,
  PublicModelComparisons: boundaries.Comparisons, PublicModelCounterexamples: boundaries.Counterexamples.length,
  DotnetPassingCases: results.size, TrxFiles: trx, ControlledDistributions: controlledDistributions,
  NewFullSeasons: current.SeasonsCompleted, NewFullSeasonMatches: current.TotalMatchesCompleted,
  FullPopulationDistributionsIdenticalTo012: true, AggregateSignatureIdenticalTo012: true,
  AggregateSignature: current.FinalStateSignature, PreservedFiles: preserved,
};
writeFileSync(join(audit, 'verification.json'), JSON.stringify(summary, null, 2));
process.stdout.write(JSON.stringify({ status: summary.Status, originalExecutions: 0, dotnet: results.size,
  scenarios: rows.length, roundObservations: rounds.length, matrixInjuries: injuries.length, selectedProbeInjuries: probeEvents.length,
  completeSeasons: current.SeasonsCompleted, protectedFileEntries: preserved.length }, null, 2) + '\n');
