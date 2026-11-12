import assert from 'node:assert/strict';
import { mkdir } from 'node:fs/promises';
import { chromium } from 'playwright';

const base = process.env.QUALITY_BASE_URL || 'http://127.0.0.1:8082';
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Local fixtures only');
const output = new URL('../docs/visual-validation/quality/', import.meta.url);
await mkdir(output, { recursive: true });
const suffix = crypto.randomUUID(); const password = 'quality-local-password-123';
async function request(path, cookie, body, method = body ? 'POST' : 'GET') {
  return fetch(new URL(path, base), { method, signal: AbortSignal.timeout(20000), headers: { ...(cookie ? { cookie } : {}), ...(body ? { 'Content-Type': 'application/json' } : {}) }, ...(body ? { body: JSON.stringify(body) } : {}) });
}
async function account(name) {
  const response = await request('/api/auth/register', null, { email: `${name}-${suffix}@example.test`, displayName: name, password });
  assert.equal(response.status, 201); return { cookie: response.headers.get('set-cookie').split(';')[0], user: await response.json() };
}
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const errors = []; const violations = [];
async function context(account) {
  const result = await browser.newContext({ viewport: { width: 1440, height: 900 }, timezoneId: 'America/Sao_Paulo', reducedMotion: 'reduce' });
  if (account) {
    const split = account.cookie.indexOf('=');
    await result.addCookies([{ name: account.cookie.slice(0, split), value: account.cookie.slice(split + 1), url: base }]);
  }
  await result.addInitScript(() => document.addEventListener('securitypolicyviolation', event => {
    // Browser test reports enforced policy failures through a binding.
    window.reportCspViolation?.(`${event.violatedDirective}: ${event.blockedURI}`);
  }));
  await result.exposeBinding('reportCspViolation', (_source, message) => violations.push(message));
  result.on('page', page => page.on('pageerror', error => errors.push(error.message)));
  return result;
}
async function open(page, path, heading) {
  const response = await page.goto(base + path, { waitUntil: 'domcontentloaded' });
  await page.locator('app-root router-outlet').first().waitFor({ state: 'attached' });
  if (heading) await page.getByRole('heading', { name: heading, exact: true }).waitFor();
  return response;
}
async function fit(page, label) {
  const overflow = await page.evaluate(() => [...document.querySelectorAll('main *')].filter(element => element.getBoundingClientRect().right > innerWidth + 1).slice(0, 8).map(element => ({ tag: element.tagName, class: element.className, width: Math.round(element.getBoundingClientRect().width), parent: element.parentElement.className, parentWidth: element.parentElement.getBoundingClientRect().width, wrap: getComputedStyle(element).overflowWrap, whiteSpace: getComputedStyle(element).whiteSpace, display: getComputedStyle(element).display, maxWidth: getComputedStyle(element).maxWidth })));
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `Horizontal overflow: ${label}: ${JSON.stringify(overflow)}`);
}
async function screenshot(page, name) { await page.screenshot({ path: new URL(name, output).pathname.replace(/^\/([A-Za-z]:)/, '$1'), fullPage: true, animations: 'disabled' }); }
async function contrast(page, selector, backgroundSelector, property = 'color') {
  return page.evaluate(({ selector, backgroundSelector, property }) => {
    const luminance = color => {
      const [r, g, b] = color.match(/[\d.]+/g).slice(0, 3).map(Number).map(value => { const s = value / 255; return s <= .04045 ? s / 12.92 : ((s + .055) / 1.055) ** 2.4; });
      return .2126 * r + .7152 * g + .0722 * b;
    };
    const values = [luminance(getComputedStyle(document.querySelector(selector))[property]), luminance(getComputedStyle(document.querySelector(backgroundSelector)).backgroundColor)].sort((a, b) => b - a);
    return (values[0] + .05) / (values[1] + .05);
  }, { selector, backgroundSelector, property });
}

