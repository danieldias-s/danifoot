import { spawn } from 'node:child_process';
import { mkdirSync, writeFileSync, createWriteStream } from 'node:fs';
import { resolve, join } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import assert from 'node:assert/strict';

const [dllArg, outputArg, provider, connection] = process.argv.slice(2);
if (!dllArg || !outputArg || !['Sqlite', 'PostgreSQL'].includes(provider)) {
    throw Error('Uso: node stage1-process-test.mjs API.dll pasta-nova Sqlite|PostgreSQL [conexão]');
}
const dll = resolve(dllArg);
const output = resolve(outputArg);
mkdirSync(output);
if (provider === 'PostgreSQL' && !/^Host=127\.0\.0\.1;Port=(54987|54983);Database=(stage1_|audit_)[a-z0-9_]+;Username=audit$/i.test(connection ?? '')) {
    throw Error('Use um banco descartável vazio em loopback na porta 54987 ou 54983.');
}
const db = provider === 'Sqlite' ? `Data Source=${join(output, 'worlds.db')};Pooling=False` : connection;
const processes = [];

function start(name, port, extraArgs = []) {
    const child = spawn('dotnet', [dll, `--urls=http://127.0.0.1:${port}`, '--environment=Audit',
        '--Elifoot:AuthMode=LocalDev', `--Elifoot:DatabaseProvider=${provider}`,
        '--Elifoot:AutoSeedDefaultWorld=false', '--Elifoot:DisableBackgroundClock=true',
        '--Elifoot:ExclusiveWorldAuthority=true', '--Logging:EventLog:LogLevel:Default=None',
        '--Logging:LogLevel:Default=Warning', `--ConnectionStrings:DefaultConnection=${db}`, ...extraArgs],
    { cwd: output, windowsHide: true });
    const log = createWriteStream(join(output, `${name}.log`));
    child.stdout.pipe(log, { end: false });
    child.stderr.pipe(log, { end: false });
    child.once('close', () => log.end());
    processes.push(child);
    return child;
}

function exited(child) {
    if (child.exitCode !== null || child.signalCode !== null) return Promise.resolve(child.exitCode);
    return new Promise(resolve => child.once('exit', resolve));
}

async function api(port, path, body) {
    const response = await fetch(`http://127.0.0.1:${port}/api${path}`, {
        method: body === undefined ? 'GET' : 'POST',
        headers: body == null ? {} : { 'content-type': 'application/json' },
        body: body == null ? null : JSON.stringify(body),
        signal: AbortSignal.timeout(15000),
    });
    assert.ok(response.ok, `${path}: HTTP ${response.status} ${await (response.ok ? Promise.resolve('') : response.text())}`);
    return response.json();
}

async function health(child, port) {
    for (let i = 0; i < 100; i++) {
        if (child.exitCode !== null || child.signalCode !== null) throw Error(`API ${port} encerrou na inicialização`);
        try { if ((await api(port, '/health')).dbProvider === provider) return; } catch {}
        await delay(150);
    }
    throw Error(`API ${port} indisponível`);
}

