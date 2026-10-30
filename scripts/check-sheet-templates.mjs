import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const ts = require('../web/node_modules/typescript');
const root = path.resolve(import.meta.dirname, '../web/src/app');

function load(file) {
  const source = fs.readFileSync(path.join(root, file), 'utf8');
  const code = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
  const exports = {};
  vm.runInNewContext(code, { exports, structuredClone }, { filename: file });
  return exports;
}

const { SHEET_TEMPLATES: templates } = load('system-templates.ts');
const { calculateSheet, fieldRoll, paranormalFieldRoll, paranormalV13BaseResources, dnd2014NextLevelXp } = load('sheet-rules.ts');
const { resolveSheetRenderer, sheetResources, sheetIndicators } = load('sheet-renderer-registry.ts');
assert.equal(templates.length, 4);
assert.equal(new Set(templates.map(template => template.layout.skin)).size, templates.length, 'Each template should have its own visual style');
assert.equal(new Set(templates.map(template => template.layout.headerLabel)).size, templates.length, 'Each template should have its own heading');
assert.equal(new Set(templates.map(template => template.layout.pages.map(page => page.id).join(','))).size, templates.length, 'Each template should have its own page composition');

for (const template of templates) {
  const fields = template.fields;
  const pages = template.layout.pages;
  assert.ok(fields.length <= 100, `${template.id}: too many fields`);
  assert.ok(pages.length <= 8, `${template.id}: too many pages`);
  assert.ok(pages.every(page => page.blocks.length <= 40), `${template.id}: page exceeds block limit`);
  assert.ok(pages.reduce((total, page) => total + page.blocks.length, 0) <= 100, `${template.id}: too many blocks`);
  assert.equal(new Set(fields.map(field => field.key)).size, fields.length, `${template.id}: duplicate field key`);
  assert.equal(new Set(pages.flatMap(page => page.blocks.map(block => block.id))).size, pages.flatMap(page => page.blocks).length, `${template.id}: duplicate block id`);
  assert.ok(pages.every(page => page.blocks.length > 0), `${template.id}: empty page`);
  assert.ok(pages.flatMap(page => page.blocks).every(block => /^[a-z][a-z0-9_]{1,39}$/.test(block.id)), `${template.id}: invalid block id`);
  assert.ok(pages.flatMap(page => page.blocks).some(block => block.style === 'score'), `${template.id}: no prominent score blocks`);
  assert.ok(pages.flatMap(page => page.blocks).some(block => block.style === 'compact'), `${template.id}: no compact skill rows`);
  const placed = pages.flatMap(page => page.blocks.filter(block => block.kind === 'field').map(block => block.key));
  assert.deepEqual(new Set(placed), new Set(fields.map(field => field.key)), `${template.id}: missing or extra field block`);
  const values = {};
  assert.equal(calculateSheet(fields, values), null, `${template.id}: formula failed`);
  for (const field of fields.filter(field => field.rollExpression))
    assert.ok(fieldRoll(field.rollExpression, values), `${template.id}: roll failed for ${field.key}`);
}

const dnd = templates.find(template => template.id === 'dnd_5e');
assert.equal(dnd.version, 2);
assert.equal(dnd.rulesetVersion, '2014', 'The supplied Player Handbook covers the 2014 rules');
assert.equal(dnd2014NextLevelXp(4), 6500);
assert.equal(dnd2014NextLevelXp(19), 355000);
assert.equal(dnd2014NextLevelXp(20), null);
const dndSystem = { id: 'system-guid', name: 'Nome alterado pela mesa', templateId: dnd.id, templateVersion: dnd.version,
  rendererMode: 'specialized', fields: dnd.fields, layout: dnd.layout };
assert.equal(resolveSheetRenderer(dndSystem)?.id, 'dnd_5e', 'Renderer follows stable ID even when the system is renamed');
assert.equal(resolveSheetRenderer({ ...dndSystem, templateVersion: 99 }), null, 'Unknown template version uses custom layout');
assert.equal(resolveSheetRenderer({ ...dndSystem, templateVersion: 1 })?.id, 'dnd_5e', 'Existing 2024 sheets remain available');
assert.equal(resolveSheetRenderer({ ...dndSystem, rendererMode: 'layout' }), null, 'Owner can select custom layout');
assert.equal(sheetResources(dndSystem).map(item => item.key).join(','), 'health', 'D&D does not display sanity or mana');
assert.equal(sheetIndicators(dndSystem, { nivel: 3, classe_armadura: 15 })[1].value, '15');
const dndValues = { forca: 9, treino_per_atletismo: 1, treino_res_forca: 1 };
assert.equal(calculateSheet(dnd.fields, dndValues), null);
assert.equal(dndValues.mod_forca, -1);
assert.equal(dndValues.proficiencia, 2);
assert.equal(dndValues.per_atletismo, 1);
assert.equal(dndValues.res_forca, 1);
assert.equal(fieldRoll('1d20+{mod_forca}', dndValues), '1d20-1');

