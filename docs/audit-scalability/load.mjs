import { spawn } from 'node:child_process';
import { createWriteStream, mkdirSync, readFileSync, statfsSync, statSync, writeFileSync } from 'node:fs';
import { randomUUID } from 'node:crypto';
import { freemem, availableParallelism } from 'node:os';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as delay } from 'node:timers/promises';

const [dllArg, rootArg, ...tiers] = process.argv.slice(2);
if (!dllArg || !rootArg || !tiers.length) throw Error('Uso: node load.mjs API.dll pasta-nova 10 50 100 500 1000');
const dll = resolve(dllArg);
const root = resolve(rootArg);
mkdirSync(root);
const port = Number(process.env.AUDIT_PORT ?? 54982);
const base = `http://127.0.0.1:${port}/api`;
const MiB = 1024 ** 2;
const GiB = 1024 ** 3;
const stop = new AbortController();
const results = [];
const pgConnection = process.env.AUDIT_PG;
if (pgConnection && (!/^Host=127\.0\.0\.1;Port=54983;Database=audit_[a-z0-9_]+;/i.test(pgConnection))) {
    throw Error('Use o cluster de auditoria em loopback, porta 54983 e um banco audit_* novo');
}
const provider = pgConnection ? 'PostgreSQL' : (process.env.AUDIT_PROVIDER ?? 'Sqlite');
if (!['PostgreSQL', 'Sqlite', 'InMemory'].includes(provider)) throw Error('Provedor inválido');
const readConcurrency = Number(process.env.AUDIT_IO_CONCURRENCY ?? 100);
const ioConcurrency = Number(process.env.AUDIT_IO_CONCURRENCY ?? 50);
if (!Number.isSafeInteger(ioConcurrency) || ioConcurrency < 1 || ioConcurrency > 100)
    throw Error('AUDIT_IO_CONCURRENCY deve estar entre 1 e 100');
const sqliteBytes = (db) => Object.fromEntries(['', '-wal', '-shm'].map(suffix => {
    try { return [suffix || 'db', statSync(db + suffix).size]; }
    catch (error) { if (error.code === 'ENOENT') return [suffix || 'db', 0]; throw error; }
}));

async function request(path, body, post = body !== undefined) {
    const start = performance.now();
    const response = await fetch(base + path, {
        method: post ? 'POST' : 'GET',
        headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: AbortSignal.any([stop.signal, AbortSignal.timeout(30000)]),
    });
    const payload = await response.text();
    const ms = performance.now() - start;
    if (!response.ok) throw Error(`HTTP ${response.status} em ${path.split('?')[0]}: ${payload.slice(0, 240)}`);
    return { value: JSON.parse(payload), ms, bytes: Buffer.byteLength(payload) };
}

function readSamples(path) {
    try {
        return readFileSync(path, 'utf8').split('\n').filter(Boolean).flatMap(line => {
            try { return [JSON.parse(line)]; } catch { return []; }
        });
    } catch (error) { if (error.code === 'ENOENT') return []; throw error; }
}

function percentiles(values) {
    const sorted = values.toSorted((a, b) => a - b);
    const p = q => sorted[Math.ceil(q * sorted.length) - 1] ?? null;
    return { p50: p(.50), p95: p(.95), p99: p(.99), max: sorted.at(-1) ?? null };
}

function resources(path, start, end) {
    const samples = readSamples(path);
    const first = samples.findLast(x => x.ms <= start);
    const last = samples.find(x => x.ms >= end);
    if (!first || !last || last.ms <= first.ms) return null;
    const seconds = (last.ms - first.ms) / 1000;
    const window = samples.filter(x => x.ms >= first.ms && x.ms <= last.ms);
    const rate = k => (last[k] - first[k]) / seconds;
    return {
        seconds, cpuSec: last.cpuSec - first.cpuSec, cpuCores: rate('cpuSec'),
        meanRssMiB: window.reduce((sum, sample) => sum + sample.rss, 0) / window.length / MiB,
        peakRssMiB: Math.max(...window.map(x => x.rss)) / MiB,
        peakPrivateMiB: Math.max(...window.map(x => x.privateBytes)) / MiB,
        readOpsSec: rate('reads'), writeOpsSec: rate('writes'),
        readMiBSec: rate('readBytes') / MiB, writeMiBSec: rate('writeBytes') / MiB,
    };
}

