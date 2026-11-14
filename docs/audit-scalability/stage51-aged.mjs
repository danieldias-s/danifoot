import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { statfsSync, writeFileSync } from 'node:fs';
import { freemem } from 'node:os';
import { dirname } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

const [world, output, roundsArg = '4', historyFloorArg = '10'] = process.argv.slice(2);
assert.match(world ?? '', /^[\da-f-]{36}$/i);
assert.ok(output);
const roundCount = Number(roundsArg);
const historyFloor = Number(historyFloorArg);
assert.ok(Number.isInteger(roundCount) && roundCount >= 1 && roundCount <= 18);
assert.ok(Number.isInteger(historyFloor) && historyFloor >= 0);
const path = `/worlds/${world}`;
const stop = new AbortController();
const samples = [], rounds = [];
const report = { world, roundCount, historyFloor, samples, rounds };
const write = () => writeFileSync(output, JSON.stringify(report, null, 2));
const GiB = 1024 ** 3;

async function api(path, data) {
    const response = await fetch(`http://127.0.0.1:54985/api${path}`, {
        method: data === undefined ? 'GET' : 'POST',
        headers: data == null ? undefined : { 'content-type': 'application/json' },
        body: data == null ? undefined : JSON.stringify(data),
        signal: AbortSignal.any([stop.signal, AbortSignal.timeout(45000)]),
    });
    assert.ok(response.ok, `${path}: ${response.status} ${response.ok ? '' : await response.text()}`);
    return response.json();
}

let phase = 'initial';
async function measure() {
    const disk = statfsSync(dirname(output));
    const sample = { at: new Date().toISOString(), phase, ...await api('/audit/clock') };
    samples.push(sample);
    write();
    if (freemem() < 1.5 * GiB || disk.bavail * disk.bsize < 12 * GiB || sample.rss > 4 * GiB)
        stop.abort(Error('Limite de RAM ou disco atingido no ensaio histórico'));
    return sample;
}

async function observe() {
    while (!stop.signal.aborted) {
        await delay(2000, undefined, { signal: stop.signal });
        await measure();
    }
}

let monitor;
try {
    assert.equal((await api('/health')).dbProvider, 'PostgreSQL');
    await measure();
    monitor = observe().catch(error => stop.abort(error));
    phase = 'cold-read';
    let snapshot = await api(`${path}/snapshot`);
    assert.equal(snapshot.gameWorldId, world);
    assert.ok(snapshot.seasonNumber > historyFloor, 'Fixture tem menos temporadas do que o ensaio exige');
    assert.equal(snapshot.roundState, 'PREPARING');
    report.season = snapshot.seasonNumber;
    const people = snapshot.readyCounter.participants;
    assert.ok(people.every(coach => coach.isUnemployed), 'Fixture precisa de treinadores sem clube');
    const coaches = people.map(coach => coach.coachId);
    if (!coaches.length) {
        const coachId = randomUUID();
        const joined = await api(`${path}/join?coachId=${coachId}&nickname=Auditoria%20hist%C3%B3rica&joinUnemployed=true`, null);
        assert.ok(joined.isUnemployed);
        coaches.push(coachId);
        snapshot = joined.snapshot;
    }
    for (let i = 0; i < roundCount; i++) {
        phase = `round-${i + 1}`;
        const start = performance.now();
        const number = snapshot.roundNumber;
        for (const coachId of coaches) {
            await api(`${path}/ready-unemployed`, { gameWorldId: world, coachId,
                expectedRoundVersion: snapshot.roundVersion, idempotencyKey: `aged-${snapshot.seasonNumber}-${number}` });
            snapshot = await api(`${path}/snapshot`);
        }
        let calendarRead = false;
        const continued = new Set();
        while (snapshot.roundState !== 'RESULTS') {
            assert.ok(performance.now() - start < 210000, 'Rodada histórica excedeu 210 s');
            const pause = snapshot.activePauseWindow?.pauseWindowId;
            if (pause && !continued.has(pause)) {
                await Promise.all(coaches.map(coachId => api(`${path}/continue`, { gameWorldId: world, coachId,
                    pauseWindowId: pause, idempotencyKey: `continue-${pause}` })));
                continued.add(pause);
            }
            if (!calendarRead && snapshot.currentMinute >= 15) {
                await Promise.all([api(`${path}/standings/PrimeiraDivisao`), api(`${path}/clubs`)]);
                calendarRead = true;
            }
            await delay(900, undefined, { signal: stop.signal });
            snapshot = await api(`${path}/snapshot`);
        }
        assert.equal(snapshot.roundNumber, number);
        assert.equal(snapshot.roundVersion, snapshot.persistedRoundVersion);
        rounds.push({ number, version: snapshot.roundVersion, elapsedMs: performance.now() - start });
        if (i + 1 < roundCount) snapshot = await api(`${path}/next-round?expectedSeasonNumber=${report.season}&expectedRoundNumber=${number}`, null);
    }
    phase = 'idle';
    await measure();
    await delay(60000, undefined, { signal: stop.signal });
    report.afterExpiry = await measure();
    assert.equal(report.afterExpiry.cacheRetained, 0);
    phase = 'reopen';
    const recovered = await api(`${path}/snapshot`);
    assert.equal(recovered.roundVersion, rounds.at(-1).version);
    assert.equal(recovered.persistedRoundVersion, recovered.roundVersion);
    const persistence = await fetch('http://127.0.0.1:54985/api/audit/persistence', { signal: AbortSignal.timeout(5000) });
    if (persistence.ok) report.persistence = await persistence.json();
    report.pass = true;
} catch (error) {
    report.error = String(error.stack ?? error);
    throw error;
} finally {
    stop.abort();
    await monitor;
    write();
}