const coc = templates.find(template => template.id === 'call_of_cthulhu_7e');
const cocValues = { inteligencia: 65 };
assert.equal(calculateSheet(coc.fields, cocValues), null);
assert.equal(cocValues.inteligencia_metade, 32);
assert.equal(cocValues.inteligencia_quinto, 13);
assert.equal(coc.version, 2);
const cocVitals = { constituicao: 60, tamanho: 50, poder: 65 };
assert.equal(calculateSheet(coc.fields, cocVitals), null);
assert.equal(cocVitals.pv_inicial, 11);
assert.equal(cocVitals.pm_inicial, 13);
assert.equal(cocVitals.san_inicial, 65);
assert.equal(resolveSheetRenderer({ ...dndSystem, templateId: coc.id, templateVersion: 1, fields: coc.fields })?.id, coc.id, 'Existing CoC 7e sheets remain available');

const t20 = templates.find(template => template.id === 'tormenta20');
assert.equal(t20.version, 2);
assert.equal(t20.rulesetVersion, 'jogo-do-ano-2024-v1.3');
for (const [level, training] of [[1, 2], [6, 2], [7, 4], [14, 4], [15, 6], [20, 6]]) {
  const values = { nivel: level, forca: 4, destreza: 2, treino_per_atletismo: 1 };
  assert.equal(calculateSheet(t20.fields, values), null);
  assert.equal(values.bonus_treinamento, training);
  assert.equal(values.per_atletismo, Math.floor(level / 2) + 4 + training);
  assert.equal(values.defesa, 12);
}
assert.equal(resolveSheetRenderer({ ...dndSystem, templateId: t20.id, templateVersion: 1, fields: t20.fields })?.id, t20.id, 'Existing Tormenta20 sheets remain available');

const order = templates.find(template => template.id === 'ordem_paranormal');
assert.equal(order.version, 5, 'New Order templates are versioned without replacing existing sheets');
assert.equal(order.rulesetVersion, '1.3');
assert.equal(order.fields.find(field => field.key === 'classe_regra')?.type, 'select');
assert.equal(JSON.stringify(paranormalV13BaseResources('especialista', 35, 2, 2)), JSON.stringify({ pv: 48, pe: 35, san: 40 }));
assert.equal(JSON.stringify(paranormalV13BaseResources('combatente', 5, 2, 3)), JSON.stringify({ pv: 22, pe: 5, san: 12 }));
assert.equal(JSON.stringify(paranormalV13BaseResources('ocultista', 99, 2, 3)), JSON.stringify({ pv: 90, pe: 140, san: 115 }));
assert.equal(paranormalV13BaseResources('especialista', 37, 2, 2), null);
assert.equal(paranormalV13BaseResources('classe digitada', 35, 2, 2), null);
assert.equal(resolveSheetRenderer({ ...dndSystem, templateId: order.id, templateVersion: 1, fields: order.fields, layout: order.layout })?.id, order.id, 'Existing Order v1 still resolves');
assert.equal(resolveSheetRenderer({ ...dndSystem, templateId: order.id, templateVersion: 2, fields: order.fields, layout: order.layout })?.id, order.id, 'Existing Order v2 still resolves');
assert.equal(resolveSheetRenderer({ ...dndSystem, templateId: order.id, templateVersion: 3, fields: order.fields, layout: order.layout })?.id, order.id, 'New Order v3 resolves');
assert.equal(resolveSheetRenderer({ ...dndSystem, templateId: order.id, templateVersion: 4, fields: order.fields, layout: order.layout })?.id, order.id, 'Order v4 resolves');
assert.equal(resolveSheetRenderer({ ...dndSystem, templateId: order.id, templateVersion: 5, fields: order.fields, layout: order.layout })?.id, order.id, 'Order v5 resolves');
const orderValues = {};
assert.equal(calculateSheet(order.fields, orderValues), null);
assert.equal(orderValues.defesa, 11);
assert.equal(fieldRoll('{agilidade}d20kh1+{per_luta}', orderValues), '1d20');
assert.equal(paranormalFieldRoll('{agilidade}d20kh1+{per_luta}', { agilidade: 0, per_luta: 5 }), '2d20kl1+5', 'Order attribute zero keeps the lower die');
console.log(`Validated ${templates.length} sheet templates and representative formulas/rolls.`);
