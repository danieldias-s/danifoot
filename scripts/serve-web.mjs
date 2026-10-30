import http from 'node:http';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = process.argv[3] ? path.resolve(process.argv[3]) : path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../client/elifoot_flutter/build/web');
const mime = {'.html':'text/html', '.js':'application/javascript', '.json':'application/json', '.wasm':'application/wasm', '.png':'image/png', '.ttf':'font/ttf', '.woff2':'font/woff2'};
http.createServer(async (req,res) => {
  try {
    const relative = decodeURIComponent(new URL(req.url,'http://localhost').pathname);
    const file = path.resolve(root, '.' + (relative === '/' ? '/index.html' : relative));
    if (!file.startsWith(root + path.sep)) { res.writeHead(403).end(); return; }
    const body = await fs.readFile(file);
    res.writeHead(200, {'Content-Type':mime[path.extname(file)] ?? 'application/octet-stream','Cache-Control':'no-store'}).end(body);
  } catch { res.writeHead(404).end('Not found'); }
}).listen(Number(process.argv[2] ?? 8080), '127.0.0.1', () => console.log('Elifoot Web: http://localhost:' + (process.argv[2] ?? 8080)));
