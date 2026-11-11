import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from 'playwright';

const base = process.env.ARCHITECTURE_BROWSER_URL || 'http://127.0.0.1:8082';
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Only local fixtures are allowed');
const suffix = crypto.randomUUID();
async function request(route, cookie, method = 'GET', body) {
  const r = await fetch(new URL(route, base), { method, signal: AbortSignal.timeout(15000), headers: { ...(cookie ? { Cookie: cookie } : {}), ...(body ? { 'Content-Type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const text = await r.text(); const result = { status: r.status, body: text ? JSON.parse(text) : null, cookie: r.headers.get('set-cookie')?.split(';')[0] };
  assert.ok(r.ok, JSON.stringify(result)); return result;
}
async function account(name) { return request('/api/auth/register', null, 'POST', { displayName: name, email: `${name}-${suffix}@example.test`, password: 'association-browser-local-123' }); }
const owner = await account('association-browser-owner'), player = await account('association-browser-player');
const field = { key: 'score', label: 'Atributo original', type: 'number', section: 'Atributos', min: 0, max: 20, options: null };
const first = (await request('/api/systems', owner.cookie, 'POST', { name: 'Modelo de origem', description: '', fields: [field] })).body;
const second = (await request('/api/systems', owner.cookie, 'POST', { name: 'Novo modelo associado', description: '', fields: [{ ...field, label: 'Atributo do novo modelo' }] })).body;
const world = (await request('/api/worlds', owner.cookie, 'POST', { name: 'Universo associado', description: '' })).body;
const campaign = (await request('/api/campaigns', owner.cookie, 'POST', { name: 'Associações integradas', description: '', accentColor: '#a54d52' })).body;
const campaignRoute = `/api/campaigns/${campaign.id}`;
await request(`${campaignRoute}/system`, owner.cookie, 'PATCH', { systemId: first.id });
const invite = (await request(`${campaignRoute}/invites`, owner.cookie, 'POST', { role: 'player' })).body;
await request(`/api/invites/${invite.token}/accept`, player.cookie, 'POST', {});
const input = { name: 'Ficha que acompanha a campanha', portraitUrl: null, backgroundUrl: null, health: 3, maxHealth: 20, sanity: 4, maxSanity: 10, mana: 2, maxMana: 10, fields: { score: 4 }, version: 1 };
const dynamic = (await request(`${campaignRoute}/characters`, player.cookie, 'POST', input)).body;
let pinned = (await request(`${campaignRoute}/characters`, player.cookie, 'POST', { ...input, name: 'Ficha com modelo fixado' })).body;
pinned = (await request(`/api/v1/characters/${pinned.id}/system-version`, owner.cookie, 'POST', { expectedVersion: 1, registrySystemId: first.id, templateRevision: first.revision })).body;
const browser = await chromium.launch({ channel: 'chrome', headless: true }); const errors = [];
async function contextFor(account) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } }); const split = account.cookie.indexOf('=');
  await context.addCookies([{ name: account.cookie.slice(0, split), value: account.cookie.slice(split + 1), url: base }]); return context;
}
try {
  const masterContext = await contextFor(owner), playerContext = await contextFor(player);
  const campaignPage = await masterContext.newPage(), notesPage = await masterContext.newPage(), livePage = await playerContext.newPage(), fixedPage = await playerContext.newPage();
  for (const page of [campaignPage, notesPage, livePage, fixedPage]) page.on('pageerror', error => errors.push(error.message));
  await campaignPage.goto(`${base}/campaigns/${campaign.id}`, { waitUntil: 'networkidle' });
  await notesPage.goto(`${base}/characters/${dynamic.id}`, { waitUntil: 'networkidle' });
  await livePage.goto(`${base}/characters/${dynamic.id}`, { waitUntil: 'networkidle' });
  await fixedPage.goto(`${base}/characters/${pinned.id}`, { waitUntil: 'networkidle' });
  await livePage.getByLabel('Atributo original', { exact: true }).waitFor();
  await fixedPage.getByLabel('Atributo original', { exact: true }).waitFor();
  // Let initial queued creation/association events settle before deliberately leaving drafts pending.
  await new Promise(resolve => setTimeout(resolve, 2300));
  await livePage.getByLabel('Nome', { exact: true }).fill('R');
  await notesPage.locator('nav.sheet-tabs').getByRole('button', { name: 'Mestre', exact: true }).click();
  await notesPage.getByLabel('Segredos e planejamento', { exact: true }).fill('Rascunho privado preservado');
  const systems = campaignPage.locator('app-campaign-system-panel'), worlds = campaignPage.locator('app-campaign-world-panel');
  await worlds.getByRole('combobox').selectOption(world.id);
  await worlds.getByRole('button', { name: 'Associar universo', exact: true }).click();
  await worlds.getByRole('link', { name: 'Abrir universo', exact: true }).waitFor();
  await systems.getByRole('combobox').selectOption(second.id);
  await systems.getByRole('button', { name: 'Aplicar sistema', exact: true }).click();
  await systems.getByRole('status').filter({ hasText: 'Sistema da campanha atualizado.' }).waitFor();
  await livePage.getByRole('alert').filter({ hasText: 'rascunho foi preservado' }).waitFor();
  await notesPage.getByRole('alert').filter({ hasText: 'rascunho foi preservado' }).waitFor();
  assert.equal(await livePage.getByLabel('Nome', { exact: true }).inputValue(), 'R');
  assert.equal(await notesPage.getByLabel('Segredos e planejamento', { exact: true }).inputValue(), 'Rascunho privado preservado');
  assert.equal(await fixedPage.getByLabel('Atributo original', { exact: true }).inputValue(), '4');
  assert.equal(await fixedPage.getByLabel('Atributo do novo modelo', { exact: true }).count(), 0);
  const output = path.resolve(import.meta.dirname, '../docs/visual-validation/architecture'); fs.mkdirSync(output, { recursive: true });
  await livePage.screenshot({ path: path.join(output, 'campaign-association-draft.png'), fullPage: true, animations: 'disabled' });
  livePage.once('dialog', dialog => void dialog.accept()); await livePage.getByRole('button', { name: 'Recarregar ficha', exact: true }).click();
  await livePage.getByLabel('Atributo do novo modelo', { exact: true }).waitFor();
  assert.equal(await livePage.getByLabel('Atributo do novo modelo', { exact: true }).inputValue(), '4');
  for (const [width, height] of [[1440, 900], [390, 844]]) {
    await livePage.setViewportSize({ width, height });
    await livePage.screenshot({ path: path.join(output, `campaign-association-${width}x${height}.png`), fullPage: true, animations: 'disabled' });
    assert.ok(await livePage.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `Viewport overflow at ${width}`);
  }
  await notesPage.getByLabel('Segredos e planejamento', { exact: true }).fill('');
  notesPage.once('dialog', dialog => void dialog.accept()); await notesPage.getByRole('button', { name: 'Recarregar ficha', exact: true }).click();
  await notesPage.getByLabel('Atributo do novo modelo', { exact: true }).waitFor();
  // With no pending drafts, the next association update must reload the effective renderer automatically.
  await systems.getByRole('combobox').selectOption(first.id);
  await systems.getByRole('button', { name: 'Aplicar sistema', exact: true }).click();
  await livePage.getByLabel('Atributo original', { exact: true }).waitFor();
  await notesPage.getByLabel('Atributo original', { exact: true }).waitFor();
  await systems.getByRole('combobox').selectOption(''); await systems.getByRole('button', { name: 'Aplicar sistema', exact: true }).click();
  await livePage.getByLabel('Atributo original', { exact: true }).waitFor({ state: 'detached' });
  assert.equal((await request(`/api/characters/${dynamic.id}`, player.cookie)).body.version, dynamic.version);
  assert.equal((await request(`/api/characters/${dynamic.id}`, player.cookie)).body.fields.score, 4);
  assert.equal((await request(`/api/characters/${pinned.id}`, player.cookie)).body.systemVersionId, pinned.systemVersionId);
  await campaignPage.reload({ waitUntil: 'networkidle' });
  await worlds.getByRole('link', { name: 'Abrir universo', exact: true }).waitFor();
  assert.equal((await request(campaignRoute, owner.cookie)).body.worldId, world.id);
  assert.deepEqual(errors, []);
  console.log('Campaign association Chrome: existing controls, world persistence, live model changes, preserved character/private drafts, explicit reload, automatic clean resync, pinned isolation, detach and desktop/mobile screenshots passed.');
} finally { await browser.close(); }
