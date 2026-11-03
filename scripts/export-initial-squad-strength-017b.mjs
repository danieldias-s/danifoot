import { strict as assert } from 'node:assert';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const [snapshotPath, previewPath, outputDirectory] = process.argv.slice(2);
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const { world, createdAtUtc } = read(snapshotPath);
const preview = read(previewPath);
const divisions = ['PrimeiraDivisao', 'SegundaDivisao', 'TerceiraDivisao', 'QuartaDivisao'];
const referenceTable = [[50, 50, 50, 46, 44, 42, 40, 39], [38, 37, 36, 34, 32, 30, 28, 27],
    [25, 24, 23, 21, 19, 17, 15, 14], [12, 11, 10, 8, 6, 4, 2, 1]];
const divisionBounds = [[39, 50], [26, 38], [13, 25], [1, 12]];
const positionOrder = ['G', 'D', 'M', 'A'];
const columns = ['WorldId', 'Seed', 'Division', 'InitialPosition', 'ClubId', 'ClubName', 'ReferenceStrength',
    'PlayerId', 'PlayerName', 'PlayerPosition', 'PlayerStrength', 'SquadRole', 'PlayerOrigin'];
const numeric = values => [Math.min(...values), Math.max(...values), values.reduce((a, b) => a + b, 0) / values.length];
const decimal = n => n.toFixed(2).replace('.', ',');
const markdown = value => String(value).replace(/\|/g, '\\|').replace(/[\r\n]/g, ' ');
const csv = value => {
    const text = String(value);
    return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
};
const stats = [];
const details = [];
const playerRows = [];
const invalid = [];

assert.equal(world.seasonNumber, 1);
assert.equal(world.roundNumber, 1);
assert.equal(world.clubs.length, 128);
assert.equal(world.clubs.filter(c => c.divisionTier === 'Distrital').length, 96);
assert.equal(world.catalogOrigin.dataHash, preview.dataHash);
assert.equal(world.seed, preview.seed);
assert.equal(world.players.length, new Set(world.players.map(p => p.id)).size);
const initialClubs = new Map(preview.clubs.map(row => [row.club.name, row]));
for (const club of world.clubs) {
    const squad = world.players.filter(p => p.clubId === club.id);
    const planned = initialClubs.get(club.name);
    assert.ok(planned, club.name);
    assert.deepEqual(squad.map(p => [p.name, p.position, p.strength, p.catalogIdentity.externalPlayerId]),
        planned.club.players.map(p => [p.playerName, p.position, p.strength, p.playerExternalId]), club.name);
}

for (const [index, division] of divisions.entries()) {
    const number = index + 1;
    const divisionClubs = world.clubs.filter(c => c.divisionTier === division)
        .sort((a, b) => a.initialSquadReference.position - b.initialSquadReference.position);
    assert.equal(divisionClubs.length, 8);
    const divisionPlayers = [];
    const priorInvalid = invalid.length;
    details.push(`## ${number}ª divisão`, '');
    for (const [offset, club] of divisionClubs.entries()) {
        const reference = club.initialSquadReference;
        const expected = referenceTable[index][offset];
        const elite = number === 1 && offset < 3;
        const minimum = elite ? 47 : Math.max(divisionBounds[index][0], expected - 1);
        const maximum = Math.min(divisionBounds[index][1], expected + 1);
        assert.deepEqual(reference, { position: offset + 1, strength: expected, minimum, maximum });
        assert.deepEqual(reference, initialClubs.get(club.name).initialSquadReference);
        const starters = new Set(club.preferredStarterIds);
        assert.equal(starters.size, 11);
        const players = world.players.filter(p => p.clubId === club.id)
            .sort((a, b) => positionOrder.indexOf(a.position) - positionOrder.indexOf(b.position)
                || a.strength - b.strength || a.name.localeCompare(b.name, 'pt-BR') || a.id.localeCompare(b.id));
        assert.ok(players.length >= 17 && players.length <= 23, club.name);
        assert.ok(players.some(p => p.position === 'G'));
        assert.ok(players.filter(p => p.position !== 'G').length >= 10);
        assert.equal(players.filter(p => starters.has(p.id)).length, 11);
        const strengths = players.map(p => p.strength);
        if (elite) assert.deepEqual([...new Set(strengths)].sort((a, b) => a - b), [47, 48, 49, 50]);
        const [min, max, average] = numeric(strengths);
        details.push(`### ${reference.position}º · ${markdown(club.name)}`, '',
            `Referência **${expected}** · ${players.length} jogadores · mínima ${min} · máxima ${max} · média ${decimal(average)}`,
            '', '| Jogador | Posição | Força | Escalação | Origem |', '| --- | --- | ---: | --- | --- |');
        for (const player of players) {
            if (!Number.isInteger(player.strength) || player.strength < minimum || player.strength > maximum)
                invalid.push(player.id);
            assert.ok(positionOrder.includes(player.position));
            const role = starters.has(player.id) ? 'Titular' : 'Reserva';
            const origin = player.catalogIdentity.origin;
            const kind = origin === 'FICTIONAL_GENERATED' ? 'Fictício' : 'Real';
            details.push(`| ${markdown(player.name)} | ${player.position} | ${player.strength} | ${role} | ${kind} |`);
            playerRows.push([world.id, world.seed, number, reference.position, club.id, club.name, expected,
                player.id, player.name, player.position, player.strength, role, origin]);
        }
        details.push('');
        divisionPlayers.push(...strengths);
    }
    const [min, max, average] = numeric(divisionPlayers);
    stats.push({ division: number, clubs: divisionClubs.length, players: divisionPlayers.length,
        min, max, average, violations: invalid.length - priorInvalid });
}

assert.equal(invalid.length, 0, `Forças fora das faixas: ${invalid.join(', ')}`);
const summary = ['| Divisão | Clubes | Atletas | Mínima | Máxima | Média | Violações |',
    '| --- | ---: | ---: | ---: | ---: | ---: | ---: |',
    ...stats.map(s => `| ${s.division}ª | ${s.clubs} | ${s.players} | ${s.min} | ${s.max} | ${decimal(s.average)} | ${s.violations} |`)];
const report = ['# Força inicial dos elencos 017B', '', `**${world.name}**`, '', `- World ID \`${world.id}\``,
    `- Seed \`${world.seed}\``, `- Criação em \`${createdAtUtc}\``, `- Hash da prévia \`${preview.dataHash}\``,
    `- 32 clubes de liga e ${playerRows.length} jogadores conferidos no snapshot PostgreSQL. O mundo tem 96 distritais.`, '',
    'Os valores e a escalação vêm da primeira gravação em `ef_worlds`. A conferência comparou o nome, a posição, a força e o ID de origem dos atletas com a prévia confirmada.',
    'A coluna Reserva inclui qualquer jogador fora do XI inicial. A origem é a do atleta. O campeonato usa clubes reais e elencos simulados autorizados.', '',
    ...summary, '', ...details];
mkdirSync(outputDirectory, { recursive: true });
writeFileSync(join(outputDirectory, 'INITIAL_SQUAD_STRENGTH_017B.md'), report.join('\n'));
writeFileSync(join(outputDirectory, 'INITIAL_SQUAD_STRENGTH_017B.csv'),
    '\uFEFF' + [columns, ...playerRows].map(row => row.map(csv).join(',')).join('\r\n') + '\r\n');
console.log(JSON.stringify({ worldId: world.id, players: playerRows.length, stats }, null, 2));
