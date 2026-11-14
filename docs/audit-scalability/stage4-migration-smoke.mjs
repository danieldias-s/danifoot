import assert from 'node:assert/strict';
import { spawn, execFileSync } from 'node:child_process';
import { createHash, randomUUID } from 'node:crypto';
import { copyFileSync, mkdirSync, readFileSync, writeFileSync, createWriteStream, existsSync } from 'node:fs';
import { resolve, join } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import { DatabaseSync } from 'node:sqlite';

const [apiFile, toolFile, outputArg, gzipFile] = process.argv.slice(2);
if (!gzipFile) throw Error('Uso: node stage4-migration-smoke.mjs API.dll EngineAudit.dll pasta-nova fixture-gzip-descartável.db');
const output = resolve(outputArg);
mkdirSync(output);
const pg = process.env.STAGE4_PG;
if (!/^Host=127\.0\.0\.1;Port=54983;Database=audit_[a-z0-9_]+;Username=audit$/i.test(pg ?? ''))
    throw Error('Defina STAGE4_PG para um banco novo no cluster descartável.');
const api = resolve(apiFile), tool = resolve(toolFile);
const sqlite = join(output, 'fixture.db');
const keys = join(output, 'keys');
mkdirSync(keys);
const port = 54984;
let server;
try {
    const username = 'migration-stage4';
    const password = 'Local-Disposable-98!';
    const source = await start('sqlite', `Data Source=${sqlite};Pooling=False`, 'AccountTest');
    const registration = await source.post('/auth/register', { username, password, displayName: 'Migration QA' });
    const session = await source.get('/auth/session');
    const guest = client();
    await guest.post('/auth/register', { username: 'migration-guest', password, displayName: 'Migration Guest' });
    const world = await source.post('/worlds', { worldName: 'Migração auditada', totalClubs: 64, maxHumans: 2, inviteCode: 'MIG-AUD-4' });
    await source.post(`/worlds/${world.gameWorldId}/join`);
    await guest.post('/worlds/join-by-code', { inviteCode: 'MIG-AUD-4', joinUnemployed: true });
    const route = `/worlds/${world.gameWorldId}`;
    const saved = await source.get(route + '/snapshot');
    await stop();

    const gzipCopy = join(output, 'historical-fixture.db');
    if (['-wal', '-shm', '-journal'].some(suffix => existsSync(resolve(gzipFile) + suffix)))
        throw Error('Feche a fixture histórica antes de copiá-la.');
    copyFileSync(resolve(gzipFile), gzipCopy);
    const fixture = new DatabaseSync(sqlite);
    const insertTables = ['ef_worlds', 'ef_financial_ledger', 'ef_season_histories', 'ef_club_summaries'];
    fixture.prepare('ATTACH DATABASE ? AS old').run(gzipCopy);
    try {
        const marker = 'ELIFOOT:GZIP:1:';
        assert.equal(fixture.prepare("SELECT substr(SnapshotJson,1,?) AS marker FROM old.ef_worlds").get(marker.length).marker, marker);
        for (const table of insertTables) fixture.exec(`INSERT INTO main.${table} SELECT * FROM old.${table}`);
        fixture.prepare('INSERT INTO ef_profile_appearance (OwnerId,Mode) VALUES (?,?)')
            .run(session.coachId.replaceAll('-', ''), 'CLASSIC_98');
    } finally {
        fixture.exec('DETACH DATABASE old');
        fixture.exec('PRAGMA wal_checkpoint(TRUNCATE)');
        fixture.close();
    }
    const hash = createHash('sha256').update(readFileSync(sqlite)).digest('hex');
    const migrated = execFileSync('dotnet', [tool, 'sqlite-to-postgres', sqlite, hash],
        { encoding: 'utf8', timeout: 240000, windowsHide: true, env: process.env });
    const migration = JSON.parse(migrated);
    assert.equal(migration.verified, true);
    assert.equal(migration.counts.ef_worlds, 2);
    assert.equal(migration.counts.ef_accounts, 2);
    assert.equal(migration.counts.ef_profile_appearance, 1);
    assert.ok(migration.snapshots.some(w => w.compressed));
    assert.ok(migration.snapshots.some(w => !w.compressed));
    assert.equal(createHash('sha256').update(readFileSync(sqlite)).digest('hex'), hash);

    const after = await start('postgres', pg, 'AccountTest');
    const signedIn = await after.post('/auth/login', { username, password });
    assert.equal(signedIn.coachId, session.coachId);
    assert.equal((await after.get('/auth/session')).accountId, session.accountId);
    const live = await after.get(route + '/snapshot');
    assert.equal(live.roundVersion, saved.roundVersion);
    assert.ok(live.readyCounter.participants.some(p => p.coachId === session.coachId));
    assert.equal((await after.get('/worlds')).some(w => w.id === world.gameWorldId || w.gameWorldId === world.gameWorldId), true);
    await after.get(route + '/market');
    await after.get(route + '/standings/PrimeiraDivisao');
    await after.get(route + '/invitations/' + session.coachId);
    writeFileSync(join(output, 'result.json'), JSON.stringify({ migration, setup: {
        registrationPreserved: (registration.coachId ?? session.coachId) === session.coachId,
        sourceHashPreserved: true, signin: true, list: true, inviteCreatedBeforeMigration: true,
        market: true, standings: true, career: true, roundVersion: live.roundVersion
    } }, null, 2));
} finally {
    await stop();
}

