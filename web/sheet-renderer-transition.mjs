import { chromium } from 'playwright';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';
import ts from 'typescript';

const root = path.dirname(fileURLToPath(import.meta.url));
const source = fs.readFileSync(path.join(root, 'src/app/system-templates.ts'), 'utf8');
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
const templates = {};
vm.runInNewContext(compiled, { exports: templates });
const template = templates.SHEET_TEMPLATES.find(item => item.id === 'ordem_paranormal');
const user = { id: 'user-visual', email: 'visual@example.test', displayName: 'Mestre', bio: null, avatarUrl: null, accent: 'wine', createdAt: '' };
const character = { id: 'char-visual', campaignId: 'camp-visual', ownerId: user.id, canEdit: true, name: 'Agente de teste', portraitUrl: null, backgroundUrl: null, health: 20, maxHealth: 20, sanity: 20, maxSanity: 20, mana: 10, maxMana: 10, fields: { agilidade: 3, forca: 2, intelecto: 3, presenca: 2, vigor: 2, nex: 15, defesa: 15 }, version: 1, updatedAt: new Date().toISOString() };
let system = { id: 'system-visual', ownerId: user.id, name: 'Sistema da campanha', description: '', fields: template.fields.filter(field => ['agilidade', 'forca', 'intelecto', 'presenca', 'vigor', 'nex', 'defesa'].includes(field.key)), layout: template.layout, templateId: null, templateVersion: 1, rulesetVersion: null, rendererMode: 'layout', revision: 1, revisions: [], npcTemplates: [], createdAt: '', updatedAt: '' };
const campaign = { id: 'camp-visual', name: 'Campanha de teste', description: '', status: 'active', accentColor: '#a54d52', role: 'owner', updatedAt: '', systemId: system.id, worldId: null };

const browser = await chromium.launch({ channel: 'chrome', headless: true });
try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  await page.route('**/api/**', async route => {
    const { pathname } = new URL(route.request().url());
    const method = route.request().method();
    if (pathname === '/api/systems/system-visual' && method === 'PUT') {
      const input = JSON.parse(route.request().postData() || '{}');
      if (input.revision !== system.revision) throw new Error('System revision was not preserved');
      system = { ...system, ...input, revision: system.revision + 1 };
    }
    const responses = { '/api/auth/me': user, '/api/characters/char-visual': character, '/api/characters/char-visual/favorite': { favorite: false }, '/api/characters/char-visual/secret': { notes: '', revision: 1, updatedAt: null }, '/api/campaigns/camp-visual': campaign, '/api/systems/system-visual': system };
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(responses[pathname] ?? (method === 'GET' ? [] : {})) });
  });
  await page.goto('http://127.0.0.1:4200/characters/char-visual', { waitUntil: 'networkidle' });
  await page.locator('.sheet-renderer-choice').waitFor();
  if (await page.locator('.custom-sheet-main .paranormal-sheet').count()) throw new Error('Old layout unexpectedly rendered the specialized sheet');
  await page.getByRole('button', { name: 'Usar ficha visual especializada' }).click();
  await page.locator('.custom-sheet-main .paranormal-sheet').waitFor();
  if (system.templateId !== template.id || system.rendererMode !== 'specialized' || system.id !== campaign.systemId || system.fields.length !== template.fields.length) throw new Error('Campaign system was not upgraded in place');
  await page.reload({ waitUntil: 'networkidle' });
  await page.locator('.custom-sheet-main .paranormal-sheet').waitFor();
  system = { ...system, layout: null };
  await page.reload({ waitUntil: 'networkidle' });
  await page.locator('.custom-sheet-main .paranormal-sheet').waitFor();
  const output = path.resolve(root, '../docs/visual-validation/ordem-existing-system-desktop.png');
  await page.screenshot({ path: output, fullPage: true });
  console.log('Existing campaign system preserved; specialized sheet persists after reload, including without a layout. Screenshot:', output);
} finally {
  await browser.close();
}
