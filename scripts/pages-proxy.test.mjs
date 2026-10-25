import assert from 'node:assert/strict';
import { after, test } from 'node:test';
import { onRequest } from '../web/functions/api/[[path]].ts';

const originalFetch = globalThis.fetch;
after(() => { globalThis.fetch = originalFetch; });

test('forwards method, path, query and session cookie to the fixed API origin', async () => {
  let forwarded;
  globalThis.fetch = async (request, options) => {
    forwarded = { request, options, body: await request.text() };
    return new Response('{"ok":true}', { status: 201, headers: {
      'content-type': 'application/json', 'set-cookie': 'chronicles_session=abc; HttpOnly; Secure; Path=/'
    } });
  };
  const request = new Request('https://cronicas.pages.dev/api/auth/login?next=campaigns', {
    method: 'POST', headers: { cookie: 'old=value', origin: 'https://cronicas.pages.dev', host: 'cronicas.pages.dev' },
    body: '{"email":"a@example.com"}'
  });
  const response = await onRequest({ env: { API_ORIGIN: 'https://cronicas-api.onrender.com' }, request });
  assert.equal(forwarded.request.url, 'https://cronicas-api.onrender.com/api/auth/login?next=campaigns');
  assert.equal(forwarded.request.method, 'POST');
  assert.equal(forwarded.request.headers.get('cookie'), 'old=value');
  assert.equal(forwarded.request.headers.get('host'), null);
  assert.equal(forwarded.body, '{"email":"a@example.com"}');
  assert.equal(forwarded.options.redirect, 'manual');
  assert.equal(response.status, 201);
  assert.match(response.headers.get('set-cookie'), /chronicles_session=abc/);
  assert.equal(response.headers.get('cache-control'), 'no-store');
});

test('rejects missing or unsafe API origins before fetching', async () => {
  globalThis.fetch = () => { throw new Error('must not fetch'); };
  for (const origin of [undefined, 'http://example.com', 'https://example.com/subpath', 'https://user:pass@example.com']) {
    const response = await onRequest({ env: { API_ORIGIN: origin }, request: new Request('https://cronicas.pages.dev/api/auth/me') });
    assert.equal(response.status, 503);
  }
});

test('returns a controlled error when the API is unreachable', async () => {
  globalThis.fetch = async () => { throw new Error('connection refused'); };
  const response = await onRequest({ env: { API_ORIGIN: 'https://cronicas-api.onrender.com' },
    request: new Request('https://cronicas.pages.dev/api/auth/me') });
  assert.equal(response.status, 502);
  assert.doesNotMatch(await response.text(), /connection refused/);
});

test('preserves a WebSocket upgrade response for signaling', async () => {
  const upgrade = { status: 101, webSocket: { readyState: 0 } };
  globalThis.fetch = async () => upgrade;
  const response = await onRequest({ env: { API_ORIGIN: 'https://cronicas-api.onrender.com' },
    request: new Request('https://cronicas.pages.dev/api/hubs/campaign?id=abc', { headers: { Upgrade: 'websocket' } }) });
  assert.equal(response, upgrade);
});
