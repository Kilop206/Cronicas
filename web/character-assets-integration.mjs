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
const account = await request('/api/auth/register', null, 'POST', { displayName: 'Asset browser', email: `assets-browser-${suffix}@example.test`, password: 'assets-browser-local-123' });
const campaign = (await request('/api/campaigns', account.cookie, 'POST', { name: 'Asset browser fixture', description: '', accentColor: '#a54d52' })).body;
const character = (await request(`/api/campaigns/${campaign.id}/characters`, account.cookie, 'POST', { name: 'Asset editor', portraitUrl: null, backgroundUrl: null,
  health: 10, maxHealth: 20, sanity: 10, maxSanity: 20, mana: 10, maxMana: 20, fields: {}, version: 1 })).body;
const route = `/api/characters/${character.id}`;
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
const split = account.cookie.indexOf('=');
await context.addCookies([{ name: account.cookie.slice(0, split), value: account.cookie.slice(split + 1), url: base }]);
const page = await context.newPage(); const errors = []; page.on('pageerror', e => errors.push(e.message));
async function saved(method, collection, click) {
  const result = page.waitForResponse(r => new URL(r.url()).pathname.startsWith(`${route}/${collection}`) && r.request().method() === method);
  await click(); const response = await result; assert.ok(response.ok(), await response.text()); return response.status() === 204 ? null : response.json();
}
try {
  await page.goto(`${base}/characters/${character.id}`, { waitUntil: 'networkidle' });
  const tabs = page.locator('nav.sheet-tabs');
  await tabs.getByRole('button', { name: 'Inventário', exact: true }).click();
  const inventory = page.locator('app-character-inventory');
  await inventory.getByLabel('Nome', { exact: true }).fill('Corda de ensaio');
  await inventory.getByLabel('Quantidade', { exact: true }).fill('2');
  await inventory.getByLabel('Peso por unidade', { exact: true }).fill('1.25');
  const item = await saved('POST', 'inventory', () => inventory.getByRole('button', { name: 'Adicionar ao inventário', exact: true }).click());
  assert.equal(item.weight, 1.25); assert.equal(item.quantity, 2);
  const row = inventory.locator('.inventory-row').filter({ hasText: 'Corda de ensaio' });
  await row.getByRole('button', { name: 'Editar', exact: true }).click();
  await inventory.getByLabel('Quantidade', { exact: true }).fill('3');
  await request(`${route}/inventory/${item.id}`, account.cookie, 'PUT', { ...item, quantity: 4 });
  const conflict = page.waitForResponse(r => new URL(r.url()).pathname === `${route}/inventory/${item.id}` && r.request().method() === 'PUT');
  await inventory.getByRole('button', { name: 'Salvar item', exact: true }).click();
  assert.equal((await conflict).status(), 409);
  await inventory.getByRole('alert').filter({ hasText: 'mudou' }).waitFor();
  assert.equal(await inventory.getByLabel('Quantidade', { exact: true }).inputValue(), '3', 'Conflict preserves the local item draft');
  await page.reload({ waitUntil: 'networkidle' });
  await tabs.getByRole('button', { name: 'Inventário', exact: true }).click();
  await row.getByRole('button', { name: 'Editar', exact: true }).click();
  await page.waitForFunction(() => document.querySelector('app-character-inventory input[name="quantity"]')?.value === '4');
  assert.equal(await inventory.getByLabel('Quantidade', { exact: true }).inputValue(), '4', 'Reload shows authoritative value');
  await inventory.getByLabel('Quantidade', { exact: true }).fill('5');
  assert.equal((await saved('PUT', 'inventory', () => inventory.getByRole('button', { name: 'Salvar item', exact: true }).click())).version, 3);
  await row.getByRole('button', { name: 'Excluir', exact: true }).click();
  await saved('DELETE', 'inventory', () => row.getByRole('button', { name: 'Confirmar exclusão', exact: true }).click());
  await inventory.getByText('Nenhum item registrado.', { exact: true }).waitFor();

  await page.setViewportSize({ width: 390, height: 844 });
  await tabs.getByRole('button', { name: 'Habilidades', exact: true }).click();
  const abilities = page.locator('app-character-abilities');
  await abilities.getByLabel('Nome', { exact: true }).fill('Pulso de ensaio');
  await abilities.getByLabel('Descrição', { exact: true }).fill('Texto autoral de teste');
  await abilities.getByLabel('Expressão de dados', { exact: true }).fill('1d6+2');
  const ability = await saved('POST', 'abilities', () => abilities.getByRole('button', { name: 'Adicionar habilidade', exact: true }).click());
  assert.equal(ability.rollExpression, '1d6+2');
  const abilityRow = abilities.locator('.ability-row').filter({ hasText: 'Pulso de ensaio' });
  const rolled = page.waitForResponse(r => new URL(r.url()).pathname === `/api/campaigns/${campaign.id}/rolls` && r.request().method() === 'POST');
  await abilityRow.getByRole('button', { name: 'Rolar na campanha', exact: true }).click();
  assert.equal((await rolled).status(), 201);
  await abilityRow.getByRole('status').filter({ hasText: 'Resultado' }).waitFor();
  assert.equal((await request(`/api/campaigns/${campaign.id}/rolls`, account.cookie)).body.length, 1);
  await abilityRow.getByRole('button', { name: 'Editar', exact: true }).click();
  await abilities.getByLabel('Custo', { exact: true }).fill('2 pontos');
  assert.equal((await saved('PUT', 'abilities', () => abilities.getByRole('button', { name: 'Salvar habilidade', exact: true }).click())).cost, '2 pontos');
  await page.reload({ waitUntil: 'networkidle' });
  await tabs.getByRole('button', { name: 'Habilidades', exact: true }).click();
  await abilityRow.getByText('Custo: 2 pontos', { exact: true }).waitFor();
  await abilityRow.getByRole('button', { name: 'Excluir', exact: true }).click();
  await saved('DELETE', 'abilities', () => abilityRow.getByRole('button', { name: 'Confirmar exclusão', exact: true }).click());
  await abilities.getByText('Nenhuma habilidade registrada.', { exact: true }).waitFor();
  assert.equal((await request(route, account.cookie)).body.version, 1);
  assert.deepEqual(errors, []);
  console.log('Character assets browser: desktop inventory CRUD/conflict draft/reload, mobile ability CRUD/reload, campaign dice and unchanged sheet revision passed.');
} finally { await browser.close(); }