function client() {
    const cookies = new Map();
    async function request(path, body, csrf) {
        const headers = {};
        if (cookies.size) headers.Cookie = [...cookies.values()].join('; ');
        if (csrf) headers['X-CSRF-TOKEN'] = csrf;
        if (body !== undefined && body !== null) headers['Content-Type'] = 'application/json';
        const response = await fetch(`http://127.0.0.1:${port}/api${path}`, {
            method: body === undefined ? 'GET' : 'POST', headers,
            body: body == null ? null : JSON.stringify(body), signal: AbortSignal.timeout(35000)
        });
        for (const line of response.headers.getSetCookie()) {
            const cookie = line.split(';')[0];
            cookies.set(cookie.split('=')[0], cookie);
        }
        const raw = await response.text();
        assert.ok(response.ok, `${path}: ${response.status} ${raw.slice(0, 120)}`);
        return raw ? JSON.parse(raw) : null;
    }
    return {
        get: path => request(path),
        post: async (path, body = null) => request(path, body, (await request('/auth/csrf')).csrfToken)
    };
}

async function start(name, connection, auth) {
    const provider = connection.startsWith('Host=') ? 'PostgreSQL' : 'Sqlite';
    server = spawn('dotnet', [api, '--environment=Development', `--urls=http://127.0.0.1:${port}`,
        `--Elifoot:AuthMode=${auth}`, `--Elifoot:DatabaseProvider=${provider}`,
        `--Accounts:KeyDirectory=${keys}`,
        '--Elifoot:AutoSeedDefaultWorld=false', '--Elifoot:DisableBackgroundClock=true',
        '--Catalog013A:Enabled=false', '--Catalog016A:Enabled=false', '--Catalog016B:Enabled=false',
        '--Logging:EventLog:LogLevel:Default=None', `--ConnectionStrings:DefaultConnection=${connection}`],
    { cwd: output, windowsHide: true });
    const log = createWriteStream(join(output, name + '.log'));
    const started = server;
    let launchError;
    started.once('error', error => { launchError = error; });
    started.stdout.pipe(log, { end: false });
    started.stderr.pipe(log, { end: false });
    started.once('close', () => log.end());
    const http = client();
    const deadline = Date.now() + 90000;
    while (Date.now() < deadline) {
        if (launchError || started.exitCode !== null || started.signalCode !== null)
            throw Error(name + ' falhou na inicialização', { cause: launchError });
        try { if ((await http.get('/health')).dbProvider === provider) return http; } catch {}
        await delay(350);
    }
    throw Error(name + ' não respondeu');
}

async function stop() {
    if (!server?.pid || server.exitCode !== null || server.signalCode !== null) return;
    const finish = new Promise(resolve => server.once('close', resolve));
    server.kill();
    await finish;
}
