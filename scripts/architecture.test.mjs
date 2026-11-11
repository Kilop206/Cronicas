import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { test } from 'node:test';

const require = createRequire(import.meta.url);
const ts = require('../web/node_modules/typescript');
const root = path.resolve(import.meta.dirname, '../web/src/app');
const files = [];
function walk(dir) { for (const entry of readdirSync(dir, { withFileTypes: true })) entry.isDirectory() ? walk(path.join(dir, entry.name)) : entry.name.endsWith('.ts') && files.push(path.join(dir, entry.name)); }
walk(root);
const graph = new Map();
for (const file of files) {
  const source = ts.createSourceFile(file, readFileSync(file, 'utf8'), ts.ScriptTarget.Latest, true);
  const dependencies = [];
  function add(specifier) {
    if (!specifier.startsWith('.')) return;
    const target = path.resolve(path.dirname(file), specifier) + '.ts';
    if (files.includes(target)) dependencies.push(target);
  }
  function visit(node) {
    if (ts.isImportDeclaration(node) && !node.importClause?.isTypeOnly) add(node.moduleSpecifier.text);
    if (ts.isExportDeclaration(node) && node.moduleSpecifier && !node.isTypeOnly) add(node.moduleSpecifier.text);
    if (ts.isCallExpression(node) && node.expression.kind === ts.SyntaxKind.ImportKeyword && ts.isStringLiteral(node.arguments[0])) add(node.arguments[0].text);
    ts.forEachChild(node, visit);
  }
  visit(source);
  graph.set(file, dependencies);
}

test('Frontend imports have no cycles', () => {
  const done = new Set();
  function check(file, stack = []) {
    assert.ok(!stack.includes(file), stack.concat(file).map(p => path.relative(root, p)).join(' -> '));
    if (done.has(file)) return;
    for (const next of graph.get(file) ?? []) check(next, stack.concat(file));
    done.add(file);
  }
  for (const file of files) check(file);
});

test('Core contracts/API do not depend on feature implementation', () => {
  for (const [file, targets] of graph) if (path.relative(root, file).startsWith('core' + path.sep))
    for (const target of targets) assert.ok(!path.relative(root, target).startsWith('features' + path.sep), `${file} imports ${target}`);
});

test('Character editor depends on the renderer host, not concrete RPG layouts', () => {
  const source = readFileSync(path.join(root, 'features/characters/studio-pages.ts'), 'utf8');
  assert.ok(source.includes('app-sheet-plugin-host'));
  for (const selector of ['app-paranormal-sheet', 'app-dnd-sheet', 'app-coc-sheet', 'app-t20-sheet']) assert.ok(!source.includes(selector));
});

