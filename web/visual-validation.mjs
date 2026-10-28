import { chromium } from 'playwright';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';
import ts from 'typescript';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const source = fs.readFileSync(path.join(__dirname, 'src/app/system-templates.ts'), 'utf8');
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
const templateExports = {};
vm.runInNewContext(compiled, { exports: templateExports }, { filename: 'system-templates.ts' });
function fixture(id) {
  const template = templateExports.SHEET_TEMPLATES.find(item => item.id === id);
  const fields = Object.fromEntries(template.fields.filter(item => item.defaultValue !== undefined && item.defaultValue !== null).map(item => [item.key, item.defaultValue]));
  const order = id === 'ordem_paranormal';
  const coc = id === 'call_of_cthulhu_7e';
  const t20 = id === 'tormenta20';
  Object.assign(fields, order ? { origem: 'Acadêmico', classe: 'Especialista', trilha: 'Investigador', nex: 35, deslocamento: 9, agilidade: 3, forca: 2, intelecto: 4, presenca: 2, vigor: 2, per_investigacao: 5, per_ocultismo: 5, per_percepcao: 5, protecao: 'Colete', resistencias: 'Mental +5' } : coc ? { ocupacao: 'Jornalista', local: 'Arkham', epoca: '1925', idade: 34, credito: 45, sorte: 62, forca: 55, constituicao: 60, tamanho: 50, destreza: 65, aparencia: 45, educacao: 70, inteligencia: 75, poder: 60, forca_metade: 27, forca_quinto: 11, per_biblioteca: 70, per_escutar: 55, per_ocultismo: 35 } : t20 ? { raca: 'Humano', origem: 'Marinheiro', classe: 'Guerreiro', divindade: 'Valkaria', nivel: 4, experiencia: 3200, forca: 4, destreza: 2, constituicao: 3, inteligencia: 1, sabedoria: 0, carisma: 1, defesa: 19, deslocamento: 9, per_atletismo: 8, per_luta: 9, per_percepcao: 4 } : { classe: 'Paladino', especie: 'Humano', antecedente: 'Viajante', nivel: 5, experiencia: 6500, proficiencia: 3, forca: 16, destreza: 12, constituicao: 14, inteligencia: 10, sabedoria: 12, carisma: 16, mod_forca: 3, mod_destreza: 1, mod_constituicao: 2, mod_inteligencia: 0, mod_sabedoria: 1, mod_carisma: 3, classe_armadura: 18, iniciativa: 1, deslocamento: 9, per_atletismo: 6, per_percepcao: 4 });
  const character = { id: 'char-visual', campaignId: 'camp-visual', ownerId: 'user-visual', canEdit: true, name: order ? 'Helena Valença' : coc ? 'Clara Mendes' : t20 ? 'Ravi dos Ventos' : 'Elias de Valoria', portraitUrl: null, backgroundUrl: null, health: order ? 28 : coc ? 11 : t20 ? 36 : 37, maxHealth: order ? 35 : coc ? 12 : t20 ? 42 : 44, sanity: coc ? 58 : 32, maxSanity: coc ? 65 : 40, mana: t20 ? 10 : 7, maxMana: t20 ? 16 : 12, fields, version: 1, updatedAt: new Date().toISOString() };
  const system = { id: 'system-visual', ownerId: null, name: template.name, description: '', fields: template.fields, layout: template.layout, templateId: template.id, templateVersion: template.version, rulesetVersion: template.rulesetVersion, rendererMode: 'specialized', revision: 1, revisions: [], npcTemplates: [], createdAt: '', updatedAt: '' };
  const campaign = { id: 'camp-visual', name: order ? 'Caso do Farol' : 'Estrada dos Reinos', description: '', status: 'active', accentColor: '#a54d52', role: 'owner', updatedAt: '', systemId: system.id, worldId: null };
  return { character, system, campaign, name: order ? 'ordem' : coc ? 'coc' : t20 ? 't20' : 'dnd', selector: order ? '.paranormal-sheet' : coc ? '.coc-sheet' : t20 ? '.t20-sheet' : '.dnd-sheet' };
}
const user = { id: 'user-visual', email: 'visual@example.test', displayName: 'Validação Visual', bio: null, avatarUrl: null, accent: 'wine', createdAt: '' };
const out = path.resolve(__dirname, '../docs/visual-validation');
fs.mkdirSync(out, { recursive: true });
const sizes = [{ name: 'desktop', width: 1440, height: 900 }, { name: 'wide', width: 1920, height: 1080 }, { name: 'tablet', width: 768, height: 1024 }, { name: 'mobile', width: 390, height: 844 }];
(async () => {
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  for (const sheetId of ['ordem_paranormal', 'dnd_5e', 'call_of_cthulhu_7e', 'tormenta20'].filter(id => !process.argv[2] || process.argv[2] === id)) for (const size of sizes.filter(item => !process.argv[3] || process.argv[3] === item.name)) {
    let { character, system, campaign, name, selector } = fixture(sheetId);
    const page = await browser.newPage({ viewport: { width: size.width, height: size.height }, deviceScaleFactor: 1 });
    page.on('console', msg => { if (msg.type() === 'error') console.error(size.name, msg.text()); });
    await page.route('**/api/**', async route => {
      const { pathname } = new URL(route.request().url());
      const method = route.request().method();
      const responses = { '/api/auth/me': user, '/api/characters/char-visual': character, '/api/characters/char-visual/favorite': { favorite: false }, '/api/characters/char-visual/secret': { notes: '', revision: 1, updatedAt: null }, '/api/campaigns/camp-visual': campaign, '/api/systems/system-visual': system };
      if (pathname === '/api/characters/char-visual' && method === 'PUT') character = { ...character, ...JSON.parse(route.request().postData() || '{}'), version: character.version + 1 };
      if ((pathname === '/api/campaigns/camp-visual/rolls' || pathname === '/api/campaigns/camp-visual/percentile-rolls') && method === 'POST') {
        const request = JSON.parse(route.request().postData() || '{}');
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ id: 'roll-visual', campaignId: campaign.id, userId: user.id, userName: user.displayName, expression: request.expression || '1d100', rolls: [13], keptIndexes: [0], modifier: 0, total: 13, visibility: request.visibility, createdAt: new Date().toISOString() }) });
      }
      const body = pathname === '/api/characters/char-visual' ? character : responses[pathname] ?? (method === 'GET' ? [] : {});
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
    });
    await page.goto('http://127.0.0.1:4200/characters/char-visual', { waitUntil: 'networkidle' });
    await page.locator(`.custom-sheet-main ${selector}`).waitFor();
    await page.screenshot({ path: path.join(out, `${name}-${size.name}.png`), fullPage: true });
    const dimensions = await page.evaluate(selector => { const skillTable = document.querySelector('.custom-sheet-main .paranormal-skills'); return { scrollWidth: document.documentElement.scrollWidth, viewport: innerWidth, sheet: document.querySelector(`.custom-sheet-main ${selector}`)?.getBoundingClientRect().toJSON(), skillTableOverflow: skillTable ? skillTable.scrollWidth - skillTable.clientWidth : 0 }; }, selector);
    console.log(name, size.name, dimensions);
    if (dimensions.scrollWidth > size.width) throw new Error(`${name} ${size.name}: horizontal page overflow`);
    if (name === 'ordem' && size.name === 'mobile' && dimensions.skillTableOverflow > 1) throw new Error('Order mobile: skill controls are clipped');
    if (size.name === 'desktop') {
      const resourceLabel = name === 'ordem' ? 'Aumentar Vida' : name === 'coc' ? 'Aumentar Pontos de vida' : name === 't20' ? 'Aumentar PONTOS DE VIDA' : 'Aumentar Hit Points';
      const resource = page.locator('.custom-sheet-main').getByRole('button', { name: resourceLabel });
      const previous = character.health;
      const savedResource = page.waitForResponse(response => response.url().endsWith('/api/characters/char-visual') && response.request().method() === 'PUT');
      await resource.click();
      await savedResource;
      await page.reload({ waitUntil: 'networkidle' });
      if (character.health !== previous + 1) throw new Error(`${name}: resource autosave failed`);
      const rollButton = page.locator('.custom-sheet-main').getByRole('button', { name: name === 'ordem' ? 'Rolar Agilidade' : name === 'coc' ? 'Rolar Força (STR)' : 'Rolar Força' });
      await rollButton.first().click();
      await page.locator('.custom-sheet-main [role="status"]').filter({ hasText: ': 13' }).first().waitFor();
      await page.getByRole('button', { name: 'Editar ficha' }).click();
      const fieldKey = name === 'ordem' ? 'agilidade' : 'forca';
      const fieldInput = page.locator('.custom-sheet-main').getByRole('spinbutton', { name: name === 'ordem' ? 'Agilidade' : name === 'coc' ? 'Força (STR)' : name === 't20' ? 'Força' : 'Valor de Força' });
      const nextValue = Number(character.fields[fieldKey]) + 1;
      const savedField = page.waitForResponse(response => response.url().endsWith('/api/characters/char-visual') && response.request().method() === 'PUT');
      await fieldInput.fill(String(nextValue));
      await savedField;
      await page.reload({ waitUntil: 'networkidle' });
      if (character.fields[fieldKey] !== nextValue) throw new Error(`${name}: attribute autosave failed`);
      console.log(name, 'resource, roll and attribute persistence passed');
    }
    await page.close();
  }
  await browser.close();
})().catch(error => { console.error(error); process.exitCode = 1; });
