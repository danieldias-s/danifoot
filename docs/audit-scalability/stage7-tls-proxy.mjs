import { readFileSync } from 'node:fs';
import { request } from 'node:http';
import { createServer } from 'node:https';
import { Transform } from 'node:stream';
import assert from 'node:assert/strict';

const [certificate, key] = process.argv.slice(2);
assert.ok(certificate && key);
const upstream = { hostname: '127.0.0.1', port: 54997 };
const transitMs = Number(process.env.AUDIT_TLS_TRANSIT_MS ?? 0);
assert.ok(Number.isInteger(transitMs) && transitMs >= 0 && transitMs <= 250);
class DelayedPipe extends Transform {
    _transform(chunk, encoding, complete) {
        setTimeout(() => complete(null, chunk), transitMs);
    }
}
function headers(input) {
    return { ...input.headers, 'x-forwarded-for': input.socket.remoteAddress, 'x-forwarded-proto': 'https' };
}

const proxy = createServer({ cert: readFileSync(certificate), key: readFileSync(key), minVersion: 'TLSv1.2' }, (input, output) => {
    const remote = request({ ...upstream, method: input.method, path: input.url, headers: headers(input) }, response => {
        output.writeHead(response.statusCode, response.headers);
        response.pipe(output);
    });
    remote.on('error', () => { if (!output.headersSent) output.writeHead(502); output.end(); });
    input.pipe(remote);
});

proxy.on('upgrade', (input, client, head) => {
    const remote = request({ ...upstream, method: 'GET', path: input.url, headers: headers(input) });
    remote.on('upgrade', (response, server, initial) => {
        const lines = [];
        for (let index = 0; index < response.rawHeaders.length; index += 2)
            lines.push(`${response.rawHeaders[index]}: ${response.rawHeaders[index + 1]}`);
        client.write(`HTTP/1.1 ${response.statusCode} ${response.statusMessage}\r\n${lines.join('\r\n')}\r\n\r\n`);
        if (initial.length) client.write(initial);
        if (head.length) server.write(head);
        client.on('error', () => server.destroy());
        server.on('error', () => client.destroy());
        client.on('close', () => server.destroy());
        server.on('close', () => client.destroy());
        if (transitMs) {
            const incoming = new DelayedPipe(), outgoing = new DelayedPipe();
            client.on('close', () => { incoming.destroy(); outgoing.destroy(); });
            client.pipe(incoming).pipe(server).pipe(outgoing).pipe(client);
        } else {
            client.pipe(server).pipe(client);
        }
    });
    remote.on('response', response => { response.resume(); client.end(`HTTP/1.1 ${response.statusCode} Rejected\r\nConnection: close\r\n\r\n`); });
    remote.on('error', () => client.destroy());
    remote.end();
});

proxy.listen(54996, '127.0.0.1', () => console.log(`TLS staging PID ${process.pid}`));
