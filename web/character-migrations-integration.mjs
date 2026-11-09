import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from 'playwright';

const base = process.env.ARCHITECTURE_BROWSER_URL || 'http://127.0.0.1:8082';
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Only local fixtures are allowed');
const suffix = crypto.randomUUID();
async function request(route, cookie, method = 'GET', body) {
  const r = await fetch(new URL(route, base), { method, headers: { ...(cookie ? { Cookie: cookie } : {}), ...(body ? { 'Content-Type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const text = await r.text(); const result = { status: r.status, body: text ? JSON.parse(text) : null, cookie: r.headers.get('set-cookie')?.split(';')[0] };
  assert.ok(r.ok, JSON.stringify(result)); return result;
}
async function account(name) { return request('/api/auth/register', null, 'POST', { displayName: name, email: `${name}-${suffix}@example.test`, password: 'migration-browser-local-123' }); }
const owner = await account('migration-browser-owner'), player = await account('migration-browser-player');
const fields = [{ key: 'score', label: 'Atributo original', type: 'number', section: 'Atributos', min: 0, max: 20, options: null }];
let model = (await request('/api/systems', owner.cookie, 'POST', { name: 'Modelo original', description: '', fields })).body;
const campaign = (await request('/api/campaigns', owner.cookie, 'POST', { name: `Migração visual ${suffix}`, description: '', accentColor: '#a54d52' })).body;
await request(`/api/campaigns/${campaign.id}/system`, owner.cookie, 'PATCH', { systemId: model.id });
const invite = (await request(`/api/campaigns/${campaign.id}/invites`, owner.cookie, 'POST', { role: 'player' })).body;
await request(`/api/invites/${invite.token}/accept`, player.cookie, 'POST', {});
let character = (await request(`/api/campaigns/${campaign.id}/characters`, player.cookie, 'POST', { name: 'Personagem em migração', portraitUrl: null, backgroundUrl: null, health: 3, maxHealth: 20, sanity: 4, maxSanity: 10, mana: 2, maxMana: 10, fields: { score: 4 }, version: 1 })).body;
character = (await request(`/api/v1/characters/${character.id}/system-version`, owner.cookie, 'POST', { expectedVersion: 1, registrySystemId: model.id, templateRevision: 1 })).body;
const original = character.systemVersionId;
model = (await request(`/api/systems/${model.id}`, owner.cookie, 'PUT', { ...model, name: 'Modelo revisado', fields: [{ ...fields[0], key: 'new_score', label: 'Atributo novo', max: 10 }, { ...fields[0], key: 'derived', label: 'Resultado calculado', formula: 'new_score * 3', max: 100 }] })).body;
const browser = await chromium.launch({ channel: 'chrome', headless: true }); const errors = []; const pages = [];
async function pageFor(user) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } }); const split = user.cookie.indexOf('=');
  await context.addCookies([{ name: user.cookie.slice(0, split), value: user.cookie.slice(split + 1), url: base }]);
  const page = await context.newPage(); pages.push(page); page.on('pageerror', e => errors.push(e.message)); await page.goto(`${base}/characters/${character.id}`, { waitUntil: 'networkidle' }); return page;
}
try {
  const masterPage = await pageFor(owner), playerPage = await pageFor(player);
  await playerPage.getByLabel('Atributo original', { exact: true }).waitFor();
  assert.equal(await playerPage.getByRole('button', { name: 'Migrar versão da ficha', exact: true }).count(), 0);
  const migrationButton = masterPage.getByRole('button', { name: 'Migrar versão da ficha', exact: true });
  await masterPage.locator('nav.sheet-tabs').getByRole('button', { name: 'Mestre', exact: true }).click();
  await masterPage.getByLabel('Segredos e planejamento', { exact: true }).fill('Rascunho privado');
  await masterPage.locator('nav.sheet-tabs').getByRole('button', { name: 'Ficha', exact: true }).click();
  assert.equal(await migrationButton.isDisabled(), true, 'private draft prevents migration');
  await masterPage.locator('nav.sheet-tabs').getByRole('button', { name: 'Mestre', exact: true }).click();
  await masterPage.getByLabel('Segredos e planejamento', { exact: true }).fill('');
  await masterPage.locator('nav.sheet-tabs').getByRole('button', { name: 'Ficha', exact: true }).click();
  await migrationButton.click();
  const panel = masterPage.getByRole('region', { name: 'Migração assistida de versão', exact: true });
  await panel.getByRole('combobox', { name: /^Versão de destino/ }).selectOption('current');
  await panel.getByLabel('Destino de score', { exact: true }).selectOption('new_score');
  await panel.getByRole('button', { name: 'Gerar prévia da migração', exact: true }).click();
  const review = panel.getByRole('region', { name: 'Prévia da migração', exact: true });
  await review.getByRole('heading', { name: /Prévia · Modelo revisado/ }).waitFor();
  assert.equal(await review.getByRole('button', { name: 'Aplicar migração revisada', exact: true }).isDisabled(), true);
  assert.equal((await request(`/api/characters/${character.id}`, owner.cookie)).body.version, 2, 'preview is read-only');
  const output = path.resolve(import.meta.dirname, '../docs/visual-validation/architecture'); fs.mkdirSync(output, { recursive: true });
  for (const [width, height] of [[1440, 900], [1920, 1080], [768, 1024], [390, 844]]) {
    await masterPage.setViewportSize({ width, height });
    await masterPage.screenshot({ path: path.join(output, `assisted-migration-${width}x${height}.png`), fullPage: true, animations: 'disabled' });
    assert.ok(await masterPage.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `Viewport overflow at ${width}`);
  }
  await masterPage.setViewportSize({ width: 1440, height: 900 });
  // Any plan edit discards confirmation and the old preview.
  await panel.getByRole('checkbox', { name: 'Revisei o destino, os valores e os campos preservados.', exact: true }).check();
  await panel.getByRole('checkbox', { name: 'Ajustar Atributo novo', exact: true }).check();
  await review.waitFor({ state: 'detached' });
  await panel.getByLabel('Destino de score', { exact: true }).selectOption('');
  await panel.getByLabel('Valor de Atributo novo', { exact: true }).fill('99');
  await panel.getByRole('button', { name: 'Gerar prévia da migração', exact: true }).click();
  await review.getByRole('alert').filter({ hasText: /tipo ou limites incompatíveis/ }).waitFor();
  assert.equal(await review.getByRole('button', { name: 'Aplicar migração revisada', exact: true }).isDisabled(), true);
  await panel.getByLabel('Valor de Atributo novo', { exact: true }).fill('3');
  await panel.getByRole('button', { name: 'Gerar prévia da migração', exact: true }).click();
  await review.getByRole('heading').waitFor();
  await review.getByRole('checkbox', { name: 'Revisei o destino, os valores e os campos preservados.', exact: true }).check();
  const applied = masterPage.waitForResponse(r => new URL(r.url()).pathname === `/api/v1/characters/${character.id}/system-migrations` && r.request().method() === 'POST');
  await review.getByRole('button', { name: 'Aplicar migração revisada', exact: true }).click();
  assert.equal((await applied).status(), 200);
  await masterPage.locator('.sheet-version-status').filter({ hasText: 'Modelo revisado' }).waitFor();
  await playerPage.getByLabel('Atributo novo', { exact: true }).waitFor();
  character = (await request(`/api/characters/${character.id}`, player.cookie)).body;
  assert.equal(character.fields.new_score, 3); assert.equal(character.fields.derived, 9); assert.equal(character.fields.score, 4); assert.equal(character.health, 3);
  await playerPage.reload({ waitUntil: 'networkidle' });
  await playerPage.getByLabel('Atributo novo', { exact: true }).waitFor();
  await masterPage.getByRole('button', { name: 'Migrar versão da ficha', exact: true }).click();
  await panel.getByText('Migrações anteriores (até 50)', { exact: true }).waitFor();
  await panel.getByRole('combobox', { name: /^Versão de destino/ }).selectOption(original);
  await panel.getByRole('button', { name: 'Gerar prévia da migração', exact: true }).click();
  await review.getByRole('heading', { name: /Prévia · Modelo original/ }).waitFor();
  await review.getByRole('checkbox', { name: 'Revisei o destino, os valores e os campos preservados.', exact: true }).check();
  const reverse = masterPage.waitForResponse(r => new URL(r.url()).pathname.endsWith('/system-migrations') && r.request().method() === 'POST');
  await review.getByRole('button', { name: 'Aplicar migração revisada', exact: true }).click();
  assert.equal((await reverse).status(), 200);
  await playerPage.getByLabel('Atributo original', { exact: true }).waitFor();
  assert.equal((await request(`/api/characters/${character.id}`, player.cookie)).body.systemVersionId, original);
  assert.deepEqual(errors, []);
  console.log('Assisted migration Chrome: master-only controls, private draft guard, mapped preview, four screenshots/viewports, edit invalidation, invalid/valid adjustments, explicit confirmation, autosaved state, realtime player refresh, audit and reverse migration passed.');
} catch (error) {
  if (pages[0]) {
    await pages[0].screenshot({ path: path.resolve(import.meta.dirname, '../docs/visual-validation/architecture/assisted-migration-failure.png'), fullPage: true });
    console.error(await pages[0].locator('app-character-system-migration').innerText());
  }
  throw error;
} finally { await browser.close(); }
