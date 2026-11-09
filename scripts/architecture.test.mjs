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