try {
  const guest = await context(); const page = await guest.newPage();
  for (const [width, height] of [[320, 740], [390, 844], [768, 1024], [1440, 900], [2560, 1080]]) {
    await page.setViewportSize({ width, height });
    for (const [path, heading] of [['/', null], ['/privacy', 'Política de Privacidade'], ['/register', 'Crie sua conta']]) {
      await open(page, path, heading); await fit(page, `${path} ${width}`);
      if (width === 390 || width === 1440) await screenshot(page, `${path === '/' ? 'home' : path.slice(1)}-${width}.png`);
    }
  }
  await open(page, '/privacy', 'Política de Privacidade');
  const skip = page.getByRole('link', { name: 'Pular para o conteúdo' });
  await skip.focus(); await page.keyboard.press('Enter');
  assert.equal(await page.evaluate(() => document.activeElement?.tagName), 'MAIN');
  assert.equal(await page.evaluate(() => getComputedStyle(document.documentElement).scrollBehavior), 'auto');
  const missing = await open(page, '/a-url-que-nao-existe', 'Este caminho ainda não foi escrito.'); assert.equal(missing.status(), 404);
  assert.match(await page.title(), /Página não encontrada/);
  const failed = await open(page, '/error', 'Esta página precisa de uma pausa.'); assert.equal(failed.status(), 500);
  await open(page, '/dashboard', 'Entre no seu arquivo'); assert.match(page.url(), /\/login\?returnUrl=/);
  // Wrong email and passwords are rejected before sending and native autocomplete is preserved.
  await page.getByLabel('Email', { exact: true }).fill('invalido');
  await page.getByLabel('Senha', { exact: true }).fill('password');
  assert.ok(await page.getByRole('button', { name: /^Entrar/ }).isDisabled());

  const failingGuest = await context(); const failurePage = await failingGuest.newPage();
  await failurePage.route('**/api/auth/me', route => route.fulfill({ status: 502, contentType: 'application/json', body: '{"detail":"internal stack trace"}' }));
  await failurePage.goto(base + '/dashboard');
  await failurePage.locator('.navigation-error').waitFor();
  assert.doesNotMatch(await failurePage.locator('body').innerText(), /internal stack trace/);
  assert.notEqual(new URL(failurePage.url()).pathname, '/login', 'Transient outage must not turn into a cached logged-out state');
  await failurePage.unroute('**/api/auth/me');
  await failurePage.getByRole('button', { name: 'Tentar novamente', exact: true }).click();
  await failurePage.waitForURL(/\/login\?returnUrl=/);
  await failingGuest.close();

  const owner = await account('Quality Owner'), outsider = await account('Quality Outsider');
  const ownerContext = await context(owner); const ownerPage = await ownerContext.newPage();
  await open(ownerPage, '/dashboard'); await ownerPage.getByRole('heading', { name: /Boa jornada/ }).waitFor();
  assert.equal(await ownerPage.locator('link[rel="canonical"]').count(), 0);
  assert.match(await ownerPage.locator('meta[name="robots"]').getAttribute('content'), /noindex/);
  assert.equal(await ownerPage.locator('.side-nav [aria-current="page"]').count(), 1);
  await open(ownerPage, '/profile', 'Meu perfil');
  assert.ok(await contrast(ownerPage, '.workspace-label', '.sidebar') >= 4.5, 'Secondary navigation label meets AA text contrast');
  assert.ok(await contrast(ownerPage, 'input[name="name"]', 'input[name="name"]', 'borderColor') >= 3, 'Input boundary remains visible');
  let submissions = 0;
  await ownerPage.route('**/api/profile', async route => {
    if (route.request().method() !== 'PATCH') return route.continue();
    submissions++; await new Promise(resolve => setTimeout(resolve, 500));
    return route.fulfill({ status: 500, contentType: 'application/json', body: '{"detail":"database-password internal stack trace"}' });
  });
  await ownerPage.getByLabel('Nome de exibição', { exact: true }).fill('Rascunho preservado');
  await ownerPage.locator('main form').evaluate(form => { form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })); form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })); });
  await ownerPage.getByRole('alert').filter({ hasText: 'temporariamente indisponível' }).waitFor();
  assert.equal(submissions, 1); assert.equal(await ownerPage.getByLabel('Nome de exibição', { exact: true }).inputValue(), 'Rascunho preservado');
  assert.doesNotMatch(await ownerPage.locator('body').innerText(), /database-password|stack trace/);
  await ownerPage.unroute('**/api/profile');
  const profileSaved = ownerPage.waitForResponse(r => r.request().method() === 'PATCH' && new URL(r.url()).pathname === '/api/profile');
  await ownerPage.getByRole('button', { name: 'Salvar perfil' }).click(); assert.equal((await profileSaved).status(), 200);
  await ownerPage.getByRole('status').filter({ hasText: 'Perfil atualizado' }).waitFor();
  await ownerPage.getByLabel('Biografia', { exact: true }).fill('Alteração ainda não salva');
  ownerPage.once('dialog', dialog => dialog.dismiss());
  await ownerPage.locator('.side-nav').getByRole('link', { name: 'Campanhas', exact: true }).click();
  assert.ok(ownerPage.url().endsWith('/profile'));
  ownerPage.once('dialog', dialog => dialog.accept());
  await ownerPage.locator('.side-nav').getByRole('link', { name: 'Campanhas', exact: true }).click();
  await ownerPage.waitForURL(base + '/campaigns');

  const campaigns = [];
  for (let index = 0; index < 21; index++) {
    const response = await request('/api/campaigns', owner.cookie, { name: `Auditoria ${String(index).padStart(2, '0')} ${suffix.slice(0, 8)}`, description: 'Qualidade e acabamento', accentColor: '#b8a079' });
    assert.equal(response.status, 201); campaigns.push(await response.json());
  }
  await open(ownerPage, '/campaigns?page=2&search=Auditoria', 'Campanhas');
  await ownerPage.getByText('Página 2', { exact: true }).waitFor();
  assert.equal(await ownerPage.locator('.campaign-row').count(), 1);
  await ownerPage.locator('.campaign-row').first().click(); await ownerPage.waitForURL(/\/campaigns\/[0-9a-f-]+$/);
  await ownerPage.goBack(); await ownerPage.getByText('Página 2', { exact: true }).waitFor();
  assert.equal(await ownerPage.getByRole('textbox', { name: 'Buscar campanhas' }).inputValue(), 'Auditoria');
  await open(ownerPage, `/campaigns/${campaigns[0].id}`);
  await ownerPage.getByRole('heading', { name: campaigns[0].name, exact: true }).waitFor();
  await ownerPage.getByRole('button', { name: 'Gerar convite', exact: true }).click();
  await ownerPage.locator('.invite-result code').waitFor();
  await ownerPage.evaluate(() => Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: async () => {} } }));
  await ownerPage.getByRole('button', { name: 'Copiar link', exact: true }).click();
  await ownerPage.getByText('Link copiado.', { exact: true }).waitFor();
  for (const width of [320, 390, 768, 1440, 2560]) { await ownerPage.setViewportSize({ width, height: 900 }); await fit(ownerPage, `campaign ${width}`); }
  await ownerPage.setViewportSize({ width: 390, height: 844 }); await screenshot(ownerPage, 'campaign-390.png');
  const campaignMenu = ownerPage.getByRole('button', { name: /Campanha ▾/ });
  await campaignMenu.click();
  await ownerPage.getByRole('link', { name: 'Transmissão', exact: true }).focus(); await ownerPage.keyboard.press('Escape');
  await ownerPage.waitForFunction(() => document.querySelector('.mobile-workspace-trigger')?.getAttribute('aria-expanded') === 'false');
  assert.equal(await campaignMenu.getAttribute('aria-expanded'), 'false'); assert.ok(await campaignMenu.evaluate(element => document.activeElement === element));
  const mobileMenu = ownerPage.locator('.mobile-site-menu summary'); await mobileMenu.click(); await ownerPage.keyboard.press('Escape');
  assert.equal(await ownerPage.locator('.mobile-site-menu').evaluate(element => element.open), false);

  const createdEntry = await request(`/api/campaigns/${campaigns[0].id}/entries/sessions`, owner.cookie, { title: 'Sessão com fuso horário', body: 'A data não deve mudar ao editar.', category: 'general', visibility: 'private', eventAt: '2026-11-12T21:30:00Z', revision: 1 });
  assert.equal(createdEntry.status, 201); const entry = await createdEntry.json();
  await open(ownerPage, `/campaigns/${campaigns[0].id}/sessions?entry=${entry.id}`);
  await ownerPage.getByRole('button', { name: 'Editar', exact: true }).click();
  assert.equal(await ownerPage.getByLabel('Data do acontecimento').inputValue(), '2026-11-12T18:30');
  const entrySaved = ownerPage.waitForResponse(r => r.request().method() === 'PUT' && new URL(r.url()).pathname === `/api/entries/${entry.id}`);
  await ownerPage.getByRole('button', { name: 'Salvar registro', exact: true }).click(); assert.equal((await entrySaved).status(), 200);
  const persistedEntry = await (await request(`/api/entries/${entry.id}`, owner.cookie)).json();
  assert.equal(new Date(persistedEntry.eventAt).toISOString(), '2026-11-12T21:30:00.000Z');

  await ownerPage.setViewportSize({ width: 1440, height: 900 });
  await open(ownerPage, '/systems/new', 'Criar sistema');
  await ownerPage.getByLabel('Nome', { exact: true }).fill('Rascunho de sistema');
  ownerPage.once('dialog', dialog => dialog.dismiss());
  await ownerPage.locator('.side-nav').getByRole('link', { name: 'Campanhas', exact: true }).click();
  assert.ok(ownerPage.url().endsWith('/systems/new'));
  assert.equal(await ownerPage.getByLabel('Nome', { exact: true }).inputValue(), 'Rascunho de sistema');
  ownerPage.once('dialog', dialog => dialog.accept());
  await ownerPage.locator('.side-nav').getByRole('link', { name: 'Campanhas', exact: true }).click(); await ownerPage.waitForURL(base + '/campaigns');
  const worldResponse = await request('/api/worlds', owner.cookie, { name: 'Universo de qualidade', description: '' });
  assert.equal(worldResponse.status, 201); const world = await worldResponse.json();
  await open(ownerPage, `/worlds/${world.id}`, world.name);
  await ownerPage.getByRole('button', { name: /Novo artigo/ }).click();
  await ownerPage.getByLabel('Título', { exact: true }).fill('Rascunho de artigo');
  ownerPage.once('dialog', dialog => dialog.dismiss());
  await ownerPage.locator('.side-nav').getByRole('link', { name: 'Campanhas', exact: true }).click();
  assert.ok(ownerPage.url().endsWith(`/worlds/${world.id}`));
  assert.equal(await ownerPage.getByLabel('Título', { exact: true }).inputValue(), 'Rascunho de artigo');
  ownerPage.once('dialog', dialog => dialog.accept());
  await ownerPage.getByRole('button', { name: 'Cancelar', exact: true }).click();

  const unauthorized = await request(`/api/campaigns/${campaigns[0].id}`, outsider.cookie); assert.equal(unauthorized.status, 404);
  const otherContext = await context(outsider); const otherPage = await otherContext.newPage();
  await open(otherPage, `/campaigns/${campaigns[0].id}`);
  await otherPage.getByRole('alert').filter({ hasText: 'não foi encontrado' }).waitFor();
  assert.doesNotMatch(await otherPage.locator('body').innerText(), new RegExp(campaigns[0].name));

  // Same page keeps draft data when its session expires mid-write.
  await open(ownerPage, '/profile', 'Meu perfil');
  await ownerPage.getByLabel('Biografia', { exact: true }).fill('Rascunho após expiração');
  await ownerPage.route('**/api/profile', route => route.fulfill({ status: 401, contentType: 'application/json', body: '{}' }));
  await ownerPage.getByRole('button', { name: 'Salvar perfil' }).click();
  await ownerPage.locator('.connection-notice').filter({ hasText: 'Sua sessão expirou' }).waitFor();
  assert.equal(await ownerPage.getByLabel('Biografia', { exact: true }).inputValue(), 'Rascunho após expiração');
  await ownerContext.setOffline(true); await ownerPage.getByRole('status').filter({ hasText: 'sem conexão' }).waitFor(); await ownerContext.setOffline(false);
  await ownerPage.unroute('**/api/profile');
  // A malformed successful body is a controlled error, with no exception shown to the user.
  await ownerPage.route('**/api/profile', route => route.fulfill({ status: 200, contentType: 'application/json', body: 'invalid json' }));
  await ownerPage.getByRole('button', { name: 'Salvar perfil' }).click();
  await ownerPage.getByRole('alert').filter({ hasText: 'Não foi possível concluir' }).waitFor();
  assert.equal(await ownerPage.getByLabel('Biografia', { exact: true }).inputValue(), 'Rascunho após expiração');
  const timeoutContext = await context(owner); const timeoutPage = await timeoutContext.newPage();
  await open(timeoutPage, '/profile', 'Meu perfil');
  await timeoutPage.clock.install();
  await timeoutPage.route('**/api/profile', () => undefined);
  await timeoutPage.getByLabel('Biografia', { exact: true }).fill('Rascunho após timeout');
  const stalled = timeoutPage.waitForRequest(r => r.method() === 'PATCH' && new URL(r.url()).pathname === '/api/profile');
  await timeoutPage.getByRole('button', { name: 'Salvar perfil' }).click(); await stalled;
  await timeoutPage.clock.fastForward(90001);
  await timeoutPage.getByRole('alert').filter({ hasText: 'O servidor demorou para responder' }).waitFor();
  assert.equal(await timeoutPage.getByLabel('Biografia', { exact: true }).inputValue(), 'Rascunho após timeout');
  await timeoutContext.close();
  assert.deepEqual(errors, [], 'No uncaught JavaScript errors');
  assert.deepEqual(violations, [], 'No CSP violations on the exercised pages');
  console.log('Quality browser: responsive pages, metadata, 404/500, keyboard, reduced motion, auth/outage, duplicate submit, safe errors, drafts, pagination/back, clipboard, IDOR, expired session, offline and malformed JSON passed.');
} finally { await browser.close(); }
