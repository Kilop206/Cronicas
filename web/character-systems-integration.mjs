import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
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
async function account(name) { return request('/api/auth/register', null, 'POST', { displayName: name, email: `${name}-${suffix}@example.test`, password: 'version-browser-local-123' }); }
const owner = await account('version-browser-owner'), player = await account('version-browser-player');
const fields = [
  { key: 'score', label: 'Atributo original', type: 'number', section: 'Atributos', min: 0, max: 20, options: null },
  { key: 'double_score', label: 'Valor derivado', type: 'number', section: 'Atributos', min: 0, max: 60, options: null, formula: 'score * 2' }
];
const model = (await request('/api/systems', owner.cookie, 'POST', { name: 'Modelo preservado', description: '', fields })).body;
const campaign = (await request('/api/campaigns', owner.cookie, 'POST', { name: 'Version browser fixture', description: '', accentColor: '#a54d52' })).body;
await request(`/api/campaigns/${campaign.id}/system`, owner.cookie, 'PATCH', { systemId: model.id });
const invite = (await request(`/api/campaigns/${campaign.id}/invites`, owner.cookie, 'POST', { role: 'player' })).body;
await request(`/api/invites/${invite.token}/accept`, player.cookie, 'POST', {});
const character = (await request(`/api/campaigns/${campaign.id}/characters`, player.cookie, 'POST', { name: 'Personagem com modelo fixado', portraitUrl: null, backgroundUrl: null,
  health: 10, maxHealth: 20, sanity: 10, maxSanity: 30, mana: 5, maxMana: 10, fields: { score: 2 }, version: 1 })).body;
