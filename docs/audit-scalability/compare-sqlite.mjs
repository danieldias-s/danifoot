import { Worker, isMainThread, parentPort, workerData } from 'node:worker_threads';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { DatabaseSync } from 'node:sqlite';
import { join, resolve } from 'node:path';

if (isMainThread) {
    const [dirArg, ...sizes] = process.argv.slice(2);
    if (!dirArg || !sizes.length) throw Error('Uso: node compare-sqlite.mjs pasta-nova ligas...');
    const dir = resolve(dirArg);
    mkdirSync(dir);
    const results = [];
    for (const n of sizes.map(Number)) {
        for (const shared of [true, false]) {
            const root = join(dir, `${n}-${shared ? 'shared' : 'split'}`);
            mkdirSync(root);
            const jobs = Array.from({ length: n }, (_, i) => join(root, `${shared ? 0 : i}.db`));
            const unique = [...new Set(jobs)];
            for (const path of unique) {
                const db = new DatabaseSync(path);
                db.exec('PRAGMA journal_mode=WAL; CREATE TABLE snapshots(id INTEGER PRIMARY KEY, body TEXT)');
                const insert = db.prepare('INSERT INTO snapshots VALUES (?, ?)');
                for (const [id, filename] of jobs.entries()) if (filename === path) insert.run(id, payload(0));
                db.close();
            }
            const assignments = Array.from({ length: Math.min(20, n) }, () => []);
            jobs.forEach((db, id) => assignments[id % assignments.length].push({ db, id }));
            const start = performance.now();
            const responses = await Promise.all(assignments.map(work => new Promise((resolve, reject) => {
                const worker = new Worker(new URL(import.meta.url), { workerData: work });
                worker.once('message', resolve);
                worker.once('error', reject);
            })));
            const ms = performance.now() - start;
            const ok = responses.flatMap(x => x.ok).sort((a, b) => a - b);
            const errors = responses.flatMap(x => x.errors);
            const p = q => ok[Math.ceil(ok.length * q) - 1] ?? null;
            const result = { n, shared, concurrency: assignments.length, ms, ok: ok.length, errors,
                opsSec: 1000 * ok.length / ms, p50: p(.5), p95: p(.95), p99: p(.99) };
            console.log(JSON.stringify(result));
            results.push(result);
            writeFileSync(join(dir, 'results.json'), JSON.stringify(results, null, 2));
        }
    }
} else {
    const ok = [], errors = [];
    for (const { db: path, id } of workerData) {
        const db = new DatabaseSync(path, { timeout: 5000 });
        try {
            const update = db.prepare('UPDATE snapshots SET body=? WHERE id=?');
            for (let revision = 1; revision <= 3; revision++) {
                const body = payload(revision);
                const start = performance.now();
                update.run(body, id);
                ok.push(performance.now() - start);
            }
        } catch (error) {
            errors.push({ code: error.errcode, error: error.message });
        } finally { db.close(); }
    }
    parentPort.postMessage({ ok, errors });
}

function payload(revision) {
    return '{"revision":' + revision + ',"synthetic":"' + 'a'.repeat(2_650_000) + '"}';
}
