import { createWriteStream } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { setTimeout as delay } from 'node:timers/promises';

const [psql, database, filename, seconds = '180', clusterPid] = process.argv.slice(2);
if (!psql || !/^audit_[a-z0-9_]+$/.test(database ?? '') || !filename)
    throw Error('Uso: node stage4-telemetry.mjs psql.exe audit_banco saida.jsonl [segundos] [pid-do-cluster]');
const out = createWriteStream(filename, { flags: 'wx' });
const limit = Date.now() + Number(seconds) * 1000;
const sql = `
SELECT json_build_object('connections', count(*),
  'active', count(*) FILTER (WHERE state='active'),
  'lockWait', count(*) FILTER (WHERE wait_event_type='Lock'),
  'ioWait', count(*) FILTER (WHERE wait_event_type='IO'),
  'blocked', count(*) FILTER (WHERE cardinality(pg_blocking_pids(pid))>0))
FROM pg_stat_activity
WHERE datname=current_database() AND pid<>pg_backend_pid()
`;
for (let t = Date.now(); t < limit; t = Date.now()) {
    const pg = spawnSync(psql, ['-h','127.0.0.1','-p','54983','-U','audit','-d',database,'-Atc',sql],
        { encoding: 'utf8', windowsHide: true, timeout: 3000 });
    const pgMetric = pg.status === 0 ? JSON.parse(pg.stdout.trim()) : { sampleError: true };
    const postgresRoot = /^[1-9][0-9]*$/.test(clusterPid ?? '') ? spawnSync('pwsh', ['-NoProfile','-Command',
        `$x=Get-Process -Id ${clusterPid} -ErrorAction SilentlyContinue; if ($x) { @{cpuSec=$x.CPU;rss=$x.WorkingSet64} | ConvertTo-Json -Compress }`],
        { encoding: 'utf8', windowsHide: true, timeout: 4000 }) : null;
    out.write(JSON.stringify({ ms: t, ...pgMetric,
        postgresRoot: postgresRoot?.stdout?.trim() ? JSON.parse(postgresRoot.stdout) : null }) + '\n');
    await delay(750);
}
out.end();
