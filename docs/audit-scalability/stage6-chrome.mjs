import assert from 'node:assert/strict';
import { spawn, execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { createWriteStream, existsSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

const [out, theme, widthArg = '1366', heightArg = '900', buildPath = '/'] = process.argv.slice(2);
assert.ok(out && ['CURRENT', 'CLASSIC_98'].includes(theme));
assert.ok(['/', '/before/', '/after/'].includes(buildPath));
const api = 'http://127.0.0.1:54987/api';
const career = process.env.AUDIT_CAREER_CASE;
assert.ok(!career || ['invitation', 'dismissal'].includes(career));
const site = `http://127.0.0.1:${process.env.AUDIT_WEB_PORT === '5243' ? 5243 : 5242}`;
const debugPort = process.env.AUDIT_CHROME_DEBUG_PORT === '54991' ? 54991 : 54990;
const debug = `http://127.0.0.1:${debugPort}`;
const cookies = new Map();
const health = await (await fetch(`${api}/health`)).json();
assert.equal(health.authMode, 'AccountTest');
assert.equal(health.dbProvider, 'PostgreSQL');

async function request(path, method = 'GET', body) {
    const headers = {};
    if (method !== 'GET') headers['X-CSRF-TOKEN'] = (await request('/auth/csrf')).csrfToken;
    headers.cookie = [...cookies].map(([key, value]) => `${key}=${value}`).join('; ');
    if (body !== undefined) headers['content-type'] = 'application/json';
    const response = await fetch(api + path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body),
        signal: AbortSignal.timeout(20000) });
    for (const line of response.headers.getSetCookie()) {
        const first = line.split(';')[0];
        const i = first.indexOf('=');
        cookies.set(first.slice(0, i), first.slice(i + 1));
    }
    const raw = await response.text();
    assert.ok(response.ok, `${path}: ${response.status} ${raw.slice(0, 250)}`);
    return JSON.parse(raw);
}

const tag = randomUUID().slice(0, 8);
const account = await request('/auth/register', 'POST', {
    username: `ch-${tag}`, displayName: `Chrome ${theme}`, password: `Audit-${tag}-only!`,
});
await request('/profile/appearance', 'PUT', { mode: theme });
const doAdjustment = process.env.AUDIT_WIDGET_ADJUSTMENT === '1';
const world = await request('/worlds', 'POST', { worldName: `Chrome Audit ${tag}`, totalClubs: 64, seed: 12001,
    maxHumans: 1, inviteCode: `CH-${tag}`, tickIntervalMs: 200, pauseTimeoutSeconds: doAdjustment ? 45 : 10 });
const path = `/worlds/${world.gameWorldId}`;
const joined = await request(`${path}/join`, 'POST');
const careerFixture = career ? JSON.parse(execFileSync('dotnet', [process.env.AUDIT_CAREER_TOOL,
    'stage7-career', world.gameWorldId, joined.coachId, career], { encoding: 'utf8', timeout: 30000 })) : null;
if (career) {
    writeFileSync(`${out}.restart`, JSON.stringify(careerFixture), { flag: 'wx' });
    const deadline = Date.now() + 45000;
    while (!existsSync(`${out}.ready`)) {
        assert.ok(Date.now() < deadline, 'Reinício da API de carreira não confirmado');
        await delay(250);
    }
}