const firstPort = 54988, secondPort = 54989;
const metrics = { provider };
async function joinV2(port, worldId, coachId) {
    const socket = new WebSocket(`ws://127.0.0.1:${port}/hubs/gameroom`);
    try {
        return await new Promise((resolve, reject) => {
            const timeout = setTimeout(() => reject(Error('SignalR join timeout')), 10000);
            socket.addEventListener('open', () => {
                socket.send('{"protocol":"json","version":1}\x1e');
                socket.send(JSON.stringify({type: 1, invocationId: 'join', target: 'JoinRoomV2', arguments: [worldId, coachId]})+'\x1e');
            });
            socket.addEventListener('error', () => { clearTimeout(timeout); reject(Error('SignalR error')); });
            socket.addEventListener('message', message => {
                for (const frame of String(message.data).split('\x1e').filter(Boolean)) {
                    const reply = JSON.parse(frame);
                    if (reply.invocationId !== 'join') continue;
                    clearTimeout(timeout);
                    if (reply.error || !reply.result) reject(Error(reply.error ?? 'SignalR join denied'));
                    else resolve(reply.result);
                }
            });
        });
    } finally { socket.close(); }
}
try {
    const owner = start('owner', firstPort);
    await health(owner, firstPort);
    const time = performance.now();
    const denied = start('denied', secondPort);
    const deniedCode = await Promise.race([exited(denied), delay(12000, 'timeout')]);
    assert.equal(typeof deniedCode, 'number');
    assert.notEqual(deniedCode, 0);
    metrics.secondProcessRejectedMs = performance.now() - time;

    const world = await api(firstPort, '/worlds', { worldName: 'Audit Authority', totalClubs: 64, maxHumans: 1, inviteCode: 'AUD-LOCK' });
    const path = `/worlds/${world.gameWorldId}`;
    const coachId = 'b49c8103-49c1-4c47-93e8-4b60f15f3fcb';
    const joined = await api(firstPort, `${path}/join?coachId=${coachId}&nickname=Audit&joinUnemployed=true`, null);
    const request = { gameWorldId: world.gameWorldId, coachId, expectedRoundVersion: joined.snapshot.roundVersion, idempotencyKey: 'authority-ready' };
    const another = await api(firstPort, '/worlds', { worldName: 'Audit other room', totalClubs: 64, maxHumans: 1, inviteCode: 'AUD-OTHER' });
    const anotherPath = `/worlds/${another.gameWorldId}`;
    const other = await api(firstPort, `${anotherPath}/join?coachId=${coachId}&nickname=Audit&joinUnemployed=true`, null);
    const otherRequest = { ...request, gameWorldId: another.gameWorldId, expectedRoundVersion: other.snapshot.roundVersion };
    const before = performance.now();
    await Promise.all([api(firstPort, path + '/ready-unemployed', request),
        api(firstPort, path + '/ready-unemployed', request), api(firstPort, anotherPath + '/ready-unemployed', otherRequest)]);
    metrics.durableReadyMs = performance.now() - before;
    const saved = await api(firstPort, path + '/snapshot');
    assert.equal(saved.roundState, 'RUNNING_FIRST_HALF');
    const otherSaved = await api(firstPort, anotherPath + '/snapshot');
    assert.equal(otherSaved.roundState, 'RUNNING_FIRST_HALF');
    const beforeReconnect = await joinV2(firstPort, world.gameWorldId, coachId);
    owner.kill();
    await exited(owner);

    const restartAt = performance.now();
    const replacement = start('replacement', secondPort);
    await health(replacement, secondPort);
    metrics.restartMs = performance.now() - restartAt;
    const replayAt = performance.now();
    await api(secondPort, path + '/ready-unemployed', request);
    metrics.replayMs = performance.now() - replayAt;
    const recovered = await api(secondPort, path + '/snapshot');
    assert.equal(recovered.roundVersion, saved.roundVersion);
    assert.equal(recovered.roundState, saved.roundState);
    metrics.roundVersion = recovered.roundVersion;
    assert.equal((await api(secondPort, anotherPath + '/snapshot')).roundVersion, otherSaved.roundVersion);
    const reconnected = await joinV2(secondPort, world.gameWorldId, coachId);
    assert.notEqual(reconnected.realtimeEpoch, beforeReconnect.realtimeEpoch);
    assert.equal(reconnected.roundVersion, saved.roundVersion);
    metrics.independentWorlds = 2;
    metrics.concurrentReplay = true;
    metrics.signalRRecovered = true;

    if (provider === 'PostgreSQL' && process.env.STAGE1_PSQL) {
        const { execFileSync } = await import('node:child_process');
        const dbName = /Database=([^;]+)/i.exec(connection)[1];
        const pgPort = /Port=([^;]+)/i.exec(connection)[1];
        const result = execFileSync(process.env.STAGE1_PSQL, ['-h', '127.0.0.1', '-p', pgPort, '-U', 'audit',
            '-d', dbName, '-Atc',
            "SELECT pg_terminate_backend(pid) FROM pg_locks WHERE locktype='advisory' AND granted AND objsubid=2 AND classid=1162627398::oid AND objid=98::oid AND database=(SELECT oid FROM pg_database WHERE datname=current_database())"],
        { encoding: 'utf8' });
        assert.equal(result.trim(), 't');
        const lossAt = performance.now();
        assert.notEqual(await Promise.race([exited(replacement), delay(12000, 'timeout')]), 'timeout');
        metrics.authorityLossStopsProcessMs = performance.now() - lossAt;
    }
    writeFileSync(join(output, 'result.json'), JSON.stringify(metrics, null, 2));
    console.log(JSON.stringify(metrics));
} finally {
    for (const child of processes) if (child.exitCode === null && child.signalCode === null) child.kill();
}
