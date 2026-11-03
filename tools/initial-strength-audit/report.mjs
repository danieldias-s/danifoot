import { readFileSync } from 'node:fs';
import { strict as assert } from 'node:assert';

const read = path => readFileSync(path, 'utf8').trim().split(/\r?\n/).map(JSON.parse);
const [before, after] = process.argv.slice(2).map(read);
const scenarios = [...new Set(before.map(c => c.Scenario))];
const clubs = (rows, scenario, division) => rows.filter(c => c.Scenario === scenario && c.Division === division);
const average = values => values.reduce((sum, x) => sum + x, 0) / values.length;
const format = value => value.toFixed(2).replace('.', ',');
const bands = [10, 20, 30, 40, 50];
const strengths = (players, role) => players.filter(p => !role || p.Role === role).map(p => p.Strength);
const averageSquad = club => average(strengths(club.Players));
const range = values => `${Math.min(...values)}–${Math.max(...values)}`;
const histogram = values => bands.map(end => values.filter(n => n > end - 10 && n <= end).length).join(' / ');
const header = (columns) => {
    console.log(`| ${columns.join(' | ')} |\n| ${columns.map(() => '---').join(' | ')} |`);
};
const row = (...columns) => console.log(`| ${columns.join(' | ')} |`);

assert.equal(before.length, after.length);
for (const scenario of scenarios) {
    console.log(`\n### ${scenario}\n`);
    header(['Div.', 'Atletas', 'Mín. / média / máx. antes', 'Mín. / média / máx. depois',
        'Média dos clubes antes → depois', 'XI antes → depois', 'Banco antes → depois']);
    for (let division = 1; division <= 4; division++) {
        const a = clubs(before, scenario, division), b = clubs(after, scenario, division);
        assert.equal(a.length, 8);
        assert.equal(b.length, 8);
        assert.deepEqual(a.map(c => [c.Name, c.Players.length]), b.map(c => [c.Name, c.Players.length]));
        if (['traditional-seed', 'elo', 'elo-manual', 'ordinal', 'unranked'].includes(scenario)
            || (scenario === 'mixed' && division <= 2)) assert.deepEqual(a, b);
        const pa = a.flatMap(c => c.Players), pb = b.flatMap(c => c.Players);
        for (const squad of [pa, pb]) {
            assert.equal(strengths(squad, 'XI').length, 88);
            assert(squad.every(p => Number.isInteger(p.Strength) && p.Strength >= 1 && p.Strength <= 50));
        }
        const describe = ps => {
            const s = strengths(ps);
            return `${Math.min(...s)} / ${format(average(s))} / ${Math.max(...s)}`;
        };
        row(division, pa.length, describe(pa), describe(pb),
            `${format(average(a.map(averageSquad)))} → ${format(average(b.map(averageSquad)))}`,
            `${format(average(strengths(pa, 'XI')))} → ${format(average(strengths(pb, 'XI')))}`,
            `${format(average(strengths(pa, 'bench')))} → ${format(average(strengths(pb, 'bench')))}`);
    }
}

const comparePlayers = c => c.Players.map(p => [p.Name, p.Position, p.Strength]);
for (const c of after.filter(c => c.Scenario === 'elo')) {
    const moved = after.find(m => m.Scenario === 'elo-manual' && m.ShortName === c.ShortName);
    assert.deepEqual(comparePlayers(c), comparePlayers(moved));
}
const pt032 = before.find(c => c.Scenario === 'pt032' && c.ShortName === 'F032');
const screenshot = { A: [32, 31], M: [34, 34, 29, 28], D: [31, 27, 24, 24] };
for (const [position, expected] of Object.entries(screenshot)) {
    assert.deepEqual(strengths(pt032.Players.filter(p => p.Position === position), 'XI').sort((a, b) => b - a), expected);
}

console.log('\n### PT-032: força por faixa e papel no elenco\n');
console.log('Faixas: **1–10 / 11–20 / 21–30 / 31–40 / 41–50**. Reservas = todos os não titulares; banco = os relacionados.\n');
header(['Div.', 'Grupo (n)', 'Faixas antes', 'Faixas depois', 'Mín.–máx. antes → depois', 'Média antes → depois']);
for (let division = 1; division <= 4; division++) {
    const a = clubs(before, 'pt032', division).flatMap(c => c.Players);
    const b = clubs(after, 'pt032', division).flatMap(c => c.Players);
    for (const [label, select] of [
        ['Elenco', p => p], ['XI', p => p.filter(x => x.Role === 'XI')],
        ['Reservas', p => p.filter(x => x.Role !== 'XI')], ['Banco', p => p.filter(x => x.Role === 'bench')],
        ['Fora da relação', p => p.filter(x => x.Role === 'outside')],
    ]) {
        const va = strengths(select(a)), vb = strengths(select(b));
        assert.equal(va.length, vb.length);
        row(division, `${label} (${va.length})`, histogram(va), histogram(vb),
            `${range(va)} → ${range(vb)}`, `${format(average(va))} → ${format(average(vb))}`);
    }
}

console.log('\n### PT-032: os 32 clubes do mundo reproduzido\n');
header(['Div.', 'Clube', 'Atletas', 'Média do elenco antes → depois', 'XI antes → depois', 'Banco antes → depois']);
for (const a of before.filter(c => c.Scenario === 'pt032')) {
    const b = after.find(c => c.Scenario === a.Scenario && c.ShortName === a.ShortName);
    row(a.Division, a.Name, a.Players.length, `${format(averageSquad(a))} → ${format(averageSquad(b))}`,
        ...['XI', 'bench'].map(role => `${format(average(strengths(a.Players, role)))} → ${format(average(strengths(b.Players, role)))}`));
}