const browser = await chromium.launch({ channel: 'chrome', headless: true }); const errors = [];
async function pageFor(user) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } }); const split = user.cookie.indexOf('=');
  await context.addCookies([{ name: user.cookie.slice(0, split), value: user.cookie.slice(split + 1), url: base }]);
  const page = await context.newPage(); page.on('pageerror', error => errors.push(error.message));
  await page.goto(`${base}/characters/${character.id}`, { waitUntil: 'networkidle' }); return page;
}
try {
  const masterPage = await pageFor(owner), playerPage = await pageFor(player);
  assert.equal(await playerPage.getByRole('button', { name: 'Fixar versão atual', exact: true }).count(), 0);
  await playerPage.getByLabel('Atributo original', { exact: true }).waitFor();
  await masterPage.locator('nav.sheet-tabs').getByRole('button', { name: 'Mestre', exact: true }).click();
  await masterPage.getByLabel('Segredos e planejamento', { exact: true }).fill('Unsaved private draft');
  await masterPage.locator('nav.sheet-tabs').getByRole('button', { name: 'Ficha', exact: true }).click();
  await masterPage.waitForFunction(() => document.querySelector('.sheet-version-status button')?.disabled === true);
  await masterPage.locator('nav.sheet-tabs').getByRole('button', { name: 'Mestre', exact: true }).click();
  await masterPage.getByLabel('Segredos e planejamento', { exact: true }).fill('');
  await masterPage.locator('nav.sheet-tabs').getByRole('button', { name: 'Ficha', exact: true }).click();
  const pinned = masterPage.waitForResponse(r => new URL(r.url()).pathname === `/api/v1/characters/${character.id}/system-version` && r.request().method() === 'POST');
  await masterPage.getByRole('button', { name: 'Fixar versão atual', exact: true }).click();
  const response = await pinned; assert.equal(response.status(), 200); const binding = (await response.json()).systemVersionId;
  await masterPage.locator('.sheet-version-status').filter({ hasText: 'Modelo fixado' }).waitFor();
  await playerPage.locator('.sheet-version-status').filter({ hasText: 'Modelo fixado' }).waitFor();
  await request(`/api/systems/${model.id}`, owner.cookie, 'PUT', { ...model, name: 'Modelo novo da campanha', fields: [
    { ...fields[0], key: 'renamed_score', label: 'Atributo novo' }, { ...fields[1], formula: 'renamed_score * 3' }
  ], fieldRenames: { score: 'renamed_score' } });
  await request(`/api/campaigns/${campaign.id}/system`, owner.cookie, 'PATCH', { systemId: null });
  await playerPage.reload({ waitUntil: 'networkidle' });
  await playerPage.getByLabel('Atributo original', { exact: true }).waitFor();
  assert.equal(await playerPage.getByLabel('Atributo novo', { exact: true }).count(), 0);
  const saved = playerPage.waitForResponse(r => new URL(r.url()).pathname === `/api/characters/${character.id}` && r.request().method() === 'PUT');
  await playerPage.getByLabel('Atributo original', { exact: true }).fill('4');
  const updated = await saved; assert.equal(updated.status(), 200);
  assert.equal((await updated.json()).fields.double_score, 8);
  await playerPage.reload({ waitUntil: 'networkidle' });
  await playerPage.getByLabel('Atributo original', { exact: true }).waitFor();
  await playerPage.waitForFunction(() => [...document.querySelectorAll('.dynamic-field label')]
    .find(label => label.textContent?.trim() === 'Atributo original')?.querySelector('input')?.value === '4');
  const data = playerPage.locator('details.obs-actions').filter({ has: playerPage.locator('summary').filter({ hasText: /^Dados estruturados$/ }) });
  await data.locator('summary').click();
  const download = playerPage.waitForEvent('download');
  await data.getByRole('button', { name: 'Exportar JSON', exact: true }).click();
  const exported = JSON.parse(fs.readFileSync(await (await download).path(), 'utf8'));
  assert.equal(exported.systemVersionId, binding); assert.equal(exported.systemDefinition.fields[0].key, 'score');
  assert.equal(exported.character.fields.double_score, 8);
  await data.locator('summary').click();
  const output = path.resolve(import.meta.dirname, '../docs/visual-validation/architecture'); fs.mkdirSync(output, { recursive: true });
  for (const [width, height] of [[1440, 900], [1920, 1080], [768, 1024], [390, 844]]) {
    await playerPage.setViewportSize({ width, height });
    await playerPage.screenshot({ path: path.join(output, `system-binding-${width}x${height}.png`), fullPage: true, animations: 'disabled' });
    assert.ok(await playerPage.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `Viewport overflow at ${width}`);
  }
  const abilityKeys = ['nivel', 'forca', 'destreza', 'constituicao', 'inteligencia', 'sabedoria', 'carisma', 'classe_armadura'];
  const dnd = (await request('/api/systems', owner.cookie, 'POST', { name: 'DND camera fixture', description: '', templateId: 'dnd_5e', templateVersion: 5,
    rulesetVersion: '2014', rendererMode: 'specialized', fields: abilityKeys.map(key => ({ key, label: key, section: 'Stats', type: 'number', min: 0, max: 99, options: null })) })).body;
  await request(`/api/campaigns/${campaign.id}/system`, owner.cookie, 'PATCH', { systemId: dnd.id });
  const cameraCharacter = (await request(`/api/campaigns/${campaign.id}/characters`, player.cookie, 'POST', { ...character, name: 'DND camera character', version: 1,
    fields: { nivel: 5, forca: 16, destreza: 12, constituicao: 14, inteligencia: 10, sabedoria: 12, carisma: 16, classe_armadura: 18 } })).body;
  await request(`/api/v1/characters/${cameraCharacter.id}/system-version`, owner.cookie, 'POST', { expectedVersion: 1, registrySystemId: dnd.id, templateRevision: 1 });
  await request(`/api/campaigns/${campaign.id}/system`, owner.cookie, 'PATCH', { systemId: null });
  const cameraPage = await playerPage.context().newPage(); cameraPage.on('pageerror', error => errors.push(error.message));
  await cameraPage.goto(`${base}/obs-camera/${campaign.id}/${cameraCharacter.id}`, { waitUntil: 'networkidle' });
  await cameraPage.waitForFunction(() => document.querySelectorAll('.obs-resources > div').length === 1);
  assert.equal((await cameraPage.locator('.obs-resources span').allTextContents()).join(','), 'HP', 'Camera source uses the pinned DND profile');
  await cameraPage.close();
  assert.deepEqual(errors, []);
  console.log('Character systems browser: master pin/draft guard, player SignalR reload, frozen fields/formula after campaign change, persistent edits, portable export, pinned OBS camera and four viewports/screenshots passed.');
} finally { await browser.close(); }
