import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';

const base = process.env.OPENAPI_LOCAL_URL || 'http://127.0.0.1:8082';
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Export from a local build');
const response = await fetch(new URL('/openapi/v1.json', base));
assert.ok(response.ok, `OpenAPI returned ${response.status}`);
const document = await response.json();
document.info = { ...document.info, title: 'Cronicas RPG APIs', version: 'v1' };
document.servers = [{ url: '/' }];
document.paths = Object.fromEntries(Object.entries(document.paths).filter(([route]) => route.startsWith('/api/v1/')));
assert.equal(Object.keys(document.paths).length, 2);
assert.ok(document.paths['/api/v1/characters/{id}/sheet']?.put, 'Missing revision-aware sheet write contract');
const requestSchemaRef = document.paths['/api/v1/characters/{id}/sheet'].put.requestBody.content['application/json'].schema.$ref;
const requestSchema = document.components.schemas[requestSchemaRef.split('/').at(-1)];
assert.ok(requestSchema.required.includes('version'), 'Write contract must declare the revision as required');
// Authentication is enforced by the existing custom session middleware/guards, not ASP.NET Authorize metadata.
for (const entry of Object.values(document.paths)) for (const operation of Object.values(entry)) operation.security = [{ sessionCookie: [] }];
const allSchemas = document.components?.schemas ?? {};
const needed = new Set();
function collect(value) {
  if (!value || typeof value !== 'object') return;
  if (typeof value.$ref === 'string' && value.$ref.startsWith('#/components/schemas/')) needed.add(value.$ref.split('/').at(-1));
  for (const child of Object.values(value)) collect(child);
}
collect(document.paths);
let previous = -1;
while (previous !== needed.size) { previous = needed.size; for (const key of needed) collect(allSchemas[key]); }
document.components = { schemas: Object.fromEntries([...needed].sort().map(key => [key, allSchemas[key]])),
  securitySchemes: { sessionCookie: { type: 'apiKey', in: 'cookie', name: 'chronicles_session' } } };
const output = new URL('../docs/contracts/', import.meta.url); mkdirSync(output, { recursive: true });
writeFileSync(new URL('rpg-api-v1.openapi.json', output), JSON.stringify(document, null, 2) + '\n');
console.log('Exported real v1 schemas with explicit session-cookie security and relative server URL.');
