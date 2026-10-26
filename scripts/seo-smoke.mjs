import assert from 'node:assert/strict';

const base = process.env.SEO_BASE_URL || 'http://127.0.0.1:8788';
const production = new URL(base).hostname === 'cronicas-bsy.pages.dev';
const get = path => fetch(new URL(path, base));

const home = await get('/');
assert.equal(home.status, 200, 'A página inicial deve responder 200');
const html = await home.text();
assert.match(html, /<html lang="pt-BR"(?:\s|>)/);
assert.match(html, /<meta name="description"/);
assert.match(html, /<link rel="canonical" href="https:\/\/cronicas-bsy\.pages\.dev\/">/);
assert.match(html, /<meta property="og:image"/);
assert.match(html, /<script type="application\/ld\+json">/);
assert.match(html, /Histórias que/);
if (production) assert.equal(home.headers.get('x-robots-tag'), null, 'A página pública deve poder ser indexada');

const sitemap = await get('/sitemap.xml');
assert.equal(sitemap.status, 200);
const xml = await sitemap.text();
assert.deepEqual([...xml.matchAll(/<loc>([^<]+)<\/loc>/g)].map(match => match[1]), ['https://cronicas-bsy.pages.dev/']);

const robots = await get('/robots.txt');
assert.equal(robots.status, 200);
assert.match(await robots.text(), /Sitemap: https:\/\/cronicas-bsy\.pages\.dev\/sitemap\.xml/);

for (const path of ['/login', '/dashboard', '/campaigns/example', '/obs/example']) {
  const response = await get(path);
  assert.equal(response.status, 200, `${path} deve carregar a SPA`);
  assert.match(response.headers.get('x-robots-tag') || '', /noindex/, `${path} não deve ser indexada`);
}

const image = await get('/social-card.png');
assert.equal(image.status, 200);
assert.match(image.headers.get('content-type') || '', /image\/png/);

if (production) {
  const api = await get('/api/auth/me');
  assert.equal(api.status, 401, 'O proxy da API deve continuar acessível');
}

console.log(`SEO smoke passou em ${base}`);
