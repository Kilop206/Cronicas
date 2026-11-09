import assert from 'node:assert/strict';

const base = process.env.SMOKE_BASE_URL || 'http://127.0.0.1:8082';
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Only local fixtures are allowed');
const suffix = crypto.randomUUID();
async function request(route, cookie, method = 'GET', body) {
  const r = await fetch(new URL(route, base), { method, headers: { ...(cookie ? { Cookie: cookie } : {}),
    ...(body ? { 'Content-Type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const text = await r.text(); return { status: r.status, body: text ? JSON.parse(text) : null, headers: r.headers,
    cookie: r.headers.get('set-cookie')?.split(';')[0] };
}
function expect(r, status) { assert.equal(r.status, status, JSON.stringify(r.body)); return r.body; }
async function account(name) {
  const r = await request('/api/auth/register', null, 'POST', { displayName: name, email: `${name}-${suffix}@example.test`, password: 'system-versions-local-only-123' });
  expect(r, 201); return r;
}
const master = await account('version-master'), player = await account('version-player');
const fields = [
  { key: 'score', label: 'Original score', type: 'number', section: 'Stats', min: 0, max: 20, options: null },
  { key: 'double_score', label: 'Double', type: 'number', section: 'Stats', min: 0, max: 60, options: null, formula: 'score * 2' }
];
let system = expect(await request('/api/systems', master.cookie, 'POST', { name: 'Original template', description: '', fields }), 201);
const campaign = expect(await request('/api/campaigns', master.cookie, 'POST', { name: `Version fixture ${suffix}`, description: '', accentColor: '#a54d52' }), 201);
expect(await request(`/api/campaigns/${campaign.id}/system`, master.cookie, 'PATCH', { systemId: system.id }), 204);
const invite = expect(await request(`/api/campaigns/${campaign.id}/invites`, master.cookie, 'POST', { role: 'player' }), 201);
expect(await request(`/api/invites/${invite.token}/accept`, player.cookie, 'POST', {}), 200);
const input = { name: 'Pinned character', portraitUrl: null, backgroundUrl: null, health: 10, maxHealth: 20,
  sanity: 10, maxSanity: 30, mana: 5, maxMana: 10, fields: { score: 2 }, version: 1 };
let character = expect(await request(`/api/campaigns/${campaign.id}/characters`, player.cookie, 'POST', input), 201);
const legacy = expect(await request(`/api/campaigns/${campaign.id}/characters`, player.cookie, 'POST', { ...input, name: 'Legacy character' }), 201);
const route = `/api/characters/${character.id}`, pin = `/api/v1/characters/${character.id}/system-version`;
expect(await request(`${route}/system`, null), 401);
expect(await request(`/api/characters/${crypto.randomUUID()}/system`, player.cookie), 404);
assert.equal(expect(await request(`${route}/system`, player.cookie), 200).name, 'Original template');
const command = { expectedVersion: 1, registrySystemId: system.id, templateRevision: 1 };
expect(await request(pin, player.cookie, 'POST', command), 403);
expect(await request(pin, master.cookie, 'POST', { expectedVersion: 1 }), 400);
expect(await request(pin, master.cookie, 'POST', { ...command, templateRevision: 2 }), 409);
character = expect(await request(pin, master.cookie, 'POST', command), 200);
assert.equal(character.version, 2); assert.ok(character.systemVersionId);
const binding = character.systemVersionId;
assert.equal(expect(await request(pin, master.cookie, 'POST', { ...command, expectedVersion: 2 }), 200).version, 2);
assert.equal(expect(await request(`/api/characters/${legacy.id}`, player.cookie), 200).systemVersionId, null);
system = expect(await request(`/api/systems/${system.id}`, master.cookie, 'PUT', { ...system, name: 'Changed template',
  fields: [{ ...fields[0], key: 'renamed_score', label: 'Changed score' }, { ...fields[1], formula: 'renamed_score * 3' }], fieldRenames: { score: 'renamed_score' } }), 200);
assert.equal(system.revision, 2);
const pinnedModel = expect(await request(`${route}/system`, player.cookie), 200);
assert.equal(pinnedModel.revision, 1); assert.equal(pinnedModel.name, 'Original template'); assert.equal(pinnedModel.fields[0].key, 'score');
const migrated = expect(await request(`/api/characters/${legacy.id}`, player.cookie), 200);
assert.equal(migrated.fields.renamed_score, 2); assert.equal(migrated.fields.double_score, 6);
character = expect(await request(route, player.cookie), 200);
assert.equal(character.version, 2); assert.equal(character.fields.score, 2); assert.equal(character.fields.double_score, 4);
character = expect(await request(route, player.cookie, 'PUT', { ...character, fields: { score: 4, double_score: 999 } }), 200);
assert.equal(character.fields.double_score, 8); assert.equal(character.systemVersionId, binding);
expect(await request(`/api/campaigns/${campaign.id}/system`, master.cookie, 'PATCH', { systemId: null }), 204);
assert.equal(expect(await request(`${route}/system`, player.cookie), 200).name, 'Original template');
assert.equal(expect(await request(`/api/characters/${legacy.id}/system`, player.cookie), 200), null);
const sheet = expect(await request(`/api/v1/characters/${character.id}/sheet`, player.cookie), 200);
assert.equal(sheet.systemVersionId, binding); assert.equal(sheet.templateRevision, 1); assert.equal(sheet.registrySystemId, system.id);
expect(await request(`/api/systems/${system.id}`, player.cookie), 404, 'Character-scoped snapshot grants no general registry access');
expect(await request(`/api/systems/${system.id}`, master.cookie, 'DELETE'), 409);
const copy = expect(await request(`${route}/duplicate`, player.cookie, 'POST', {}), 201);
assert.equal(copy.systemVersionId, binding); assert.equal(copy.version, 1);
const history = expect(await request(`${route}/revisions`, player.cookie), 200);
character = expect(await request(`${route}/revisions/${history[0].id}/restore`, player.cookie, 'POST', { expectedVersion: character.version }), 200);
assert.equal(character.systemVersionId, binding);
const backup = expect(await request('/api/profile/export', player.cookie), 200);
assert.ok(backup.systemVersions.some(v => v.id === binding && v.name === 'Original template'));
assert.equal(backup.characters.find(c => c.id === character.id).systemVersionId, binding);
console.log('Character systems HTTP: explicit master-only binding, source revisions, untouched legacy sheets, protected field migration, pinned formulas/system changes, copies/history/export and scoped access passed.');
