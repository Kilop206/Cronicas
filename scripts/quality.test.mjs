import assert from 'node:assert/strict';
import { test } from 'node:test';
import { readFileSync } from 'node:fs';
import { onRequest } from '../web/functions/_middleware.ts';
import fallback from '../web/fallback-worker.mjs';
import { localDatetimeInput } from '../web/src/app/core/local-datetime.ts';
import { copyLink } from '../web/src/app/core/clipboard.ts';
import { safeReturnUrl } from '../web/src/app/core/return-url.ts';

const html = readFileSync(new URL('../web/dist/cronicas/browser/index.html', import.meta.url), 'utf8');
function context(path, hostname = 'cronicas-bsy.pages.dev', method = 'GET') {
  return { request: new Request(`https://${hostname}${path}`, { method }), env: { ASSETS: { fetch: async () => new Response(html, { headers: { 'content-type': 'text/html', 'etag': 'old-etag', 'content-length': '1' } }) } }, next: async () => new Response(html, { headers: { 'content-type': 'text/html' } }) };
}
test('server sends safe per-page metadata before Angular starts', async () => {
  for (const [path, title] of [['/login', 'Entrar'], ['/campaigns/new', 'Nova campanha'], ['/systems/new', 'Novo sistema'], ['/privacy', 'Política de Privacidade'], ['/campaigns/123/characters', 'Personagens'], ['/obs/private.token', 'Portrait para OBS']]) {
    const response = await onRequest(context(path)); const body = await response.text();
    assert.equal(response.status, 200); assert.ok(body.includes(`<title>${title} · Crônicas</title>`));
    assert.match(body, /name="robots" content="noindex/); assert.match(response.headers.get('x-robots-tag'), /noindex/);
    assert.equal(response.headers.get('cache-control'), 'no-store'); assert.equal(response.headers.get('etag'), null);
    assert.doesNotMatch(body, /rel="canonical"|property="og:url"|application\/ld\+json/);
    assert.doesNotMatch(body, /private\.token/);
    assert.match(body, /property="og:title" content="[^"<>]+ · Crônicas"/);
  }
});
test('unknown URLs return a real 404 and operational error page returns 500', async () => {
  for (const path of ['/missing', '/campaigns/id/nonexistent']) {
    const response = await onRequest(context(path)); assert.equal(response.status, 404);
    assert.match(await response.text(), /<title>Página não encontrada · Crônicas/);
  }
  assert.equal((await onRequest(context('/error'))).status, 500);
});
test('preview, queries and duplicate entry points cannot introduce indexable copies', async () => {
  const preview = await onRequest(context('/', 'preview.example.test'));
  assert.match(preview.headers.get('x-robots-tag'), /noindex/);
  const redirect = await onRequest(context('/index.html?token=secret')); assert.equal(redirect.status, 301);
  assert.equal(redirect.headers.get('location'), 'https://cronicas-bsy.pages.dev/');
  const slash = await onRequest(context('/privacy/')); assert.equal(slash.status, 301); assert.equal(slash.headers.get('location'), 'https://cronicas-bsy.pages.dev/privacy');
  const home = await onRequest(context('/?q=private')); assert.equal(home.headers.get('x-robots-tag'), null);
  assert.match(await home.text(), /rel="canonical" href="https:\/\/cronicas-bsy.pages.dev\/"/);
});
test('HEAD, mutation attempts and security policy behave consistently', async () => {
  assert.doesNotMatch(html, /\sonload=/i, 'Production styles must not require inline script handlers under the CSP');
  const head = await onRequest(context('/privacy', undefined, 'HEAD')); assert.equal(await head.text(), '');
  assert.equal((await onRequest(context('/privacy', undefined, 'POST'))).status, 405);
  const response = await onRequest(context('/'));
  assert.equal(response.headers.get('referrer-policy'), 'no-referrer');
  const csp = response.headers.get('content-security-policy');
  assert.match(csp, /script-src 'self';/); assert.doesNotMatch(csp, /unsafe-eval/);
  assert.match(response.headers.get('permissions-policy'), /camera=\(self\)/);
});
test('alternate host uses the same status/metadata and remains noindex', async () => {
  const ctx = context('/missing', 'fallback.example.test');
  const response = await fallback.fetch(ctx.request, ctx.env);
  assert.equal(response.status, 404); assert.match(response.headers.get('x-robots-tag'), /noindex/);
});
test('favicon derivatives have their declared pixel dimensions and contain no empty placeholders', () => {
  for (const [file, size] of [['favicon-16.png', 16], ['favicon-32.png', 32], ['favicon-48.png', 48], ['apple-touch-icon.png', 180]]) {
    const image = readFileSync(new URL(`../web/public/${file}`, import.meta.url));
    assert.equal(image.readUInt32BE(16), size); assert.equal(image.readUInt32BE(20), size); assert.ok(image.length > 100);
  }
});
test('editing a session keeps local wall time instead of slicing UTC', () => {
  const value = '2026-11-12T21:30:00Z'; const date = new Date(value);
  const local = localDatetimeInput(value); const result = new Date(local);
  assert.equal(result.getTime(), date.getTime()); assert.equal(localDatetimeInput(null), ''); assert.equal(localDatetimeInput('invalid'), '');
});
test('authentication return URLs preserve internal destinations without guest redirect loops', () => {
  for (const value of [null, '//external.test', 'https://external.test', '/\\external.test', '/login', '/register?returnUrl=/login', '/reset-password']) assert.equal(safeReturnUrl(value), '/dashboard');
  assert.equal(safeReturnUrl('/campaigns/id?tab=characters'), '/campaigns/id?tab=characters');
});
test('clipboard denial or absence produces usable feedback without rejected promises', async () => {
  assert.match(await copyLink({ defaultView: null }, 'link'), /copie manualmente/);
  assert.match(await copyLink({ defaultView: { navigator: { clipboard: { writeText: async () => { throw new Error('denied'); } } } } }, 'link'), /copie manualmente/);
  let copied;
  assert.equal(await copyLink({ defaultView: { navigator: { clipboard: { writeText: async value => { copied = value; } } } } }, 'link'), 'Link copiado.');
  assert.equal(copied, 'link');
});
