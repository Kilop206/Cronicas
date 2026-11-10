import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from 'playwright';

const base = process.env.ARCHITECTURE_BROWSER_URL || 'http://127.0.0.1:8082';
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Only local fixtures are allowed');
const suffix = crypto.randomUUID(); const password = 'campaign-browser-local-only-123';
async function account(name) {
  const email = `${name}-${suffix}@example.test`;
  const response = await fetch(new URL('/api/auth/register', base), { method: 'POST', signal: AbortSignal.timeout(15000), headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ displayName: name, email, password }) });
  assert.equal(response.status, 201); return { email, user: await response.json(), cookie: response.headers.get('set-cookie')?.split(';')[0] };
}
const owner = await account('campaign-browser-owner'), player = await account('campaign-browser-player');
const browser = await chromium.launch({ channel: 'chrome', headless: true }); const errors = [];
const ownerContext = await browser.newContext({ viewport: { width: 1440, height: 900 } });
const split = owner.cookie.indexOf('='); await ownerContext.addCookies([{ name: owner.cookie.slice(0, split), value: owner.cookie.slice(split + 1), url: base }]);
const ownerPage = await ownerContext.newPage(); const playerContext = await browser.newContext({ viewport: { width: 1440, height: 900 } }); const playerPage = await playerContext.newPage();
for (const page of [ownerPage, playerPage]) page.on('pageerror', e => errors.push(e.message));
try {
  await ownerPage.goto(`${base}/campaigns/new`, { waitUntil: 'networkidle' });
  const name = `Campanha integrada ${suffix.slice(0, 8)}`;
  await ownerPage.getByLabel(/^Nome da campanha/).fill(name);
  await ownerPage.getByLabel('Descrição', { exact: true }).fill('Campanha criada pelo formulário real.');
  await ownerPage.getByRole('button', { name: /^Criar campanha/ }).click();
  await ownerPage.waitForURL(/\/campaigns\/[0-9a-f-]+$/);
  const campaignPath = new URL(ownerPage.url()).pathname;
  await ownerPage.getByRole('heading', { name, exact: true }).waitFor();
  await ownerPage.locator('app-invite-panel').getByRole('button', { name: 'Gerar convite', exact: true }).click();
  const inviteLink = ownerPage.locator('app-invite-panel .invite-result code');
  await inviteLink.waitFor(); const inviteUrl = (await inviteLink.textContent()).trim(); const invitePath = new URL(inviteUrl).pathname;
  await playerPage.goto(inviteUrl, { waitUntil: 'networkidle' });
  await playerPage.waitForURL(/\/login\?returnUrl=/);
  assert.equal(new URL(playerPage.url()).searchParams.get('returnUrl'), invitePath);
  await playerPage.getByLabel('Email', { exact: true }).fill(player.email);
  await playerPage.getByLabel('Senha', { exact: true }).fill(password);
  await playerPage.getByRole('button', { name: /^Entrar/ }).click();
  await playerPage.waitForURL(`${base}${invitePath}`);
  await playerPage.getByRole('heading', { name, exact: true }).waitFor();
  await playerPage.reload({ waitUntil: 'networkidle' });
  await playerPage.getByRole('button', { name: 'Entrar na campanha', exact: true }).waitFor();
  const output = path.resolve(import.meta.dirname, '../docs/visual-validation/architecture'); fs.mkdirSync(output, { recursive: true });
  for (const [width, height] of [[1440, 900], [390, 844]]) {
    await playerPage.setViewportSize({ width, height });
    await playerPage.screenshot({ path: path.join(output, `campaign-invite-${width}x${height}.png`), fullPage: true, animations: 'disabled' });
    assert.ok(await playerPage.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `Viewport overflow at ${width}`);
  }
  const accepted = playerPage.waitForResponse(r => new URL(r.url()).pathname.endsWith('/accept') && r.request().method() === 'POST');
  await playerPage.getByRole('button', { name: 'Entrar na campanha', exact: true }).click(); assert.equal((await accepted).status(), 200);
  await playerPage.waitForURL(`${base}${campaignPath}`); await playerPage.getByRole('heading', { name, exact: true }).waitFor();
  await playerPage.reload({ waitUntil: 'networkidle' }); await playerPage.getByRole('heading', { name, exact: true }).waitFor();
  assert.equal(await playerPage.locator('app-invite-panel').count(), 0, 'player cannot issue invites');
  await ownerPage.reload({ waitUntil: 'networkidle' });
  const roleChanged = ownerPage.waitForResponse(r => new URL(r.url()).pathname.endsWith(`/members/${player.user.id}`) && r.request().method() === 'PATCH');
  await ownerPage.getByLabel(`Papel de ${player.user.displayName}`, { exact: true }).selectOption('spectator');
  assert.equal((await roleChanged).status(), 204);
  await playerPage.reload({ waitUntil: 'networkidle' });
  await playerPage.locator('.campaign-title .role-badge').filter({ hasText: 'spectator' }).waitFor();
  playerPage.once('dialog', dialog => void dialog.accept());
  await playerPage.locator('app-member-panel').getByRole('button', { name: 'Sair', exact: true }).click();
  await playerPage.waitForURL(`${base}/campaigns`);
  await playerPage.getByRole('heading', { name: 'Campanhas', exact: true }).waitFor();
  await playerPage.goto(inviteUrl, { waitUntil: 'networkidle' });
  await playerPage.getByRole('alert').waitFor();
  assert.equal(await playerPage.getByRole('button', { name: 'Entrar na campanha', exact: true }).count(), 0, 'used invite cannot be replayed');
  assert.deepEqual(errors, []);
  console.log('Campaign Chrome: create form, real invite link, anonymous login returnUrl, direct refresh, acceptance, campaign reload, owner role control, spectator view, self-leave and replay rejection passed.');
} finally { await browser.close(); }