test('Character creation/save HTTP adapters delegate without EF or business validation', () => {
  const api = path.resolve(import.meta.dirname, '../api/Chronicles.Api');
  const source = readFileSync(path.join(api, 'Features/CharacterSheets/CharacterWriteEndpoints.cs'), 'utf8');
  assert.ok(source.includes('CreateCharacter command') && source.includes('SaveCharacter command'));
  assert.ok(!/AppDbContext|EntityFrameworkCore|SaveChanges|CharacterHistory|ValidateFields|CanEdit\(/.test(source));
  const legacy = readFileSync(path.join(api, 'Features/Characters/CharacterEndpoints.cs'), 'utf8');
  const reader = readFileSync(path.join(api, 'Features/CharacterSheets/CharacterReadEndpoints.cs'), 'utf8');
  assert.ok(reader.includes('ReadCharacter query') && !/AppDbContext|EntityFrameworkCore/.test(reader));
  assert.ok(!legacy.includes('api.MapGet("/characters/{id:guid}"'));
  assert.ok(!legacy.includes('api.MapPut("/characters/{id:guid}"'));
  assert.ok(!legacy.includes('api.MapPost("/campaigns/{campaignId:guid}/characters"'));
});

test('Character list, duplication and history adapters delegate without EF or business validation', () => {
  const api = path.resolve(import.meta.dirname, '../api/Chronicles.Api');
  const source = readFileSync(path.join(api, 'Features/CharacterSheets/CharacterLifecycleEndpoints.cs'), 'utf8');
  assert.ok(source.includes('ReadCharacterLifecycle query') && source.includes('DuplicateCharacter command') && source.includes('RestoreCharacter command'));
  assert.ok(!/AppDbContext|EntityFrameworkCore|SaveChanges|CharacterHistory|ValidateFields|CanEdit\(/.test(source));
  const legacy = readFileSync(path.join(api, 'Features/Characters/CharacterEndpoints.cs'), 'utf8');
  for (const route of ['api.MapGet("/campaigns/{campaignId:guid}/characters"', 'api.MapGet("/characters/{id:guid}/revisions"',
    'api.MapPost("/characters/{id:guid}/duplicate"', 'api.MapPost("/characters/{id:guid}/revisions/{revisionId:guid}/restore"']) assert.ok(!legacy.includes(route));
});

test('Inventory and ability HTTP adapters contain no persistence or business rules', () => {
  const api = path.resolve(import.meta.dirname, '../api/Chronicles.Api');
  for (const [file, service] of [['CharacterInventoryEndpoints.cs', 'ManageCharacterInventory'], ['CharacterAbilityEndpoints.cs', 'ManageCharacterAbilities']]) {
    const source = readFileSync(path.join(api, 'Features/Characters', file), 'utf8');
    assert.ok(source.includes(`${service} command`) && source.includes(`${service} query`));
    assert.ok(!/AppDbContext|EntityFrameworkCore|SaveChanges|Validate\(|CanEdit\(|\.CountAsync|\.Version\+\+/.test(source));
  }
});

test('Favorites, private notes and OBS use cases own their logic outside HTTP', () => {
  const api = path.resolve(import.meta.dirname, '../api/Chronicles.Api');
  const source = readFileSync(path.join(api, 'Features/CharacterSheets/CharacterCompanionEndpoints.cs'), 'utf8');
  for (const service of ['ManageCharacterFavorites', 'ManageCharacterSecrets', 'ManageCharacterOverlay']) assert.ok(source.includes(service));
  assert.ok(!/AppDbContext|EntityFrameworkCore|SaveChanges|IsMaster\(|CanEdit\(|SHA256|RandomNumberGenerator|D20Conditions|TemplateVersion|\.Trim\(/.test(source));
  const composition = readFileSync(path.join(api, 'Features/Characters/CharacterEndpoints.cs'), 'utf8');
  assert.ok(composition.includes('MapCharacterCompanions(api)'));
  assert.ok(!/AppDbContext|EntityFrameworkCore|api\.Map/.test(composition));
});

test('Character version selection stays in application and the editor reads its effective system', () => {
  const api = path.resolve(import.meta.dirname, '../api/Chronicles.Api');
  const endpoint = readFileSync(path.join(api, 'Features/CharacterSheets/CharacterSystemEndpoints.cs'), 'utf8');
  assert.ok(endpoint.includes('PinCharacterSystem command') && endpoint.includes('ReadCharacterSystem query'));
  assert.ok(!/AppDbContext|EntityFrameworkCore|SaveChanges|Capture\(|IsMaster\(|Fingerprint/.test(endpoint));
  const editor = readFileSync(path.join(root, 'features/characters/studio-pages.ts'), 'utf8');
  assert.ok(editor.includes('this.api.characterSystem(id)'));
  assert.ok(!editor.includes('this.api.system(campaign.systemId)'));
});

test('Assisted migration HTTP adapters delegate and the UI sends reviewed plans, not computed state', () => {
  const api = path.resolve(import.meta.dirname, '../api/Chronicles.Api');
  const source = readFileSync(path.join(api, 'Features/CharacterSheets/CharacterSystemEndpoints.cs'), 'utf8');
  assert.ok(source.includes('MigrateCharacterSystem migration'));
  assert.ok(!/AppDbContext|EntityFrameworkCore|SaveChanges|ValidateFields|CharacterSystemMigration\.Plan/.test(source));
  const adapter = readFileSync(path.join(root, 'core/api/api.service.ts'), 'utf8');
  assert.ok(adapter.includes('{ plan, previewHash }'));
  const editor = readFileSync(path.join(root, 'features/characters/studio-pages.ts'), 'utf8');
  assert.ok(editor.includes('app-character-system-migration') && editor.includes('onSystemMigrated'));
});

test('Campaign, invitation and membership HTTP routes depend on application contracts', () => {
  const api = path.resolve(import.meta.dirname, '../api/Chronicles.Api');
  for (const [file, service] of [['CampaignEndpoints.cs', 'ManageCampaigns'], ['InviteEndpoints.cs', 'ManageCampaignInvites'], ['CampaignMembersEndpoints.cs', 'ManageCampaignMembers']]) {
    const source = readFileSync(path.join(api, 'Features/Campaigns', file), 'utf8');
    assert.ok(source.includes(service));
    assert.ok(!/AppDbContext|EntityFrameworkCore|SaveChanges|RandomNumberGenerator|CampaignPolicy|RoomAccessService/.test(source), file);
  }
  const root = readFileSync(path.join(api, 'Program.cs'), 'utf8');
  for (const legacy of ['api.MapGet("/campaigns"', 'api.MapPost("/campaigns"', 'api.MapPatch("/campaigns/{id:guid}"']) assert.ok(!root.includes(legacy));
  assert.ok(root.includes('api.MapCampaignEndpoints()'));
});

test('Campaign associations use public module contracts and thin HTTP adapters', () => {
  const api = path.resolve(import.meta.dirname, '../api/Chronicles.Api');
  const endpoint = readFileSync(path.join(api, 'Features/Campaigns/CampaignAssociationEndpoints.cs'), 'utf8');
  assert.ok(endpoint.includes('ManageCampaignAssociations command'));
  assert.ok(!/AppDbContext|EntityFrameworkCore|SaveChanges|CampaignPolicy/.test(endpoint));
  for (const [file, route] of [['RpgSystems/SystemEndpoints.cs', '/system'], ['Worlds/WorldEndpoints.cs', '/world']]) {
    const legacy = readFileSync(path.join(api, 'Features', file), 'utf8');
    assert.ok(!legacy.includes(`api.MapPatch("/campaigns/{campaignId:guid}${route}"`));
  }
  const persistence = readFileSync(path.join(api, 'modules/Chronicles.Infrastructure/Campaigns/EfCampaignAssociationStore.cs'), 'utf8');
  assert.ok(persistence.includes('IRpgSystemCampaignAccess') && persistence.includes('IWorldCampaignAccess'));
  assert.ok(!/db\.(RpgSystems|Worlds)|EfRpgSystemCampaignAccess|EfWorldCampaignAccess/.test(persistence));
});

test('Domain and application source cannot import HTTP/EF adapters', () => {
  const modules = path.resolve(import.meta.dirname, '../api/Chronicles.Api/modules');
  function inspect(dir) {
    for (const item of readdirSync(dir, { withFileTypes: true })) {
      if (['bin', 'obj'].includes(item.name)) continue;
      const file = path.join(dir, item.name);
      if (item.isDirectory()) inspect(file);
      else if (item.name.endsWith('.cs')) assert.ok(!/^\s*(?:global\s+)?using\s+(?:static\s+)?(?:Chronicles\.(?:Api|Infrastructure)|Microsoft\.(?:AspNetCore|EntityFrameworkCore))/m.test(readFileSync(file, 'utf8')), file);
    }
  }
  inspect(path.join(modules, 'Chronicles.Domain'));
  inspect(path.join(modules, 'Chronicles.Application'));
});
