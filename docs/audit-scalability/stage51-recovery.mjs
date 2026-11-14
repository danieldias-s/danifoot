import assert from 'node:assert/strict';
import { spawn, execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { createWriteStream, mkdirSync, statfsSync, writeFileSync } from 'node:fs';
import { freemem } from 'node:os';
import { join, resolve } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

const [dllArg, outputArg, db, psql] = process.argv.slice(2);
assert.match(db ?? '', /^audit_stage51_[a-z0-9_]+$/);
assert.ok(dllArg && outputArg && psql, 'Uso: node stage51-recovery.mjs API.dll pasta-nova audit_stage51_nome psql.exe');
const output = resolve(outputArg);
mkdirSync(output);
const port = 54986;
const outputFile = join(output, 'result.json');
const connection = `Host=127.0.0.1;Port=54983;Database=${db};Username=audit`;
const sqlArgs = ['-X', '-h', '127.0.0.1', '-p', '54983', '-U', 'audit', '-d', db, '-v', 'ON_ERROR_STOP=1', '-At'];
const sql = statement => execFileSync(psql, [...sqlArgs, '-c', statement], { encoding: 'utf8', timeout: 5000 }).trim();
const children = [];
const stop = new AbortController();
const GiB = 1024 ** 3;
const report = { configuredWorkers: 1, configuredQueue: 1, rooms: [], samples: [] };
const savedReport = () => writeFileSync(outputFile, JSON.stringify(report, null, 2));

function guard() {
    const disk = statfsSync(output);
    assert.ok(freemem() >= 1.5 * GiB && disk.bavail * disk.bsize >= 12 * GiB, 'Limite de RAM ou disco livre atingido');
}

function start(name, executable, args) {
    const child = spawn(executable, args, { cwd: output, windowsHide: true });
    const log = createWriteStream(join(output, `${name}.log`));
    child.stdout.pipe(log, { end: false });
    child.stderr.pipe(log, { end: false });
    child.once('close', () => log.end());
    child.once('error', error => stop.abort(error));
    children.push(child);
    return child;
}

function server(name, disabled) {
    return start(name, 'dotnet', [resolve(dllArg), `--urls=http://127.0.0.1:${port}`, '--environment=Audit',
        '--Elifoot:AuthMode=LocalDev', '--Elifoot:DatabaseProvider=PostgreSQL', '--Elifoot:AutoSeedDefaultWorld=false',
        `--Elifoot:DisableBackgroundClock=${disabled}`, '--Elifoot:ExclusiveWorldAuthority=true',
        '--Elifoot:EnableLocalClockMetrics=true', '--Elifoot:ClockConcurrency=1', '--Elifoot:ClockQueueCapacity=1',
        '--Logging:EventLog:LogLevel:Default=None', '--Logging:LogLevel:Default=Warning',
        `--ConnectionStrings:DefaultConnection=${connection}`]);
}

function exited(child) {
    return child.exitCode !== null || child.signalCode !== null
        ? Promise.resolve() : new Promise(resolve => child.once('exit', resolve));
}

async function terminate(child) {
    child.kill();
    await Promise.race([exited(child), delay(5000).then(() => { throw Error(`Processo ${child.pid} não encerrou`); })]);
}

async function api(path, body) {
    const response = await fetch(`http://127.0.0.1:${port}/api${path}`, {
        method: body === undefined ? 'GET' : 'POST', headers: body == null ? {} : { 'content-type': 'application/json' },
        body: body == null ? null : JSON.stringify(body), signal: AbortSignal.any([stop.signal, AbortSignal.timeout(12000)]),
    });
    assert.ok(response.ok, `${path}: HTTP ${response.status} ${response.ok ? '' : await response.text()}`);
    return response.json();
}

async function poll(read, accept, timeoutMs, label) {
    const until = performance.now() + timeoutMs;
    while (performance.now() < until) {
        guard();
        stop.signal.throwIfAborted();
        const value = await read();
        if (accept(value)) return value;
        await delay(100, undefined, { signal: stop.signal });
    }
    throw Error(`Tempo excedido: ${label}`);
}

async function health(child) {
    await poll(async () => {
        assert.equal(child.exitCode, null, 'API encerrou na inicialização');
        try { return (await api('/health')).dbProvider; } catch { return null; }
    }, provider => provider === 'PostgreSQL', 18000, 'health PostgreSQL');
}

async function joinV2({ id, coachId }) {
    const socket = new WebSocket(`ws://127.0.0.1:${port}/hubs/gameroom`);
    try {
        return await new Promise((resolve, reject) => {
            const timeout = setTimeout(() => reject(Error('Timeout SignalR JoinRoomV2')), 12000);
            socket.onopen = () => {
                socket.send('{"protocol":"json","version":1}\x1e');
                socket.send(JSON.stringify({ type: 1, invocationId: 'join', target: 'JoinRoomV2', arguments: [id, coachId] }) + '\x1e');
            };
            socket.onerror = () => { clearTimeout(timeout); reject(Error('Erro no WebSocket')); };
            socket.onmessage = message => {
                for (const frame of String(message.data).split('\x1e').filter(Boolean)) {
                    const reply = JSON.parse(frame);
                    if (reply.invocationId !== 'join') continue;
                    clearTimeout(timeout);
                    if (reply.error || reply.result?.gameWorldId !== id) reject(Error(reply.error ?? 'Mundo SignalR incorreto'));
                    else resolve(reply.result);
                }
            };
        });
    } finally { socket.close(); }
}

try {
    guard();
    assert.equal(sql("SELECT count(*) FROM information_schema.tables WHERE table_schema='public'"), '0', 'Banco deve estar vazio');
    const seed = server('seed', true);
    await health(seed);
    for (let index = 0; index < 6; index++) {
        const coachId = randomUUID();
        const world = await api('/worlds', { worldName: `Audit Recovery ${index}`, seed: 12001 + index,
            totalClubs: 64, maxHumans: 1, inviteCode: `AUD-R${index}`, tickIntervalMs: 65, pauseTimeoutSeconds: 5 });
        const id = world.gameWorldId;
        const path = `/worlds/${id}`;
        const joined = await api(`${path}/join?coachId=${coachId}&nickname=Recovery${index}&joinUnemployed=true`, null);
        assert.equal(joined.isUnemployed, true);
        const ready = { gameWorldId: id, coachId, expectedRoundVersion: joined.snapshot.roundVersion, idempotencyKey: 'durable-ready' };
        assert.equal((await api(`${path}/ready-unemployed`, ready)).roundStarted, true);
        const initial = await joinV2({ id, coachId });
        assert.equal(initial.roundState, 'RUNNING_FIRST_HALF');
        report.rooms.push({ id, path, coachId, ready, initialEpoch: initial.realtimeEpoch, initialVersion: initial.roundVersion,
            round: initial.roundNumber, season: initial.seasonNumber });
    }
    await terminate(seed);
    savedReport();

    // Allow reads while making a real checkpoint writer wait on PostgreSQL.
    const lock = start('hold-checkpoint', psql, sqlArgs);
    const lockReady = new Promise(resolve => {
        let received = '';
        lock.stdout.on('data', chunk => { received += chunk; if (received.includes('CHECKPOINT_HELD')) resolve(); });
    });
    lock.stdin.write("BEGIN;\nSET LOCAL idle_in_transaction_session_timeout='45s';\nLOCK TABLE ef_worlds IN SHARE MODE;\n\\echo CHECKPOINT_HELD\n");
    await Promise.race([lockReady, delay(5000).then(() => { throw Error('Lock do ensaio indisponível'); })]);
    const interrupted = server('interrupted', false);
    await health(interrupted);
    report.saturated = await poll(async () => {
        const value = await api('/audit/clock');
        if (value.waiting === 1 && value.executing === 1) {
            value.blockedWriters = Number(sql("SELECT count(*) FROM pg_locks WHERE relation='ef_worlds'::regclass AND NOT granted AND mode='RowExclusiveLock'"));
        }
        report.samples.push(value);
        return value;
    }, value => value.active >= 3 && value.waiting === 1 && value.executing === 1 && value.blockedWriters === 1,
    18000, 'fila cheia, pelo menos três mundos aptos e checkpoint bloqueado');
    await terminate(interrupted);
    report.interruptedAt = new Date().toISOString();
    lock.stdin.end('ROLLBACK;\n\\q\n');
    await Promise.race([exited(lock), delay(5000).then(() => { throw Error('Lock não foi liberado'); })]);
    assert.equal(lock.exitCode, 0);
    savedReport();

    const restart = performance.now();
    const replacement = server('replacement', false);
    await health(replacement);
    report.restartMs = performance.now() - restart;
    await Promise.all(report.rooms.map(async room => {
        const current = await joinV2(room);
        assert.notEqual(current.realtimeEpoch, room.initialEpoch);
        assert.ok(current.roundVersion >= room.initialVersion);
        assert.equal(current.roundNumber, room.round);
        const replay = await api(`${room.path}/ready-unemployed`, room.ready);
        assert.equal(replay.roundStarted, false);
        const result = await poll(() => api(`${room.path}/snapshot`), snapshot => snapshot.roundState === 'RESULTS',
            95000, `resultado de ${room.id}`);
        assert.equal(result.gameWorldId, room.id);
        assert.equal(result.seasonNumber, room.season);
        assert.equal(result.roundNumber, room.round);
        assert.equal(result.roundVersion, result.persistedRoundVersion);
        assert.ok(result.matches.length > 0 && result.matches.every(match => match.isFinished));
        room.completed = { version: result.roundVersion, matches: result.matches.map(match => match.matchId), elapsedAfterRestartMs: performance.now() - restart };
        const nextPath = `${room.path}/next-round?expectedSeasonNumber=${room.season}&expectedRoundNumber=${room.round}`;
        const next = await Promise.all([api(nextPath, null), api(nextPath, null)]);
        assert.ok(next.every(snapshot => snapshot.gameWorldId === room.id && snapshot.roundNumber === room.round + 1 &&
            snapshot.roundVersion === result.roundVersion + 1 && snapshot.roundState === 'PREPARING'));
        room.nextVersion = next[0].roundVersion;
    }));
    report.recovered = await api('/audit/clock');
    assert.equal(report.recovered.peakWaiting, 1);
    assert.equal(report.recovered.peakExecuting, 1);
    assert.equal(report.recovered.failed, 0);
    assert.equal(new Set(report.rooms.flatMap(room => room.completed.matches)).size,
        report.rooms.reduce((total, room) => total + room.completed.matches.length, 0));
    report.pass = true;
} catch (error) {
    report.error = String(error.stack ?? error);
    throw error;
} finally {
    savedReport();
    for (const child of children) if (child.exitCode === null && child.signalCode === null) child.kill();
}
