import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { statfsSync, writeFileSync } from 'node:fs';
import { Agent, request } from 'node:https';
import { freemem } from 'node:os';
import { dirname } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

const [output, countArg, seatsArg = '5'] = process.argv.slice(2);
const count = Number(countArg), seats = Number(seatsArg);
assert.ok(output && [10, 100, 500, 1000].includes(count) && seats >= 2 && seats <= 8 && count % seats === 0);
assert.ok(process.env.NODE_EXTRA_CA_CERTS, 'Informe o certificado do staging em NODE_EXTRA_CA_CERTS');
const root = 'https://127.0.0.1:54996';
const separator = '\x1e';
const batch = randomUUID().slice(0, 8);
const subnet = Number(process.env.AUDIT_LOOPBACK_SUBNET ?? ([10, 100, 500, 1000].indexOf(count) + 1));
assert.ok(Number.isInteger(subnet) && subnet >= 1 && subnet <= 250);
const report = { count, seats, worlds: count / seats, phases: [], initialFree: freemem() };
const write = () => writeFileSync(output, JSON.stringify(report, null, 2));
const agents = new Map(), sockets = new Set();
const GiB = 1024 ** 3;
function guard() {
    const disk = statfsSync(dirname(output));
    assert.ok(freemem() >= 2 * GiB && disk.bavail * disk.bsize >= 12 * GiB, 'Limite de RAM ou disco livre');
}

async function http(route, { player, body, post = body !== undefined, csrf } = {}) {
    const cookie = player ? [...player.cookies.values()].join('; ') : '';
    const data = body === undefined ? undefined : JSON.stringify(body);
    const localAddress = player ? `127.0.${subnet}.${2 + Math.floor(player.index / 10)}` : '127.0.0.1';
    if (!agents.has(localAddress)) agents.set(localAddress, new Agent({ keepAlive: true, maxSockets: 8 }));
    const start = performance.now();
    return new Promise((resolve, reject) => {
        const req = request(`${root}/api${route}`, { localAddress, agent: agents.get(localAddress), method: post ? 'POST' : 'GET',
            signal: AbortSignal.timeout(15000), headers: {
                Cookie: cookie, ...(csrf ? { 'X-CSRF-TOKEN': csrf } : {}),
                ...(data === undefined ? {} : { 'content-type': 'application/json', 'content-length': Buffer.byteLength(data) })
            } }, res => {
            const tls = res.socket?.getProtocol();
            for (const item of res.headers['set-cookie'] ?? []) {
                const pair = item.split(';')[0];
                player?.cookies.set(pair.split('=')[0], pair);
            }
            const chunks = [];
            res.on('data', chunk => chunks.push(chunk));
            res.on('error', reject);
            res.on('end', () => {
                const payload = Buffer.concat(chunks);
                let value;
                try { value = JSON.parse(payload); } catch { value = null; }
                resolve({ status: res.statusCode, value, bytes: payload.length, ms: performance.now() - start, tls });
            });
        });
        req.on('error', reject);
        req.end(data);
    });
}

function ok(response, label) {
    assert.ok(response.status >= 200 && response.status < 300, `${label}: HTTP ${response.status}`);
    return response.value;
}

async function post(player, route, body) {
    const token = ok(await http('/auth/csrf', { player }), 'csrf').csrfToken;
    return http(route, { player, post: true, csrf: token, body });
}

async function phase(name, items, concurrency, work) {
    const started = performance.now();
    const successes = [], faults = [];
    let index = 0, guarded;
    await Promise.all(Array.from({ length: Math.min(items.length, concurrency) }, async () => {
        while (index < items.length && !guarded && (faults.length < 3 || faults.length / items.length < .05)) {
            const item = items[index++];
            try { guard(); } catch (error) { guarded = error; break; }
            try { successes.push(await work(item)); } catch (error) { faults.push(error.message); }
        }
    }));
    const ms = performance.now() - started;
    const sorted = successes.map(x => x.ms).sort((a, b) => a - b);
    const quantile = q => sorted[Math.ceil(q * sorted.length) - 1];
    const entry = { name, target: items.length, ok: successes.length, errors: faults.length, faults: faults.slice(0, 6),
        ms, opsSec: successes.length * 1000 / ms, p50: quantile(.5), p95: quantile(.95), p99: quantile(.99),
        bytes: successes.reduce((total, result) => total + (result.bytes ?? 0), 0), guard: guarded?.message };
    report.phases.push(entry);
    write();
    assert.equal(entry.ok, entry.target, `${name}: ${entry.ok}/${entry.target} (${guarded?.message ?? faults[0]})`);
    return entry;
}

