import { createHash } from 'node:crypto';
import { copyFileSync, existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { basename, join, resolve } from 'node:path';
import { gzipSync } from 'node:zlib';
import { DatabaseSync } from 'node:sqlite';

const [targetArg, ...sources] = process.argv.slice(2);
if (!targetArg || !sources.length) throw Error('Uso: node storage.mjs pasta-nova banco1 [banco2...]');
const target = resolve(targetArg);
mkdirSync(target);
const results = [];
for (const [index, source] of sources.entries()) {
    const dest = join(target, `${index}-${basename(source)}`);
    const capture = () => Object.fromEntries(['', '-wal', '-shm', '-journal'].map(suffix => {
        const path = source + suffix;
        if (!existsSync(path)) return [suffix || 'db', null];
        const stat = statSync(path);
        const hash = suffix === '-shm' ? null : createHash('sha256').update(readFileSync(path)).digest('hex');
        return [suffix || 'db', { bytes: stat.size, mtimeMs: stat.mtimeMs, hash }];
    }));
    const entry = { source, files: capture() };
    results.push(entry);
    try {
        for (const suffix of ['', '-wal', '-journal']) {
            if (entry.files[suffix || 'db']) copyFileSync(source + suffix, dest + suffix);
        }
        const after = capture();
        for (const suffix of ['db', '-wal', '-journal']) {
            const sourceFile = entry.files[suffix];
            if (JSON.stringify(sourceFile) !== JSON.stringify(after[suffix])) throw Error('Origem mudou durante a cópia');
            if (sourceFile) {
                const copied = dest + (suffix === 'db' ? '' : suffix);
                const digest = createHash('sha256').update(readFileSync(copied)).digest('hex');
                if (digest !== sourceFile.hash) throw Error('Cópia divergiu do hash da origem');
            }
        }
        const sqlite = new DatabaseSync(dest);
        try {
            entry.integrity = sqlite.prepare('PRAGMA quick_check').all();
            if (entry.integrity.length !== 1 || entry.integrity[0].quick_check !== 'ok') throw Error('Cópia não passou no quick_check');
            entry.journalMode = sqlite.prepare('PRAGMA journal_mode').get().journal_mode;
            entry.pageSize = sqlite.prepare('PRAGMA page_size').get().page_size;
            entry.pages = sqlite.prepare('PRAGMA page_count').get().page_count;
            entry.freePages = sqlite.prepare('PRAGMA freelist_count').get().freelist_count;
            entry.objects = sqlite.prepare(`
                SELECT s.type, s.tbl_name AS tableName, d.name, count(*) AS pages,
                    sum(d.pgsize) AS bytes, sum(d.payload) AS payload, sum(d.unused) AS unused
                FROM dbstat d LEFT JOIN sqlite_schema s ON s.name=d.name GROUP BY d.name ORDER BY bytes DESC`).all();
            const tables = sqlite.prepare("SELECT name FROM sqlite_schema WHERE type='table'").all();
            entry.rowCounts = Object.fromEntries(tables.map(({ name }) => [name, sqlite.prepare(
                'SELECT count(*) AS rows FROM "' + name.replaceAll('"', '""') + '"').get().rows]));
            if (entry.rowCounts.ef_worlds) {
                entry.worlds = [];
                for (const row of sqlite.prepare('SELECT TotalClubs AS clubs, SeasonNumber AS season, RoundNumber AS round, SnapshotJson FROM ef_worlds ORDER BY SeasonNumber DESC').iterate()) {
                    const { SnapshotJson: json, ...header } = row;
                    const world = JSON.parse(json);
                    const counts = Object.fromEntries(['fixtures', 'transactions', 'auditLogs', 'playerStatistics', 'seasonHistories'].map(name => [name, {
                        rows: world[name]?.length ?? 0,
                        bytes: Buffer.byteLength(JSON.stringify(world[name] ?? [])),
                    }]));
                    entry.worlds.push({ ...header, bytes: Buffer.byteLength(json), gzipBytes: gzipSync(json).length, counts });
                }
                const dupes = sqlite.prepare(`SELECT count(*) AS groups FROM (
                    SELECT SnapshotJson FROM ef_worlds GROUP BY SnapshotJson HAVING count(*) > 1)`).get();
                entry.exactDuplicateSnapshotGroups = dupes.groups;
            }
            if (entry.rowCounts.ef_financial_ledger) {
                entry.ledgerSeasons = sqlite.prepare(`
                    SELECT SeasonNumber AS season, count(*) AS rows,
                    sum(length(CAST(Id AS blob)) + length(CAST(GameWorldId AS blob)) +
                        length(CAST(ClubId AS blob)) + length(CAST(Amount AS blob)) + length(CAST(BalanceAfter AS blob)) +
                        length(CAST(Description AS blob)) + length(CAST(IdempotencyKey AS blob)) + length(CAST(CreatedAtUtc AS blob))) AS textBytes
                    FROM ef_financial_ledger GROUP BY 1 ORDER BY 1`).all();
            }
        } finally { sqlite.close(); }
    } catch (error) {
        entry.error = String(error);
    }
}
writeFileSync(join(target, 'storage.json'), JSON.stringify(results, null, 2));
