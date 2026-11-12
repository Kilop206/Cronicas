// Local production-build preview using the real Pages HTML/security middleware.
// The only upstream allowed by this helper is the local development API.
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { onRequest } from '../web/functions/_middleware.ts';

const root = path.resolve(fileURLToPath(new URL('../web/dist/cronicas/browser/', import.meta.url)));
const port = Number(process.env.QUALITY_PORT || 8082);
const api = new URL(process.env.QUALITY_API_URL || 'http://127.0.0.1:5099');
if (api.protocol !== 'http:' || !['localhost', '127.0.0.1'].includes(api.hostname)) throw new Error('Local API required');
const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.svg': 'image/svg+xml', '.png': 'image/png', '.ico': 'image/x-icon', '.xml': 'application/xml', '.txt': 'text/plain' };
async function asset(target) {
  const pathname = decodeURIComponent(new URL(target instanceof Request ? target.url : target).pathname);
  const filename = path.resolve(root, '.' + (pathname === '/' ? '/index.html' : pathname));
  if (!filename.startsWith(root + path.sep)) return new Response(null, { status: 404 });
  try { return new Response(await readFile(filename), { headers: { 'content-type': types[path.extname(filename)] || 'application/octet-stream' } }); }
  catch { return new Response(null, { status: 404 }); }
}
createServer(async (incoming, outgoing) => {
  try {
    const url = new URL(incoming.url, `http://127.0.0.1:${port}`);
    const body = [];
    for await (const chunk of incoming) body.push(chunk);
    const request = new Request(url, { method: incoming.method, headers: incoming.headers, ...(!['GET', 'HEAD'].includes(incoming.method) ? { body: Buffer.concat(body) } : {}) });
    const response = await onRequest({ request, env: { ASSETS: { fetch: asset } }, next: async () => {
      if (!url.pathname.startsWith('/api/')) return asset(url);
      const headers = new Headers(request.headers); headers.delete('host');
      try { return await fetch(new URL(url.pathname + url.search, api), { method: request.method, headers, body: ['GET', 'HEAD'].includes(request.method) ? undefined : await request.arrayBuffer(), redirect: 'manual', signal: AbortSignal.timeout(95000) }); }
      catch { return Response.json({ message: 'Serviço indisponível.' }, { status: 502 }); }
    } });
    outgoing.writeHead(response.status, Object.fromEntries(response.headers));
    outgoing.end(Buffer.from(await response.arrayBuffer()));
  } catch { outgoing.writeHead(500); outgoing.end('Falha no preview local.'); }
}).listen(port, '127.0.0.1', () => console.log(`Quality preview: http://127.0.0.1:${port}`));