async function phase(name, items, concurrency, action, context) {
    const start = Date.now();
    const latencies = [], http = [], errors = [];
    const byRoute = new Map();
    let bytes = 0, index = 0;
    await Promise.all(Array.from({ length: Math.min(concurrency, items.length) }, async () => {
        while (index < items.length && !stop.signal.aborted) {
            const item = items[index++];
            const before = performance.now();
            try {
                await action(item, async (path, body, post = body !== undefined) => {
                    const route = (post ? 'POST ' : 'GET ') + path.split('?')[0]
                        .replace(/\b[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}\b/gi, ':id');
                    const bucket = byRoute.get(route) ?? { times: [], errors: 0 };
                    byRoute.set(route, bucket);
                    let response;
                    try { response = await request(path, body, post); }
                    catch (error) { bucket.errors++; throw error; }
                    http.push(response.ms);
                    bucket.times.push(response.ms);
                    bytes += response.bytes;
                    return response.value;
                });
                latencies.push(performance.now() - before);
            } catch (error) {
                errors.push([String(error), error.cause?.code].filter(Boolean).join(' '));
                if (name === 'seed' || errors.length >= Math.max(3, Math.ceil(items.length * .05))) {
                    stop.abort(Error(`${name}: limite de erros atingido`));
                }
            }
        }
    }));
    const end = Date.now();
    await delay(320);
    const seconds = (end - start) / 1000;
    const report = {
        name, target: items.length, concurrency, seconds, ok: latencies.length,
        errors: errors.length, examples: errors.slice(0, 8),
        opsSec: latencies.length / seconds, httpResponses: http.length, responseBytes: bytes,
        latencyMs: percentiles(latencies), httpMs: percentiles(http),
        httpByRoute: Object.fromEntries([...byRoute].map(([route, bucket]) => [route,
            { ok: bucket.times.length, errors: bucket.errors, ms: percentiles(bucket.times) }])),
        resources: resources(context.samples, start, end), sqlite: sqliteBytes(context.db),
    };
    context.result.phases.push(report);
    console.log(JSON.stringify({ worlds: context.result.worlds, ...report }));
    return report;
}

