import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import ts from 'typescript';
import { chromium } from 'playwright';

const base = process.env.RULES_TEST_URL || 'http://127.0.0.1:4200';
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'This suite only writes to a local test server');
const source = fs.readFileSync(new URL('./src/app/system-templates.ts', import.meta.url), 'utf8');
const exports = {};
vm.runInNewContext(ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText, { exports });
const suffix = crypto.randomUUID();
async function request(route, cookie, method = 'GET', body) {
  const response = await fetch(`${base}${route}`, { method, headers: { ...(cookie ? { cookie } : {}), ...(body ? { 'content-type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const text = await response.text();
  return { status: response.status, body: text ? JSON.parse(text) : null, cookie: response.headers.get('set-cookie')?.split(';')[0] };
}
async function register(name) {
  const result = await request('/api/auth/register', null, 'POST', { displayName: name, email: `${name}-${suffix}@example.test`, password: 'local-rules-password-123' });
  assert.equal(result.status, 201, JSON.stringify(result.body)); return result.cookie;
}
const owner = await register('rules-owner'); const player = await register('rules-player'); const spectator = await register('rules-spectator');
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const context = await browser.newContext();
const cookieIndex = owner.indexOf('=');
await context.addCookies([{ name: owner.slice(0, cookieIndex), value: owner.slice(cookieIndex + 1), url: base }]);
const page = await context.newPage();
const errors = []; page.on('pageerror', error => errors.push(error.message));
const out = path.resolve(import.meta.dirname, '../docs/visual-validation/rules'); fs.mkdirSync(out, { recursive: true });
try {
  for (const template of exports.SHEET_TEMPLATES) {
    const system = await request('/api/systems', owner, 'POST', { name: `Teste ${template.id}`, description: '', fields: template.fields, layout: template.layout, templateId: template.id, templateVersion: template.version, rulesetVersion: template.rulesetVersion, rendererMode: 'specialized', npcTemplates: [] });
    assert.equal(system.status, 201, JSON.stringify(system.body));
    const campaign = await request('/api/campaigns', owner, 'POST', { name: `Regras ${template.id}`, description: '', accentColor: '#a54d52' });
    assert.equal(campaign.status, 201);
    assert.equal((await request(`/api/campaigns/${campaign.body.id}/system`, owner, 'PATCH', { systemId: system.body.id })).status, 204);
    for (const [cookie, role] of [[player, 'player'], [spectator, 'spectator']]) {
      const invite = await request(`/api/campaigns/${campaign.body.id}/invites`, owner, 'POST', { role }); assert.equal(invite.status, 201);
      assert.equal((await request(`/api/invites/${invite.body.token}/accept`, cookie, 'POST', {})).status, 200);
    }
    const fields = Object.fromEntries(template.fields.filter(field => field.defaultValue !== undefined && field.defaultValue !== null).map(field => [field.key, field.defaultValue]));
    Object.assign(fields, template.id === 'ordem_paranormal' ? { nex: 35, classe_regra: 'especialista' } : template.id === 'call_of_cthulhu_7e' ? { constituicao: 60, tamanho: 50, forca: 65, destreza: 60, idade: 40, inteligencia: 70 } : { nivel: 4 });
    fields.ataques_regras = [{ nome: 'Ataque de validação', base: template.id === 'dnd_5e' ? 'mod_forca' : template.id === 'call_of_cthulhu_7e' ? 'per_lutar' : 'per_luta', dano: '1d6+1', dano_extra: '1d4', municao: 3,
      ...(template.id === 'dnd_5e' ? { treinado: true, bonus: 0 } : template.id === 'call_of_cthulhu_7e' ? { empala: true, tipo: 'corpo', falha: 100 } : { margem: 20, multiplicador: 2 }) }];
    const created = await request(`/api/campaigns/${campaign.body.id}/characters`, owner, 'POST', { name: 'Personagem de validação', health: 12, maxHealth: 20, sanity: 50, maxSanity: 99, mana: 10, maxMana: 20, portraitUrl: null, backgroundUrl: null, fields });
    assert.equal(created.status, 201, JSON.stringify(created.body));
    let character = created.body;
    const route = `/api/characters/${character.id}/rules`;
    assert.equal((await request(route, owner)).body.supported, true);
    const input = { expectedVersion: character.version, command: { action: 'damage', amount: 3, option: 'normal', visibility: 'private' } };
    assert.equal((await request(route, null, 'POST', input)).status, 401);
    assert.equal((await request(route, player, 'POST', input)).status, 403);
    assert.equal((await request(route, spectator, 'POST', input)).status, 403);
    const applied = await request(route, owner, 'POST', input);
    assert.equal(applied.status, 200, JSON.stringify(applied.body)); character = applied.body.character;
    assert.equal(character.health, 9);
    assert.equal((await request(route, owner, 'POST', input)).status, 409, 'stale command must not apply twice');
    assert.equal((await request(`/api/characters/${character.id}`, owner)).body.health, 9);
    assert.ok((await request(`/api/characters/${character.id}/revisions`, owner)).body.length > 0);
    await page.goto(`${base}/characters/${character.id}`, { waitUntil: 'networkidle' });
    const controls = page.locator('app-sheet-rule-controls'); await controls.getByRole('combobox', { name: 'Ação', exact: true }).waitFor();
    await controls.getByRole('combobox', { name: 'Ação', exact: true }).selectOption('heal');
    await controls.getByRole('spinbutton').fill('3');
    const saved = page.waitForResponse(response => response.url().endsWith(route) && response.request().method() === 'POST');
    await controls.getByRole('button', { name: 'Aplicar ação', exact: true }).click(); assert.equal((await saved).status(), 200);
    await page.reload({ waitUntil: 'networkidle' });
    assert.equal((await request(`/api/characters/${character.id}`, owner)).body.health, 12);
    await controls.getByRole('combobox', { name: 'Ação', exact: true }).selectOption('attack');
    await controls.getByRole('combobox', { name: 'Rolagens', exact: true }).selectOption('private');
    const attacked = page.waitForResponse(response => response.url().endsWith(route) && response.request().method() === 'POST');
    await controls.getByRole('button', { name: 'Aplicar ação', exact: true }).click(); assert.equal((await attacked).status(), 200);
    const afterAttack = (await request(`/api/characters/${character.id}`, owner)).body;
    assert.equal(afterAttack.fields.ataques_regras[0].municao, 2, 'ammunition consumed and persisted');
    assert.ok((await request(`/api/campaigns/${campaign.body.id}/rolls`, owner)).body.length >= 1);
    assert.equal((await request(`/api/campaigns/${campaign.body.id}/rolls`, player)).body.length, 0, 'private attack rolls stay private');
    if (template.id === 'dnd_5e') {
      const configured = await request(route, owner, 'POST', { expectedVersion: afterAttack.version, command: { action: 'configure', amount: 5, option: 'nivel_clerigo', visibility: 'private' } });
      assert.equal(configured.status, 200, JSON.stringify(configured.body));
      assert.equal(configured.body.character.fields.espacos_magia_3, 2);
      const cast = await request(route, owner, 'POST', { expectedVersion: configured.body.character.version, command: { action: 'cast', amount: 3, visibility: 'private' } });
      assert.equal(cast.status, 200, JSON.stringify(cast.body)); assert.equal(cast.body.character.fields.espacos_magia_3, 1);
    }
    await page.reload({ waitUntil: 'networkidle' });
    for (const [name, width, height] of [['desktop', 1440, 900], ['wide', 1920, 1080], ['tablet', 768, 1024], ['mobile', 390, 844]]) {
      await page.setViewportSize({ width, height });
      await page.screenshot({ path: path.join(out, `${template.id}-${name}.png`), fullPage: true });
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${template.id}: overflow at ${width}`);
    }
    console.log(`${template.id}: real API, permissions, conflict, history, browser action, reload and four screenshots passed`);
  }
  assert.deepEqual(errors, []);
} finally { await browser.close(); }