const chrome = spawn(process.env.AUDIT_CHROME ?? 'C:/Program Files/Google/Chrome/Application/chrome.exe', [
    '--headless=new', `--remote-debugging-port=${debugPort}`, '--no-first-run', '--disable-background-networking',
    '--in-process-gpu', '--use-angle=swiftshader', '--enable-unsafe-swiftshader',
    '--host-resolver-rules=MAP * ~NOTFOUND, EXCLUDE 127.0.0.1',
    ...(process.env.AUDIT_CHROME_SOFTWARE_ONLY === '1' ? ['--no-sandbox'] : []),
    `--user-data-dir=${join(dirname(resolve(out)), 'chrome-' + tag)}`, 'about:blank',
], { windowsHide: true, stdio: ['ignore', 'ignore', 'pipe'] });
chrome.stderr.pipe(createWriteStream(`${out}.chrome.log`));
let tab;
try {
    for (let attempt = 0; attempt < 40; attempt++) {
        try {
            const targets = await (await fetch(`${debug}/json/list`)).json();
            tab = targets.find(target => target.type === 'page');
            if (tab) break;
        }
        catch { await delay(200); }
    }
    assert.ok(tab?.webSocketDebuggerUrl, 'Chrome CDP indisponível');
} catch (error) { chrome.kill(); throw error; }
const ws = new WebSocket(tab.webSocketDebuggerUrl);
await new Promise((resolve, reject) => { ws.onopen = resolve; ws.onerror = reject; });
let id = 0;
const pending = new Map();
const faults = [], network = [], sockets = [];
ws.addEventListener('message', ({ data }) => {
    const msg = JSON.parse(data);
    if (msg.id) {
        const finish = pending.get(msg.id);
        pending.delete(msg.id);
        finish?.(msg);
    } else if (msg.method === 'Runtime.exceptionThrown') faults.push(msg.params.exceptionDetails.exception?.description);
    else if (msg.method === 'Network.responseReceived') {
        const { url, status, mimeType } = msg.params.response;
        if (url.startsWith('http://127.0.0.1:')) network.push({ url: new URL(url).pathname, status, mimeType, at: Date.now() });
    } else if (msg.method === 'Network.webSocketFrameReceived') {
        const payload = msg.params.response.payloadData;
        for (const frame of payload.split('\x1e').filter(Boolean)) {
            try {
                const message = JSON.parse(frame);
                const state = message.arguments?.[0];
                sockets.push({ at: Date.now(), target: message.target, minute: state?.currentMinute });
            } catch {}
        }
    }
});

async function cdp(method, params = {}) {
    const key = ++id;
    const message = await new Promise((resolve, reject) => {
        const timer = setTimeout(() => { pending.delete(key); reject(Error(`CDP timeout: ${method}`)); }, 12000);
        pending.set(key, value => { clearTimeout(timer); resolve(value); });
        ws.send(JSON.stringify({ id: key, method, params }));
    });
    if (message.error) throw Error(`${method}: ${message.error.message}`);
    return message.result;
}