async function run(worldCount) {
    const dir = join(root, String(worldCount));
    mkdirSync(dir);
    const db = join(dir, 'worlds.db');
    const samples = join(dir, 'process.jsonl');
    const clubs = Number(process.env.AUDIT_CLUBS ?? 64);
    const seasons = Number(process.env.AUDIT_SEASONS ?? 0);
    const roundOnly = process.env.AUDIT_ROUND_ONLY === '1';
    const useClock = process.env.AUDIT_CLOCK === '1';
    const result = { worlds: worldCount, clubs, phases: [], provider, seasons: [], hub: [], roundOnly, useClock };
    const context = { db, samples, result };
    results.push(result);
    const connection = pgConnection ?? `Data Source=${db};Default Timeout=10`;
    const driverLimit = Number(process.env.AUDIT_ROUND_CONCURRENCY ?? 50);
    const subscribers = Number(process.env.AUDIT_HUB_SUBSCRIBERS ?? worldCount);
    const roundsPerWorld = Number(process.env.AUDIT_CLOCK_ROUNDS ?? 1);
    if (!Number.isInteger(subscribers) || subscribers < 0) throw Error('Quantidade de observadores inválida');
    if (!Number.isInteger(roundsPerWorld) || roundsPerWorld < 1 || roundsPerWorld > 20)
        throw Error('Quantidade de rodadas por mundo inválida');
    const server = spawn('dotnet', [dll, `--urls=http://127.0.0.1:${port}`, '--environment=Audit',
        '--Elifoot:AuthMode=LocalDev', `--Elifoot:DatabaseProvider=${provider}`, '--Elifoot:AutoSeedDefaultWorld=false',
        `--Elifoot:DisableBackgroundClock=${!useClock}`, `--ConnectionStrings:DefaultConnection=${connection}`,
        `--Elifoot:ClockConcurrency=${process.env.AUDIT_CLOCK_WORKERS ?? 1}`,
        `--Elifoot:ClockQueueCapacity=${process.env.AUDIT_CLOCK_QUEUE ?? 64}`,
        `--Elifoot:InactiveWorldCacheSeconds=${process.env.AUDIT_CACHE_SECONDS ?? 120}`,
        `--Elifoot:EnableLocalClockMetrics=${process.env.AUDIT_CLOCK_METRICS === '1'}`,
        `--Elifoot:ExclusiveWorldAuthority=${process.env.AUDIT_AUTHORITY === '1'}`,
        '--Catalog013A:Enabled=false', '--Catalog016A:Enabled=false', '--Catalog016B:Enabled=false',
        '--Logging:LogLevel:Default=Warning', '--Logging:EventLog:LogLevel:Default=None'], { cwd: dir, windowsHide: true });
    server.stdout.pipe(createWriteStream(join(dir, 'stdout.log')));
    server.stderr.pipe(createWriteStream(join(dir, 'stderr.log')));
    let sampler, diskSampler, guard;
    try {
        // A tier starts from a new database. Refuse to seed through an exited server.
        for (let attempt = 0; ; attempt++) {
            if (attempt === 100 || server.exitCode !== null) throw Error('API isolada indisponível');
            try {
                if ((await request('/health')).value.dbProvider === provider) break;
            } catch { await delay(250); }
        }
        sampler = spawn('pwsh.exe', ['-NoProfile', '-File',
            fileURLToPath(new URL('sample.ps1', import.meta.url)), '-ServerPid', String(server.pid),
            '-OutputPath', samples], { windowsHide: true });
        sampler.stderr.pipe(createWriteStream(join(dir, 'sampler.log')));
        if (process.env.AUDIT_SAMPLE_DISK === '1') {
            diskSampler = spawn('pwsh.exe', ['-NoProfile', '-File',
                fileURLToPath(new URL('sample-disk.ps1', import.meta.url)), '-OutputPath', join(dir, 'disk.jsonl')],
            { windowsHide: true });
            diskSampler.stderr.pipe(createWriteStream(join(dir, 'disk-sampler.log')));
        }
        for (let tries = 0; tries < 30 && !readSamples(samples).length; tries++) await delay(250);
        result.idle = readSamples(samples).at(-1);
        if (!result.idle) throw Error('Telemetria de processo indisponível');
        let maxWalBytes = 0;
        guard = setInterval(() => {
            const current = readSamples(samples).at(-1);
            const disk = statfsSync(root);
            maxWalBytes = Math.max(maxWalBytes, sqliteBytes(db)['-wal']);
            const availableRam = freemem();
            const availableDisk = disk.bavail * disk.bsize;
            const ramFloor = worldCount <= 10 ? 1.5 * GiB : 2 * GiB;
            if (availableRam < ramFloor || availableDisk < 12 * GiB ||
                current?.privateBytes > 6 * GiB || maxWalBytes > 2 * GiB) {
                stop.abort(Error(`Limite atingido: RAM livre=${availableRam}, disco livre=${availableDisk}, RAM privada=${current?.privateBytes}, WAL=${maxWalBytes}`));
            }
        }, 500);
        const ids = [];
        const jobs = Array.from({ length: worldCount }, (_, index) => index);
        const seed = await phase('seed', jobs, 2, async (index, call) => {
            const coachId = randomUUID();
            const world = await call('/worlds', {
                worldName: `Audit ${index}`, totalClubs: clubs, maxHumans: 1,
                seed: 199801 + index, inviteCode: `AUD${index}`,
            });
            const path = `/worlds/${world.gameWorldId}`;
            const joined = await call(`${path}/join?coachId=${coachId}&nickname=Audit&joinUnemployed=true`, undefined, true);
            if (!joined.isUnemployed) throw Error('Fixture exige observador sem clube');
            ids[index] = { path, world, coachId };
        }, context);
        if (seed.ok !== worldCount) throw stop.signal.reason ?? Error('Provisionamento incompleto');
        async function playRound(item, call) {
            const before = await call(item.path + '/snapshot');
            const ready = await call(item.path + '/ready-unemployed', {
                coachId: item.coachId, gameWorldId: before.gameWorldId,
                expectedRoundVersion: before.roundVersion, idempotencyKey: `ready-${before.roundVersion}`,
            });
            if (!ready.roundStarted) throw Error('Rodada não iniciou');
            let snapshot = before;
            for (let step = 0; step < 12; step++) {
                snapshot = await call(item.path + '/tick?ticks=125', undefined, true);
                if (snapshot.gameWorldId !== before.gameWorldId) throw Error('Mundo trocado');
                if (snapshot.roundState === 'RESULTS') break;
                if (!snapshot.activePauseWindow) throw Error(`Estado inesperado: ${snapshot.roundState}`);
                await call(item.path + '/continue', {
                    gameWorldId: before.gameWorldId, coachId: item.coachId,
                    pauseWindowId: snapshot.activePauseWindow.pauseWindowId, idempotencyKey: `continue-${before.roundVersion}-${step}`,
                });
            }
            if (snapshot.roundState !== 'RESULTS' || !snapshot.matches.every(match => match.isFinished)) {
                throw Error('Rodada não concluída');
            }
            const transition = before.roundNumber === before.totalRoundsInSeason;
            const saveStart = performance.now();
            const next = await call(item.path + `/next-round?expectedSeasonNumber=${before.seasonNumber}&expectedRoundNumber=${before.roundNumber}`, undefined, true);
            if (next.roundNumber !== (transition ? 1 : before.roundNumber + 1) ||
                next.seasonNumber !== before.seasonNumber + Number(transition)) throw Error('Avanço fora da rodada esperada');
            return { next, transitionMs: transition ? performance.now() - saveStart : null };
        }
        if (seasons) {
            const { DatabaseSync } = await import('node:sqlite');
            function snapshotSize(season, transitionMs, durationMs) {
                const sqlite = new DatabaseSync(db, { readOnly: true });
                try {
                    const json = sqlite.prepare('SELECT SnapshotJson FROM ef_worlds').get().SnapshotJson;
                    result.seasons.push({ season, transitionMs, durationMs, files: sqliteBytes(db),
                        snapshotBytes: Buffer.byteLength(json), components: components(JSON.parse(json)),
                        ledger: sqlite.prepare('SELECT SeasonNumber AS season, count(*) AS rows FROM ef_financial_ledger GROUP BY 1').all(),
                        pages: sqlite.prepare('SELECT name, sum(pgsize) AS bytes, sum(unused) AS unused FROM dbstat GROUP BY name ORDER BY bytes DESC').all(),
                        freelist: sqlite.prepare('PRAGMA freelist_count').get().freelist_count });
                    console.log(JSON.stringify({ clubs, season, durationMs, transitionMs, snapshotBytes: Buffer.byteLength(json) }));
                } finally { sqlite.close(); }
            }
            snapshotSize(0, null, 0);
            let seasonStart = performance.now();
            for (let completed = 0; completed < seasons;) {
                const { next, transitionMs } = await playRound(ids[0], async (...args) => (await request(...args)).value);
                if (transitionMs !== null) {
                    completed++;
                    snapshotSize(next.seasonNumber - 1, transitionMs, performance.now() - seasonStart);
                    seasonStart = performance.now();
                }
            }
            result.maxWalBytes = maxWalBytes;
            return;
        }
        const repeat = Array.from({ length: Math.max(4, Math.ceil(200 / worldCount)) }, () => ids).flat();
        const read = roundOnly || stop.signal.aborted ? null : await phase('read', repeat, Math.min(worldCount, readConcurrency), (item, call) => call(item.path + '/snapshot'), context);
        const nav = roundOnly || stop.signal.aborted ? null : await phase('navigation', repeat, Math.min(worldCount, ioConcurrency), async (item, call) => {
            await call('/worlds');
            await call(item.path + '/clubs');
            await call(item.path + '/standings/PrimeiraDivisao');
        }, context);
        const write = roundOnly || stop.signal.aborted ? null : await phase('write', ids, Math.min(worldCount, ioConcurrency), async (item, call) => {
            const coachId = item.coachId;
            const reply = await call(`${item.path}/join?coachId=${coachId}&nickname=Audit&joinUnemployed=true`, undefined, true);
            if (reply.coachId !== coachId) throw Error('Identidade inesperada no rejoin');
        }, context);
        const subscribed = new Set(ids.slice(0, subscribers));
        const runRound = useClock ? async (item, call) => {
            for (let i = 0; i < roundsPerWorld; i++) {
                const snapshot = subscribed.has(item) ? await playWithHub(item, call) : await playWithPolling(item, call);
                if (subscribed.has(item)) result.hub.push(snapshot);
                if (i + 1 < roundsPerWorld) {
                    const next = await call(item.path + `/next-round?expectedSeasonNumber=${snapshot.seasonNumber}&expectedRoundNumber=${snapshot.roundNumber}`, undefined, true);
                    if (next.roundNumber !== snapshot.roundNumber + 1)
                        throw Error('Avanço durante o ensaio de rodadas sucessivas divergiu');
                }
            }
        } : playRound;
        result.roundsPerWorld = roundsPerWorld;
        if (process.env.AUDIT_CLOCK_METRICS === '1')
            result.clockBefore = (await request('/audit/clock')).value;
        if (process.env.AUDIT_PERSISTENCE_METRICS === '1')
            result.persistenceBefore = (await request('/audit/persistence')).value;
        const rounds = stop.signal.aborted ? null : await phase('round', ids, Math.min(worldCount, driverLimit),
            runRound, context);
        if (!stop.signal.aborted && process.env.AUDIT_CLOCK_METRICS === '1') {
            if (process.env.AUDIT_PERSISTENCE_METRICS === '1')
                result.persistence = (await request('/audit/persistence')).value;
            result.clock = (await request('/audit/clock')).value;
            if (process.env.AUDIT_MEASURE_IDLE === '1') {
                await delay(Number(process.env.AUDIT_CLOCK_IDLE_MS ?? 1500));
                result.clockIdle = (await request('/audit/clock')).value;
                if (process.env.AUDIT_CACHE_REJOIN === '1') {
                    const idle = ids[0];
                    const snapshot = (await request(idle.path + '/snapshot')).value;
                    if (snapshot.roundState !== 'RESULTS' || snapshot.roundVersion !== snapshot.persistedRoundVersion)
                        throw Error('Mundo reativado não preservou o resultado durável');
                    const next = (await request(idle.path + `/next-round?expectedSeasonNumber=${snapshot.seasonNumber}&expectedRoundNumber=${snapshot.roundNumber}`,
                        undefined, true)).value;
                    if (next.roundNumber !== snapshot.roundNumber + 1 || next.roundVersion !== snapshot.roundVersion + 1)
                        throw Error('Mundo reativado avançou incorretamente');
                    result.cacheRejoin = { before: snapshot.roundVersion, after: next.roundVersion };
                }
            }
        }
        result.maxWalBytes = maxWalBytes;
        result.end = readSamples(samples).at(-1);
        if (read?.httpMs.p95 > 2000 || nav?.httpMs.p95 > 2000 || write?.httpMs.p95 > 5000 ||
            rounds?.latencyMs.p95 > (useClock ? 90000 : 25000) ||
            [read, nav, write, rounds].filter(Boolean).some(p => p.errors / p.target >= .05)) {
            stop.abort(Error('Limite de latência ou erro ultrapassado neste nível'));
        }
    } finally {
        clearInterval(guard);
        diskSampler?.kill();
        sampler?.kill();
        server.kill();
        result.finalFiles = sqliteBytes(db);
    }
}