function connect(player, room = true) {
    const started = performance.now();
    return new Promise((resolve, reject) => {
        const ws = new WebSocket(root.replace('https:', 'wss:') + '/hubs/gameroom', {
            headers: { Cookie: [...player.cookies.values()].join('; ') }
        });
        sockets.add(ws);
        const send = obj => ws.send(JSON.stringify(obj) + separator);
        const stop = setTimeout(() => { ws.close(); reject(Error('Timeout de ingresso no SignalR')); }, 18000);
        const ping = setInterval(() => { if (ws.readyState === WebSocket.OPEN) send({ type: 6 }); }, 8000);
        const fail = () => { clearTimeout(stop); reject(Error('Falha no WebSocket autenticado')); };
        const done = snapshot => { clearTimeout(stop); resolve({ ms: performance.now() - started, snapshot }); };
        ws.onopen = () => send({ protocol: 'json', version: 1 });
        ws.onerror = fail;
        ws.onclose = () => { clearInterval(ping); sockets.delete(ws); fail(); };
        ws.onmessage = ({ data }) => {
            player.wsBytes += Buffer.byteLength(data);
            for (const line of data.split(separator).filter(Boolean)) {
                const msg = JSON.parse(line);
                if (msg.type === undefined) {
                    if (room) send({ type: 1, invocationId: 'join', target: 'JoinRoomV2', arguments: [player.world, player.coach] });
                    else done(null);
                } else if (msg.type === 3 && msg.invocationId === 'join') {
                    if (msg.result?.gameWorldId === player.world) done(msg.result);
                    else fail();
                } else if (msg.type === 1) {
                    const event = msg.arguments[0]?.snapshot ?? msg.arguments[0];
                    const world = event?.gameWorldId ?? event?.worldId;
                    if (world && world !== player.world) report.crossWorld = (report.crossWorld ?? 0) + 1;
                }
            }
        };
        player.ws = ws;
    });
}