async function evaluate(expression) {
    const result = await cdp('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (result.exceptionDetails) throw Error(result.exceptionDetails.exception?.description ?? expression);
    return result.result.value;
}

const nodes = `(() => {
const roots = [document];
for (const root of roots) {
    for (const element of root.querySelectorAll('flt-glass-pane, flutter-view, flt-semantics-host')) {
        if (element.shadowRoot) roots.push(element.shadowRoot);
    }
}
return roots.flatMap(root => [...root.querySelectorAll('flt-semantics, [role]')]).map(el => {
    const r = el.getBoundingClientRect();
    return { label: (el.getAttribute('aria-label') || el.textContent || '').trim(), role: el.getAttribute('role'),
        x: r.x + r.width / 2, y: r.y + r.height / 2, w: r.width, h: r.height };
}).filter(x => x.w > 0 && x.h > 0 && x.x > 0 && x.x < innerWidth && x.y > 0 && x.y < innerHeight && x.label.length < 150);
})()`;

async function waitLabel(regex, timeout = 14000, since = Date.now()) {
    while (Date.now() - since < timeout) {
        const found = (await evaluate(nodes)).find(node => regex.test(node.label));
        if (found) return found;
        await delay(65);
    }
    throw Error(`Elemento ausente: ${regex}`);
}

async function click(node) {
    const { x, y } = node;
    await cdp('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: 'left', clickCount: 1 });
    await cdp('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: 'left', clickCount: 1 });
}

async function adjustTactics() {
    const snapshot = await request(`${path}/snapshot`);
    assert.equal(snapshot.roundState, 'GLOBAL_PAUSED');
    const mine = snapshot.matches.find(match => match.homeClubId === joined.clubId || match.awayClubId === joined.clubId);
    const side = mine.homeClubId === joined.clubId ? 'home' : 'away';
    const previous = mine[`${side}Formation`];
    const next = previous === '4-4-2' ? '4-3-3' : '4-4-2';
    const escape = value => String(value).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const control = await waitLabel(new RegExp(`^Formaç[aã]o ${escape(previous)}$`, 'i'), 4500);
    await click(control);
    await click(await waitLabel(new RegExp(`^Formaç[aã]o ${escape(next)}$`, 'i'), 4500));
    await click(await waitLabel(/^APLICAR TÁTICA$/i, 4500));
    const until = Date.now() + 5000;
    while (Date.now() < until) {
        const followup = await request(`${path}/snapshot`);
        if (followup.matches.find(match => match.matchId === mine.matchId)?.[`${side}Formation`] === next) {
            return { from: previous, to: next, version: followup.roundVersion };
        }
        await delay(250);
    }
    throw Error('Formação alterada não encontrada na projeção do servidor');
}

async function substitutePlayer() {
    const snapshot = await request(`${path}/snapshot`);
    assert.equal(snapshot.roundState, 'GLOBAL_PAUSED');
    const mine = snapshot.matches.find(match => match.homeClubId === joined.clubId || match.awayClubId === joined.clubId);
    const side = mine.homeClubId === joined.clubId ? 'home' : 'away';
    const matchesPlayer = player => label => label.split('\n').includes(player.displayName);
    const starters = mine[`${side}Starters`].slice(0, 6).filter(p => !p.isSentOff && !p.wasSubstitutedOut);
    const bench = mine[`${side}Bench`].slice(0, 6).filter(p => !p.isSentOff && !p.isInjuredInMatch);
    const all = await evaluate(nodes);
    const left = all.find(node => /^EM CAMPO — SAI/i.test(node.label));
    const right = all.find(node => /^BANCO — ENTRA/i.test(node.label));
    assert.ok(left && right, 'Listas da janela de substituição ausentes');
    const findPlayer = (labels, player, heading) => labels.filter(node => node.role === 'button' &&
        matchesPlayer(player)(node.label) && node.y > heading.y && node.y < heading.y + 284 &&
        Math.abs(node.x - heading.x) < heading.w / 2).sort((a, b) => b.w - a.w)[0];
    const out = starters.find(p => bench.some(q => q.position === p.position && q.displayName !== p.displayName &&
        findPlayer(all, q, right)) && findPlayer(all, p, left));
    assert.ok(out, 'Par elegível ausente nos controles de substituição');
    const incoming = bench.find(p => p.position === out.position && p.displayName !== out.displayName &&
        findPlayer(all, p, right));
    for (const [player, heading] of [[out, left], [incoming, right]]) {
        const control = findPlayer(await evaluate(nodes), player, heading);
        assert.ok(control, `Controle ausente: ${player.displayName}`);
        await click(control);
        await delay(300);
    }
    const button = (await evaluate(nodes)).filter(node => node.role === 'button' && /^SUBSTITUIR$/i.test(node.label) && node.y > left.y + 50).at(-1);
    assert.ok(button, 'Ação da janela de substituição ausente');
    await click(button);
    const until = Date.now() + 5000;
    while (Date.now() < until) {
        const followup = await request(`${path}/snapshot`);
        const match = followup.matches.find(m => m.matchId === mine.matchId);
        const event = match?.events.find(e => e.eventType === 'Substitution' && e.clubId === joined.clubId &&
            e.primaryPlayerId === incoming.playerId && e.secondaryPlayerId === out.playerId);
        if (match?.[`${side}SubCount`] === mine[`${side}SubCount`] + 1 && event) {
            return { out: out.displayName, in: incoming.displayName, minute: event.minute, version: followup.roundVersion };
        }
        await delay(200);
    }
    throw Error('Substituição não encontrada na projeção do servidor');
}

const report = { theme, viewport: [Number(widthArg), Number(heightArg)], navigation: [], faults, network, sockets };
const flush = () => writeFileSync(out, JSON.stringify(report, null, 2));
async function sample(name, ready, action, timeout = 14000) {
    const start = Date.now();
    await action();
    const arrived = await waitLabel(ready, timeout, start);
    report.navigation.push({ name, ms: Date.now() - start, label: arrived.label });
    flush();
}

async function optionalSample(name, ready, action) {
    try { await sample(name, ready, action, 5000); }
    catch (error) {
        report.navigation.push({ name, error: String(error.message) });
        flush();
    }
}

try {
    await cdp('Page.enable');
    await cdp('Runtime.enable');
    await cdp('Network.enable');
    await cdp('Network.setCacheDisabled', { cacheDisabled: true });
    await cdp('Network.setBypassServiceWorker', { bypass: true });
    await cdp('Performance.enable');
    await cdp('Emulation.setDeviceMetricsOverride', { width: Number(widthArg), height: Number(heightArg), deviceScaleFactor: 1, mobile: false });
    for (const [name, value] of cookies) {
        await cdp('Network.setCookie', { name, value, url: site, httpOnly: true, sameSite: 'Strict' });
    }
    await cdp('Page.addScriptToEvaluateOnNewDocument', { source: `
        window.__audit = {frames: [], intervals: []};
        try { new PerformanceObserver(list => window.__audit.intervals.push(...list.getEntries().map(e => e.duration)))
            .observe({type: 'longtask', buffered: true}); } catch (_) {}
        let previous;
        function frame(now) {
            if (previous != null && window.__audit.frames.length < 20000) window.__audit.frames.push([now, now - previous]);
            previous = now;
            requestAnimationFrame(frame);
        }
        requestAnimationFrame(frame);
        try { localStorage.setItem(${JSON.stringify(`flutter.lc98.account-navigation.${account.accountId}`)},
          JSON.stringify({worldId: ${JSON.stringify(world.gameWorldId)}, coachId: ${JSON.stringify(account.coachId)}})); } catch (_) {}
    ` });
    const start = Date.now();
    await cdp('Page.navigate', { url: site + (buildPath === '/' ? '/' : `${buildPath}index.html`) });
    const resume = await waitLabel(/RETOMAR MINHA SESSÃO/i, 35000, start);
    report.initialMs = Date.now() - start;
    report.initialLabels = await evaluate(nodes);
    await sample('retomar', theme === 'CURRENT' ? /ENVIAR TÁTICA|PLANTEL|VOCÊ •|TREINADOR SEM CLUBE/i : /VERSÃO\s+CLÁSSICA/i,
        () => click(resume));
    flush();
    if (career === 'dismissal') {
        if (theme === 'CLASSIC_98') {
            await click(await waitLabel(/^TREINADOR$/i));
        }
        const visible = await waitLabel(/TREINADOR SEM CLUBE|VOCÊ •.*Sem clube|Clube: Sem clube|AVANÇAR RODADA \(SEM CLUBE\)/i);
        const state = await request(`${path}/snapshot`);
        const mine = state.readyCounter.participants.find(coach => coach.coachId === joined.coachId);
        assert.ok(careerFixture.unemployed && visible && (!mine || mine.clubId == null),
            'Treinador com clube ou widgets sem indicação de desemprego');
        assert.ok(visible, 'Widgets não exibiram situação sem clube');
        report.career = { case: career, label: visible.label, version: state.persistedRoundVersion };
    } else if (career === 'invitation') {
        const careerBefore = await request(`${path}/invitations/${joined.coachId}`);
        assert.ok(careerBefore.invitations.some(item => item.clubId === careerFixture.clubId), 'Convite não exposto pela API');
        if (theme === 'CLASSIC_98') {
            await click(await waitLabel(/^TREINADOR$/i));
            await click(await waitLabel(/^História\.\.\.$/i));
        } else {
            await click(await waitLabel(/^5\.\s*RANKING/i));
            const selection = /^CARREIRA DO TREINADOR$/i;
            await waitLabel(selection);
            const careerTab = (await evaluate(nodes)).find(node => node.role === 'button' && selection.test(node.label));
            assert.ok(careerTab, 'Aba de carreira indisponível');
            await click(careerTab);
        }
        const invite = await waitLabel(theme === 'CLASSIC_98' ? /^Convites$/i : /CONVITES DE EMPREGO DISPONÍVEIS/i);
        await click(await waitLabel(/^ACEITAR$/i));
        const until = Date.now() + 18000;
        while (Date.now() < until) {
            const state = await request(`${path}/snapshot`);
            const mine = state.readyCounter.participants.find(coach => coach.coachId === joined.coachId);
            if (mine?.clubId === careerFixture.clubId && state.roundVersion === state.persistedRoundVersion &&
                network.some(packet => /\/invitations\/[^/]+\/accept$/.test(packet.url) && packet.status === 200)) {
                report.career = { case: career, invitation: invite.label, club: mine.clubName, version: state.roundVersion };
                break;
            }
            await delay(350);
        }
        assert.ok(report.career, 'Troca de clube não confirmada');
    }
    if (!career) await playAndMeasure();
} catch (error) {
    report.error = String(error.stack ?? error);
    try { report.lastLabels = await evaluate(nodes); } catch {}
    try {
        report.page = await evaluate(`({body: document.body.innerHTML.slice(0,450), session: Object.keys(localStorage)})`);
        report.ax = (await cdp('Accessibility.getFullAXTree')).nodes.map(node => node.name?.value).filter(Boolean).slice(0, 55);
    } catch {}
    throw error;
} finally {
    flush();
    ws.close();
    chrome.kill();
}

async function playAndMeasure() {
    const width = Number(widthArg);
    const lineup = /^(Táticas|Escalação|1\. Plantel & Táticas)$/i;
    const market = /^(Mercado|(?:3\. )?Mercado & Leilão|Mercado & Leilões)$/i;
    if (width >= 1024 && process.env.AUDIT_FAST_MATCH !== '1') {
        if (theme === 'CURRENT') {
            await optionalSample('classificacao', /CLASSIFICAÇÃO|TABELA DE CLASSIFICAÇÃO/i,
                async () => click(await waitLabel(/^2\. DIVISÕES/i)));
            await optionalSample('calendario', /CALENDÁRIO DA TEMPORADA|TODAS AS RODADAS/i,
                async () => click(await waitLabel(/^6\. CALENDÁRIO/i)));
            await optionalSample('carreira', /CARREIRA DO TREINADOR|CONVITES|RANKING DOS TREINADORES/i,
                async () => click(await waitLabel(/^5\. RANKING/i)));
            await optionalSample('mercado', /LEILÕES —|COMPRA DIRETA/i, async () => click(await waitLabel(market)));
        } else {
            await optionalSample('mercado', /^Minhas ofertas$|ANUNCIAR JOGADOR/i, async () => click(await waitLabel(market)));
        }
        await optionalSample('escalacao', /FORMAÇÃO|PLANTEL DO CLUBE|ESQUEMA TÁTICO|RESUMO TÁTICO/i, async () => {
            const actions = await evaluate(nodes);
            const button = actions.filter(node => node.role === 'button' && lineup.test(node.label)).at(-1);
            await click(button ?? await waitLabel(lineup));
        });
    }
    if (theme === 'CLASSIC_98') {
        const actions = await evaluate(nodes);
        if (!actions.some(node => /^ENVIAR TÁTICA$/i.test(node.label))) {
            const coachHome = actions.filter(node => node.role === 'button' && /^Jogo$/i.test(node.label)).at(-1);
            if (coachHome) await click(coachHome);
        }
    }
    await delay(400);
    report.beforeRoundLabels = await evaluate(nodes);
    let button = report.beforeRoundLabels.find(node => /^ENVIAR TÁTICA$/i.test(node.label));
    if (!button && theme === 'CLASSIC_98' && width < 1024) {
        for (let i = 0; i < 3 && !button; i++) {
            await cdp('Input.dispatchMouseEvent', { type: 'mouseWheel', x: width * .53, y: Number(heightArg) * .75, deltaX: 0, deltaY: 550 });
            await delay(450);
            button = (await evaluate(nodes)).find(node => /^ENVIAR TÁTICA$/i.test(node.label));
        }
    }
    if (button) {
        const started = Date.now();
        await click(button);
        await waitLabel(/MINUTO [1-9]|PLACARES DA RODADA|LANCES DA PARTIDA/i);
        report.roundEnteredMs = Date.now() - started;
        report.roundInitialLabels = await evaluate(nodes);
        const perfStart = Object.fromEntries((await cdp('Performance.getMetrics')).metrics.map(({ name, value }) => [name, value]));
        const sampleStart = await evaluate('performance.now()');
        await delay(5000);
        report.liveFrames = await evaluate(`window.__audit.frames.filter(([at]) => at >= ${sampleStart}).map(([, ms]) => ms)`);
        report.frameWindowMs = 5000;
        const perfEnd = Object.fromEntries((await cdp('Performance.getMetrics')).metrics.map(({ name, value }) => [name, value]));
        if (perfEnd.TaskDuration >= perfStart.TaskDuration && report.liveFrames.length > 0) {
            report.liveMainThreadTaskMs = (perfEnd.TaskDuration - perfStart.TaskDuration) * 1000;
        }
        report.liveJsHeapBytes = perfEnd.JSHeapUsedSize;
        flush();
    }
    const beforeScroll = await evaluate('performance.now()');
    await cdp('Input.dispatchMouseEvent', { type: 'mouseWheel', x: width * .6, y: Number(heightArg) * .72, deltaX: 0, deltaY: 520 });
    await delay(700);
    report.scrollFrames = await evaluate(`window.__audit.frames.filter(([at]) => at >= ${beforeScroll}).map(([,ms]) => ms)`);
    report.performance = Object.fromEntries((await cdp('Performance.getMetrics')).metrics.map(({ name, value }) => [name, value]));
    report.resources = await evaluate(`performance.getEntriesByType('resource').map(e => ({url: new URL(e.name).pathname,
        durationMs: e.duration, bytes: e.transferSize, initiator: e.initiatorType}))`);
    report.longTasks = await evaluate('window.__audit.intervals');
    if (button && process.env.AUDIT_FINISH_ROUND === '1') {
        const stop = (await evaluate(nodes)).find(node => node.role === 'button' && /^SUBSTITUIR$/i.test(node.label));
        if (stop) {
            await click(stop);
            await optionalSample('pausa', /SUBSTITUIÇÕES •|CONFIRMAR E CONTINUAR|APLICAR TÁTICA/i, async () => {});
            if (doAdjustment) {
                try { report.adjustment = await adjustTactics(); }
                catch (error) { report.adjustmentError = String(error.message); }
                try { report.substitution = await substitutePlayer(); }
                catch (error) { report.substitutionError = String(error.message); }
                flush();
            }
        }
        const deadline = Date.now() + 75000;
        const interactions = new Set();
        while (Date.now() < deadline) {
            const snapshot = await request(`${path}/snapshot`);
            if (snapshot.roundState === 'RESULTS' || snapshot.roundNumber > 1) {
                report.result = { state: snapshot.roundState, round: snapshot.roundNumber, version: snapshot.roundVersion,
                    persisted: snapshot.persistedRoundVersion, matchesFinished: snapshot.matches.every(match => match.isFinished),
                    events: snapshot.matches.flatMap(match => match.events ?? [])
                        .reduce((counts, event) => { counts[event.eventType] = (counts[event.eventType] ?? 0) + 1; return counts; }, {}) };
                break;
            }
            const pause = snapshot.activePauseWindow?.pauseWindowId;
            if (pause && !interactions.has(pause)) {
                try {
                    const resume = await waitLabel(/^(CONTINUAR|PRONTO PARA O 2º TEMPO|CONFIRMAR E CONTINUAR)$/i, 3000);
                    await click(resume);
                    interactions.add(pause);
                } catch {}
            }
            await delay(850);
        }
        try {
            report.finishedUi = (await evaluate(nodes)).filter(node => /RODADA CONCLUÍDA|CLASSIFICAÇÃO/i.test(node.label)).map(node => node.label);
        } catch (error) { report.finishedUiError = String(error.message); }
        if (report.result?.state === 'RESULTS' && process.env.AUDIT_WAIT_NEXT_ROUND === '1') {
            const until = Date.now() + 20000;
            while (Date.now() < until) {
                if (!(report.finishedUi?.length > 0)) {
                    report.finishedUi = (await evaluate(nodes)).filter(node => /RODADA CONCLUÍDA|CLASSIFICAÇÃO/i.test(node.label)).map(node => node.label);
                }
                const snapshot = await request(`${path}/snapshot`);
                const advancedByWidget = network.some(response => response.url === `/api${path}/next-round` && response.status === 200);
                if (advancedByWidget && snapshot.roundNumber === report.result.round + 1 &&
                    snapshot.persistedRoundVersion === snapshot.roundVersion) {
                    report.nextRound = { round: snapshot.roundNumber, state: snapshot.roundState, version: snapshot.roundVersion };
                    break;
                }
                await delay(700);
            }
        }
    }
}
