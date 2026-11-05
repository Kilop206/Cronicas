import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import ts from 'typescript';
import { chromium } from 'playwright';

const base = process.env.ARCHITECTURE_BROWSER_URL || 'http://127.0.0.1:4200';
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Only local fixtures are allowed');
const output = path.resolve(import.meta.dirname, '../docs/visual-validation/architecture'); fs.mkdirSync(output, { recursive: true });
const exports = {};
vm.runInNewContext(ts.transpileModule(fs.readFileSync(new URL('./src/app/system-templates.ts', import.meta.url), 'utf8'), { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText, { exports });
const suffix = crypto.randomUUID();
async function request(route, cookie, method = 'GET', body) {
  const response = await fetch(new URL(route, base), { method, headers: { ...(cookie ? { Cookie: cookie } : {}), ...(body ? { 'Content-Type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const text = await response.text();
  return { status: response.status, body: text ? JSON.parse(text) : null, cookie: response.headers.get('set-cookie')?.split(';')[0] };
}
const owner = await request('/api/auth/register', null, 'POST', { displayName: 'Arquitetura', email: `architecture-browser-${suffix}@example.test`, password: 'local-architecture-browser-123' });
assert.equal(owner.status, 201, JSON.stringify(owner.body));
const fixtures = [];
for (const template of exports.SHEET_TEMPLATES.filter(t => ['ordem_paranormal', 'dnd_5e'].includes(t.id))) {
  const system = await request('/api/systems', owner.cookie, 'POST', { name: `Arquitetura ${template.id}`, description: '', fields: template.fields, layout: template.layout,
    templateId: template.id, templateVersion: template.version, rulesetVersion: template.rulesetVersion, rendererMode: 'specialized' });
  assert.equal(system.status, 201, JSON.stringify(system.body));
  const campaign = await request('/api/campaigns', owner.cookie, 'POST', { name: `Arquitetura ${template.id}`, description: '', accentColor: '#a54d52' });
  assert.equal(campaign.status, 201);
  assert.equal((await request(`/api/campaigns/${campaign.body.id}/system`, owner.cookie, 'PATCH', { systemId: system.body.id })).status, 204);
  const fields = Object.fromEntries(template.fields.filter(f => f.defaultValue !== undefined && f.defaultValue !== null).map(f => [f.key, f.defaultValue]));
  Object.assign(fields, template.id === 'ordem_paranormal' ? { nex: 35, classe_regra: 'especialista', origem: 'Acadêmico', classe: 'Especialista', trilha: 'Investigador', agilidade: 3, forca: 2, intelecto: 4, presenca: 2, vigor: 2, deslocamento: 9 }
    : { nivel: 5, classe: 'Paladino', especie: 'Humano', antecedente: 'Viajante', forca: 16, destreza: 12, constituicao: 14, inteligencia: 10, sabedoria: 12, carisma: 16, classe_armadura: 18, deslocamento: 9 });
  const character = await request(`/api/campaigns/${campaign.body.id}/characters`, owner.cookie, 'POST', { name: template.id === 'ordem_paranormal' ? 'Helena — validação de arquitetura' : 'Elias — validação de arquitetura',
    portraitUrl: null, backgroundUrl: null, health: 20, maxHealth: 30, sanity: 40, maxSanity: 50, mana: 10, maxMana: 20, fields });
  assert.equal(character.status, 201, JSON.stringify(character.body)); fixtures.push({ id: character.body.id, templateId: template.id });
}
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const errors = [];
const contexts = [];
async function context() {
  const result = await browser.newContext({ viewport: { width: 1440, height: 900 } }); contexts.push(result);
  const split = owner.cookie.indexOf('='); await result.addCookies([{ name: owner.cookie.slice(0, split), value: owner.cookie.slice(split + 1), url: base }]);
  const page = await result.newPage(); page.on('pageerror', e => errors.push(e.message)); return page;
}
async function open(page, id) {
  let reads = 0;
  const listener = response => { if (response.url().endsWith(`/api/characters/${id}`) && response.request().method() === 'GET' && response.status() === 200) reads++; };
  page.on('response', listener);
  await page.goto(`${base}/characters/${id}`, { waitUntil: 'networkidle' });
  await page.locator('.custom-sheet-main app-sheet-plugin-host').waitFor();
  for (let i = 0; i < 100 && reads < 2; i++) await new Promise(resolve => setTimeout(resolve, 50));
  page.off('response', listener);
  assert.ok(reads >= 2, 'Real browser hub subscribes and resynchronizes via REST');
  assert.equal(await page.getByText('Sincronização em tempo real indisponível.', { exact: false }).count(), 0);
}
try {
  const a = await context(); const b = await context(); const op = fixtures.find(f => f.templateId === 'ordem_paranormal');
  await open(a, op.id); await open(b, op.id);
  assert.equal(await a.locator('.custom-sheet-main .attribute-wheel').count(), 1);
  await a.getByRole('button', { name: 'Editar ficha', exact: true }).click();
  const name = a.getByRole('textbox', { name: 'Nome do agente', exact: true });
  await name.fill('R'); // Invalid local draft prevents autosave while testing a genuine remote update.
  const snapshot = (await request(`/api/characters/${op.id}`, owner.cookie)).body;
  const changed = await request(`/api/characters/${op.id}/rules`, owner.cookie, 'POST', { expectedVersion: snapshot.version, command: { action: 'damage', amount: 2, visibility: 'private' } });
  assert.equal(changed.status, 200, JSON.stringify(changed.body));
  await a.getByRole('alert').filter({ hasText: 'rascunho foi preservado' }).waitFor();
  assert.equal(await name.inputValue(), 'R', 'Remote state never replaces the local unsaved draft');
  await b.waitForFunction(() => document.querySelector('.custom-sheet-main [role="meter"][aria-label="Vida"]')?.getAttribute('aria-valuenow') === '18');
  await a.screenshot({ path: path.join(output, 'ordem-conflict-preserved.png'), fullPage: true });
  a.once('dialog', dialog => dialog.dismiss());
  await a.locator('.back-link').click();
  assert.ok(a.url().endsWith(`/characters/${op.id}`), 'Unsaved navigation can be cancelled');
  a.once('dialog', dialog => dialog.accept());
  await a.getByRole('button', { name: 'Recarregar ficha', exact: true }).click();
  await a.locator('.custom-sheet-main .attribute-wheel').waitFor();
  await a.getByRole('button', { name: 'Editar ficha', exact: true }).click();
  await a.locator('.custom-sheet-main').getByRole('button', { name: 'Combate', exact: true }).click();
  const saved = a.waitForResponse(response => response.url().endsWith(`/api/characters/${op.id}`) && response.request().method() === 'PUT');
  await a.getByRole('textbox', { name: 'Nome do agente', exact: true }).fill('Helena sincronizada');
  assert.equal((await saved).status(), 200);
  assert.equal(await a.locator('.custom-sheet-main').getByRole('button', { name: 'Combate', exact: true }).getAttribute('class'), 'active', 'Autosave keeps renderer navigation');
  await b.locator('.custom-sheet-main').getByRole('heading', { name: 'Helena sincronizada', exact: true }).waitFor();
  assert.equal((await request(`/api/characters/${op.id}`, owner.cookie)).body.name, 'Helena sincronizada');
  for (const fixture of fixtures) {
    await open(a, fixture.id);
    if (fixture.templateId === 'dnd_5e') assert.equal(await a.locator('.custom-sheet-main .attribute-wheel').count(), 0, 'D&D keeps its own composition');
    for (const [width, height] of [[1440, 900], [1920, 1080], [768, 1024], [390, 844]]) {
      await a.setViewportSize({ width, height });
      assert.ok(await a.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 2), 'No horizontal viewport overflow');
      await a.screenshot({ path: path.join(output, `${fixture.templateId}-${width}x${height}.png`), fullPage: true });
    }
  }
  assert.deepEqual(errors, []);
  console.log('Chrome + real API/PostgreSQL: OP/D&D plugin hosts, refresh, four viewports, live resync, preserved draft, cancelled navigation, autosave and retained tab passed; screenshots captured.');
} finally { await Promise.allSettled(contexts.map(c => c.close())); await browser.close(); }