const players = Array.from({ length: count }, (_, index) => ({ index, cookies: new Map(), wsBytes: 0 }));
const groups = Array.from({ length: count / seats }, (_, i) => players.slice(i * seats, (i + 1) * seats));
try {
    const health = await http('/health');
    assert.equal(ok(health, 'saúde').authMode, 'Account');
    assert.equal(health.value.dbProvider, 'PostgreSQL');
    report.tls = health.tls;
    assert.equal((await http('/worlds')).status, 401);
    await phase('cadastro', players, 8, async player => {
        const response = await post(player, '/auth/register', { username: `s7-${batch}-${player.index}`,
            displayName: `Audit ${player.index}`, password: `Isolado-${batch}-${player.index}!` });
        player.coach = ok(response, 'cadastro').coachId;
        return response;
    });
    await phase('mundos', groups, 3, async group => {
        const [owner, ...guests] = group;
        const started = performance.now();
        const made = ok(await post(owner, '/worlds', { worldName: `Audit ${batch} ${owner.index}`, totalClubs: 64,
            seed: 20000 + owner.index, maxHumans: seats, tickIntervalMs: 250, pauseTimeoutSeconds: 10 }), 'mundo');
        const worldId = made.gameWorldId;
        const mine = ok(await post(owner, `/worlds/${worldId}/join`), 'host');
        owner.club = mine.clubId;
        const listing = ok(await http('/worlds', { player: owner }), 'lista do host');
        const code = listing.find(world => world.id === worldId)?.inviteCode;
        assert.ok(code, 'Código indisponível ao host');
        for (const player of guests) {
            const mine = ok(await post(player, '/worlds/join-by-code', { inviteCode: code, joinUnemployed: true }), 'convite');
            assert.equal(mine.worldId, worldId);
        }
        for (const player of group) player.world = worldId;
        return { ms: performance.now() - started };
    });
    await phase('conexao', players, 20, player => connect(player, false));
    await delay(15000);
    report.idleOpen = players.filter(player => player.ws.readyState === WebSocket.OPEN).length;
    assert.equal(report.idleOpen, count);
    await phase('navegacao', players, 16, async player => {
        await delay((player.index * 37) % 180);
        const route = ['clubs', 'calendar', 'standings/PrimeiraDivisao'][player.index % 3];
        const response = await http(`/worlds/${player.world}/${route}`, { player });
        ok(response, route); return response;
    });
    await phase('mercado_leitura', players, 12, async player => {
        await delay((player.index * 29) % 220);
        const response = await http(`/worlds/${player.world}/market`, { player });
        ok(response, 'mercado'); return response;
    });
    const activeGroups = groups.slice(0, Math.min(2, groups.length));
    const active = activeGroups.flat();
    for (const player of active) player.ws.close();
    await phase('ingresso_salas', active, 4, player => connect(player));
    if (process.env.AUDIT_PLAY_ROUND === '1') {
        const routeOf = player => `/worlds/${player.world}`;
        const rosterOf = async owner => ok(await http(`${routeOf(owner)}/clubs/${owner.club}`, { player: owner }), 'plantel');
        const owners = activeGroups.map(group => group[0]);
        await phase('mercado_anuncio_retirada', owners, 2, async owner => {
            const roster = await rosterOf(owner);
            const playerId = roster.preferredBenchIds[0];
            const ids = new URLSearchParams({ coachId: owner.coach, playerId, askingPrice: '100000' });
            const started = performance.now();
            ok(await post(owner, `${routeOf(owner)}/market/list?${ids}`), 'anúncio');
            const withdrawal = await post(owner, `${routeOf(owner)}/market/unlist?${ids}`);
            ok(withdrawal, 'retirada');
            return { ms: performance.now() - started, bytes: withdrawal.bytes };
        });
        await phase('partidas_avanco', activeGroups, activeGroups.length, async group => {
            const started = performance.now();
            const [owner, ...guests] = group;
            const world = routeOf(owner);
            const roster = await rosterOf(owner);
            if (owner.index) await delay(2300);
            async function snapshot() {
                const response = await http(`${world}/snapshot`, { player: owner });
                return response.status === 503 ? null : ok(response, 'snapshot');
            }
            let state = await snapshot();
            const number = state.roundNumber;
            const season = state.seasonNumber;
            let interrupted = false;
            for (const player of [owner, ...guests]) {
                const common = { gameWorldId: owner.world, coachId: player.coach,
                    expectedRoundVersion: state.roundVersion, idempotencyKey: `s7-ready-${number}` };
                const response = player === owner
                    ? await post(owner, `${world}/tactics`, { ...common, formation: roster.preferredFormation,
                        starterPlayerIds: roster.preferredStarterIds, benchPlayerIds: roster.preferredBenchIds })
                    : await post(player, `${world}/ready-unemployed`, common);
                ok(response, 'prontidão');
                state = await snapshot();
            }
            while (state?.roundState !== 'RESULTS') {
                guard();
                assert.ok(performance.now() - started < 110000, 'Rodada excedeu 110 s');
                await delay(850);
                state = await snapshot();
                if (process.env.AUDIT_INTERRUPT_ROUND === '1' && !interrupted && state?.currentMinute >= 10 && state.currentMinute < 40) {
                    interrupted = true;
                    const before = state.roundVersion;
                    const stopped = performance.now();
                    owner.ws.close();
                    await delay(3000);
                    const { snapshot: resumed, ms } = await connect(owner);
                    assert.equal(resumed.roundNumber, number);
                    assert.ok(resumed.roundVersion >= before);
                    (report.duringRoundReconnect ??= []).push({ before, after: resumed.roundVersion,
                        minute: resumed.currentMinute, elapsedMs: performance.now() - stopped, handshakeMs: ms });
                }
            }
            if (process.env.AUDIT_INTERRUPT_ROUND === '1') assert.ok(interrupted, 'Interrupção durante a partida não exercitada');
            assert.equal(state.roundVersion, state.persistedRoundVersion);
            assert.equal(state.roundNumber, number);
            assert.ok(state.matches.length && state.matches.every(match => match.isFinished));
            assert.equal(new Set(state.matches.map(match => match.matchId)).size, state.matches.length);
            report.durableResults = (report.durableResults ?? 0) + 1;
            const salary = `/worlds/${owner.world}/clubs/${owner.club}`;
            for (let i = 0; i < 30; i++) {
                const finances = ok(await http(`${salary}/finances`, { player: owner }), 'finanças');
                const pending = finances.salaryRequests.find(item => item.status === 'PENDING');
                if (!pending) break;
                const version = (await snapshot()).roundVersion;
                ok(await post(owner, `${salary}/salary-requests/${pending.id}/respond`, { coachId: owner.coach,
                    accept: false, idempotencyKey: `s7-salary-${pending.id}`, expectedRoundVersion: version }), 'decisão salarial');
            }
            const next = ok(await post(owner, `${world}/next-round?expectedSeasonNumber=${season}&expectedRoundNumber=${number}`), 'avanço');
            assert.equal(next.roundNumber, number + 1);
            assert.equal(next.roundVersion, next.persistedRoundVersion);
            report.durableRounds = (report.durableRounds ?? 0) + 1;
            return { ms: performance.now() - started };
        });
    }
    const reconnect = players.slice(0, Math.min(count, 100));
    for (const player of reconnect) player.ws.close();
    report.peakReconnectClients = reconnect.length;
    await phase('pico_reconexao', reconnect, 20, player => connect(player, active.includes(player)));
    const a = groups[0][0], b = groups[1][0];
    report.worldProtection = (await http(`/worlds/${b.world}/snapshot`, { player: a })).status;
    assert.equal(report.worldProtection, 403);
    const revoked = a.ws;
    ok(await post(a, '/auth/logout'), 'logout');
    for (let i = 0; i < 20 && revoked.readyState !== WebSocket.CLOSED; i++) await delay(100);
    report.revokedSocket = revoked.readyState === WebSocket.CLOSED;
    assert.ok(report.revokedSocket);
    assert.equal((await http('/worlds', { player: a })).status, 401);
    report.socketBytes = players.reduce((total, player) => total + player.wsBytes, 0);
    assert.equal(report.crossWorld ?? 0, 0);
    report.pass = true;
} catch (error) {
    report.error = error.message;
    throw error;
} finally {
    write();
    for (const socket of sockets) socket.close();
    for (const agent of agents.values()) agent.destroy();
}