async function playWithPolling(item, call) {
    const before = await call(item.path + '/snapshot');
    await call(item.path + '/ready-unemployed', {
        coachId: item.coachId, gameWorldId: before.gameWorldId,
        expectedRoundVersion: before.roundVersion, idempotencyKey: `poll-ready-${before.seasonNumber}-${before.roundNumber}`,
    });
    const end = performance.now() + 90000;
    while (!stop.signal.aborted && performance.now() < end) {
        await delay(1000);
        const current = await call(item.path + '/snapshot');
        if (current.roundState === 'RESULTS') {
            if (current.roundVersion !== current.persistedRoundVersion) throw Error('Resultado sem versão durável');
            return current;
        }
    }
    throw Error('Relógio com polling excedeu 90 s');
}

async function playWithHub(item, call) {
    const id = item.world.gameWorldId;
    const socket = new WebSocket(`ws://127.0.0.1:${port}/hubs/gameroom`);
    const separator = '\x1e';
    let resolved, rejected, invocationSent = false, ticks = 0, maxGap = 0, lastTick = 0, events = 0, bytes = 0;
    let checkpointAt = null, snapshot, reconciliation = null;
    const byType = {}, tickGaps = [];
    const incremental = process.env.AUDIT_SIGNALR_V2 === '1';
    const navigateLive = process.env.AUDIT_HUB_NAVIGATE === '1';
    let navigation = null;
    function count(type, line) {
        const stat = byType[type] ??= { count: 0, bytes: 0, maxBytes: 0 };
        const size = Buffer.byteLength(line) + 1;
        stat.count++;
        stat.bytes += size;
        stat.maxBytes = Math.max(stat.maxBytes, size);
    }
    const complete = new Promise((resolve, reject) => { resolved = resolve; rejected = reject; });
    const timeout = setTimeout(() => rejected(Error('Relógio/SignalR excedeu 90 s')), 90000);
    const heartbeat = setInterval(() => {
        if (socket.readyState === WebSocket.OPEN) send({ type: 6 });
    }, 10000);
    const abort = () => rejected(stop.signal.reason);
    stop.signal.addEventListener('abort', abort, { once: true });
    socket.addEventListener('error', () => rejected(Error('Falha WebSocket')));
    socket.addEventListener('close', () => rejected(Error('WebSocket encerrou antes da rodada')));
    socket.addEventListener('open', () => send({ protocol: 'json', version: 1 }));
    function send(message) { socket.send(JSON.stringify(message) + separator); }
    socket.addEventListener('message', async ({ data }) => {
        try {
            bytes += Buffer.byteLength(data);
            for (const line of data.split(separator).filter(Boolean)) {
                const msg = JSON.parse(line);
                if (!invocationSent && !msg.type) {
                    count('Handshake', line);
                    invocationSent = true;
                    send({ type: 1, invocationId: 'join', target: incremental ? 'JoinRoomV2' : 'JoinRoom', arguments: [id, item.coachId] });
                } else if (msg.type === 3) {
                    count('JoinResult', line);
                    if (msg.error || msg.result?.gameWorldId !== id) throw Error(msg.error ?? 'JoinRoom retornou outro mundo');
                    snapshot = msg.result;
                    await call(item.path + '/ready-unemployed', {
                        coachId: item.coachId, gameWorldId: id, expectedRoundVersion: msg.result.roundVersion,
                        idempotencyKey: `hub-ready-${msg.result.seasonNumber}-${msg.result.roundNumber}`,
                    });
                } else if (msg.type === 1) {
                    events++;
                    count(msg.target, line);
                    const event = msg.arguments[0]?.snapshot ?? msg.arguments[0];
                    if ((event?.gameWorldId ?? event?.worldId) !== id) throw Error('Evento de outro mundo');
                    if (msg.target === 'RoundCheckpointPending') checkpointAt = performance.now();
                    if (msg.target === 'RoundPatchV2') {
                        if (event.previousSequence !== snapshot?.realtimeSequence ||
                            event.previousVersion !== snapshot?.roundVersion) throw Error('Lacuna no stream');
                        snapshot = { ...snapshot, ...event };
                    } else if (['ClockTick', 'RoundSyncV2', 'RoundStateChanged'].includes(msg.target)) {
                        snapshot = event;
                    } else {
                        continue;
                    }
                    if (snapshot.currentMinute <= 0) continue;
                    const now = performance.now();
                    if (lastTick) {
                        const gap = now - lastTick;
                        tickGaps.push(gap);
                        maxGap = Math.max(maxGap, gap);
                    }
                    lastTick = now;
                    ticks++;
                    if (navigateLive && ticks === 20) {
                        navigation = Promise.all([
                            call(item.path + '/calendar'),
                            call(item.path + '/clubs'),
                            call(item.path + '/standings/PrimeiraDivisao'),
                        ]).then(([calendar, clubs, table]) => {
                            if (!calendar || clubs.length < 64 || table.length !== 8)
                                throw Error('Navegação ao vivo retornou dados inesperados');
                        });
                        navigation.catch(rejected);
                    }
                    if (snapshot.roundState === 'RESULTS') {
                        const closeMs = checkpointAt == null ? null : now - checkpointAt;
                        resolved({ id, ticks, events, bytes, maxGap, byType, tickGaps, closeMs,
                            roundVersion: snapshot.roundVersion, persistedRoundVersion: snapshot.persistedRoundVersion,
                            seasonNumber: snapshot.seasonNumber, roundNumber: snapshot.roundNumber });
                    }
                } else count(`SignalR-${msg.type}`, line);
            }
        } catch (error) { rejected(error); }
    });
    try {
        const stats = await complete;
        await navigation;
        if (stats.ticks < 90) throw Error(`Somente ${stats.ticks} ticks recebidos`);
        if (process.env.AUDIT_HUB_STANDINGS === '1') {
            const now = performance.now();
            const table = await call(item.path + '/standings/PrimeiraDivisao');
            if (table.length !== 8) throw Error('Classificação inesperada');
            reconciliation = performance.now() - now;
        }
        stats.standingsMs = reconciliation;
        console.log(JSON.stringify({ hub: stats }));
        return stats;
    } finally {
        clearTimeout(timeout);
        clearInterval(heartbeat);
        stop.signal.removeEventListener('abort', abort);
        socket.close();
    }
}

