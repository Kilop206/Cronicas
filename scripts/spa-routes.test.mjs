import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { onRequest } from '../web/functions/_middleware.ts';

const index = readFileSync(new URL('../web/dist/cronicas/browser/index.html', import.meta.url), 'utf8');
const origin = 'https://cronicas-bsy.pages.dev';

function context(path) {
  const request = new Request(`${origin}${path}`);
  const requestedAssets = [];
  const env = { ASSETS: { fetch: async target => {
    requestedAssets.push(new URL(target).pathname);
    return new Response(index, { headers: { 'content-type': 'text/html; charset=utf-8' } });
  } } };
  return { request, env, requestedAssets, next: async () => new Response(index, { headers: { 'content-type': 'text/html; charset=utf-8' } }) };
}

test('deep links serve the SPA and resolve bundles from the site root', async () => {
  const invitation = `/invites/${'a'.repeat(48)}`;
  for (const path of ['/dashboard', '/campaigns/123/characters', invitation]) {
    const ctx = context(path);
    const response = await onRequest(ctx);
    const html = await response.text();
    assert.equal(response.status, 200);
    assert.deepEqual(ctx.requestedAssets, ['/']);
    assert.match(response.headers.get('x-robots-tag'), /noindex/);
    const base = html.match(/<base href="([^"]+)"/i)?.[1];
    assert.equal(base, '/', 'The document base must point to the site root');
    const bundlePaths = [...html.matchAll(/(?:src|href)="([^"]+\.(?:js|css))"/g)].map(match => match[1]);
    assert.ok(bundlePaths.some(path => path.includes('main-')), 'Angular bundle is present');
    for (const asset of bundlePaths) assert.equal(new URL(asset, new URL(base, ctx.request.url)).pathname, `/${asset.replace(/^\//, '')}`);
  }
});

test('the public home stays indexable and asset and API requests continue normally', async () => {
  const home = await onRequest(context('/'));
  assert.equal(home.headers.get('x-robots-tag'), null);
  for (const path of ['/main-EXAMPLE.js', '/api/auth/me']) {
    const ctx = context(path);
    await onRequest(ctx);
    assert.deepEqual(ctx.requestedAssets, []);
  }
});
