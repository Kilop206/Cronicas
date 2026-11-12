import assert from 'node:assert/strict';
import { chromium } from 'playwright';

const base = process.env.ARCHITECTURE_BROWSER_URL || 'http://127.0.0.1:8082';
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Only local fixtures are allowed');
const suffix = crypto.randomUUID();
async function request(route, cookie, method = 'GET', body) {
  const r = await fetch(new URL(route, base), { method, headers: { ...(cookie ? { Cookie: cookie } : {}),
    ...(body ? { 'Content-Type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const text = await r.text(); const result = { status: r.status, body: text ? JSON.parse(text) : null, cookie: r.headers.get('set-cookie')?.split(';')[0] };
  assert.ok(r.ok, JSON.stringify(result)); return result;
}
async function account(name) { return request('/api/auth/register', null, 'POST', { displayName: name, email: `${name}-${suffix}@example.test`, password: 'companions-browser-local-123' }); }
const owner = await account('companion-browser-owner'), player = await account('companion-browser-player');
const campaign = (await request('/api/campaigns', owner.cookie, 'POST', { name: 'Companion browser fixture', description: '', accentColor: '#a54d52' })).body;
const invite = (await request(`/api/campaigns/${campaign.id}/invites`, owner.cookie, 'POST', { role: 'player' })).body;
await request(`/api/invites/${invite.token}/accept`, player.cookie, 'POST', {});
const character = (await request(`/api/campaigns/${campaign.id}/characters`, owner.cookie, 'POST', { name: 'Companion browser character', portraitUrl: null, backgroundUrl: null,
  health: 10, maxHealth: 20, sanity: 10, maxSanity: 30, mana: 5, maxMana: 10, fields: {}, version: 1 })).body;
const route = `/api/characters/${character.id}`;
const browser = await chromium.launch({ channel: 'chrome', headless: true }); const errors = [];
async function pageFor(user, viewport = { width: 1440, height: 900 }) {
  const context = await browser.newContext({ viewport });
  if (user) { const split = user.cookie.indexOf('='); await context.addCookies([{ name: user.cookie.slice(0, split), value: user.cookie.slice(split + 1), url: base }]); }
  const page = await context.newPage(); page.on('pageerror', e => errors.push(e.message)); return page;
}
try {
  const page = await pageFor(owner);
  await page.goto(`${base}/characters/${character.id}`, { waitUntil: 'networkidle' });
  const favorite = page.getByRole('button', { name: 'Adicionar aos favoritos', exact: true });
  const favored = page.waitForResponse(r => new URL(r.url()).pathname === `${route}/favorite` && r.request().method() === 'PUT');
  await favorite.click(); assert.equal((await favored).status(), 204);
  await page.getByRole('button', { name: 'Remover dos favoritos', exact: true }).waitFor();
  await page.reload({ waitUntil: 'networkidle' });
  await page.getByRole('button', { name: 'Remover dos favoritos', exact: true }).waitFor();
  const tabs = page.locator('nav.sheet-tabs');
  await tabs.getByRole('button', { name: 'Mestre', exact: true }).click();
  const notes = page.getByLabel('Segredos e planejamento', { exact: true });
  await notes.fill('Local initial draft');
  await request(`${route}/secret`, owner.cookie, 'PUT', { notes: 'Remote initial plan', revision: 1, expectedExists: false });
  const stale = page.waitForResponse(r => new URL(r.url()).pathname === `${route}/secret` && r.request().method() === 'PUT');
  await page.getByRole('button', { name: 'Salvar notas secretas', exact: true }).click();
  const rejected = await stale; assert.equal(rejected.request().postDataJSON().expectedExists, false); assert.equal(rejected.status(), 409);
  await page.getByRole('alert').filter({ hasText: 'anotações mudaram' }).waitFor();
  assert.equal(await notes.inputValue(), 'Local initial draft', 'Initial revision conflict keeps private draft');
  await page.reload({ waitUntil: 'networkidle' });
  await tabs.getByRole('button', { name: 'Mestre', exact: true }).click();
  await page.waitForFunction(() => document.querySelector('.character-secret textarea')?.value === 'Remote initial plan');
  await notes.fill('Private browser plan');
  const saved = page.waitForResponse(r => new URL(r.url()).pathname === `${route}/secret` && r.request().method() === 'PUT');
  await page.getByRole('button', { name: 'Salvar notas secretas', exact: true }).click();
  const response = await saved; assert.equal(response.status(), 200); assert.equal(response.request().postDataJSON().expectedExists, true);
  await page.getByRole('status').filter({ hasText: 'Notas salvas.' }).waitFor();
  await tabs.getByRole('button', { name: 'Ficha', exact: true }).click();
  const source = page.locator('details.obs-actions').filter({ has: page.locator('summary').filter({ hasText: /^Fontes para OBS$/ }) });
  await source.locator('summary').click();
  const [generated] = await Promise.all([
    page.waitForResponse(r => new URL(r.url()).pathname === `${route}/overlay-key` && r.request().method() === 'POST'),
    source.getByRole('button', { name: 'Gerar nova URL', exact: true }).click()
  ]);
  const first = (await generated.json()).key;
  await source.locator('code').filter({ hasText: `/obs/${first}` }).waitFor();
  const publicPage = await pageFor(null, { width: 390, height: 844 });
  await publicPage.goto(`${base}/obs/${first}`, { waitUntil: 'networkidle' });
  await publicPage.getByRole('heading', { name: character.name, exact: true }).waitFor();
  assert.equal(await publicPage.locator('.obs-resources > div').count(), 3);
  assert.ok(!(await publicPage.locator('body').innerText()).includes('Private browser plan'));
  const [rotated] = await Promise.all([
    page.waitForResponse(r => new URL(r.url()).pathname === `${route}/overlay-key` && r.request().method() === 'POST'),
    source.getByRole('button', { name: 'Gerar nova URL', exact: true }).click()
  ]);
  const next = (await rotated.json()).key; assert.notEqual(first, next);
  await publicPage.getByRole('alert').filter({ hasText: 'Fonte indisponível' }).waitFor();
  await publicPage.goto(`${base}/obs/${next}`, { waitUntil: 'networkidle' });
  await publicPage.getByRole('heading', { name: character.name, exact: true }).waitFor();
  const playerPage = await pageFor(player);
  await playerPage.goto(`${base}/characters/${character.id}`, { waitUntil: 'networkidle' });
  assert.equal(await playerPage.locator('nav.sheet-tabs').getByRole('button', { name: 'Mestre', exact: true }).count(), 0);
  await playerPage.getByRole('button', { name: 'Adicionar aos favoritos', exact: true }).waitFor();
  assert.ok(!(await playerPage.locator('body').innerText()).includes('Private browser plan'));
  assert.deepEqual(errors, []);
  console.log('Companion browser: personal favorite/reload, initial-secret conflict with draft preserved, private save, owner OBS URL, anonymous mobile source/revocation and player isolation passed.');
} catch (error) {
  console.error('Browser errors:', errors);
  for (const context of browser.contexts()) for (const page of context.pages()) {
    console.error('Alerts:', await page.getByRole('alert').allTextContents());
    console.error('OBS button state:', await page.locator('details.obs-actions button').evaluateAll(buttons => buttons.map(b => ({ text: b.textContent, disabled: b.disabled }))));
  }
  throw error;
} finally { await browser.close(); }