try {
    if (pgConnection && tiers.length !== 1) throw Error('Informe um banco PostgreSQL novo por execução');
    for (const tier of tiers.map(Number)) {
        if (![1, 10, 50, 100, 500, 1000].includes(tier)) throw Error('Nível de carga inválido');
        await run(tier);
        if (stop.signal.aborted) break;
    }
} catch (error) {
    stop.abort(error);
} finally {
    writeFileSync(join(root, 'results.json'), JSON.stringify({
        hostLogicalCpus: availableParallelism(), stop: stop.signal.reason?.message ?? null, results,
    }, null, 2));
    if (stop.signal.aborted) console.error(stop.signal.reason);
}

function components(world) {
    const result = {};
    for (const [name, value] of Object.entries(world)) {
        if (value === null || typeof value !== 'object') continue;
        const bytes = Buffer.byteLength(JSON.stringify(value));
        result[name] = { bytes, rows: Object.keys(value).length };
        if (!Array.isArray(value)) {
            for (const [key, nested] of Object.entries(value)) {
                if (nested === null || typeof nested !== 'object') continue;
                result[`${name}.${key}`] = {
                    rows: Object.keys(nested).length, bytes: Buffer.byteLength(JSON.stringify(nested)),
                };
            }
        }
    }
    return result;
}
