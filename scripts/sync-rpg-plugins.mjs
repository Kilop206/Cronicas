import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const source = new URL('../api/Chronicles.Api/modules/Chronicles.Domain/RpgSystems/builtin-rpg-plugins.json', import.meta.url);
const destination = new URL('../web/src/app/features/rpg-systems/plugins/builtin-rpg-plugins.json', import.meta.url);
const content = readFileSync(source, 'utf8');
JSON.parse(content);
if (process.argv.includes('--check')) {
  if (readFileSync(destination, 'utf8') !== content) throw new Error('Plugin catalog drift: run node scripts/sync-rpg-plugins.mjs');
} else {
  mkdirSync(fileURLToPath(new URL('./', destination)), { recursive: true });
  writeFileSync(destination, content);
}
console.log('RPG plugin catalogs match.');
