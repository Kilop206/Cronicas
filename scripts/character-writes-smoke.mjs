import assert from 'node:assert/strict';

const base = process.env.SMOKE_BASE_URL || 'http://127.0.0.1:8082';
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Character write tests only use local servers');
const suffix = crypto.randomUUID();
async function request(route, cookie, method = 'GET', body) {
  const response = await fetch(new URL(route, base), { method, headers: { ...(cookie ? { Cookie: cookie } : {}),
    ...(body ? { 'Content-Type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const text = await response.text();
  return { status: response.status, body: text ? JSON.parse(text) : null, headers: response.headers,
    cookie: response.headers.get('set-cookie')?.split(';')[0] };
}
async function account(name) {
  const response = await request('/api/auth/register', null, 'POST', { displayName: name, email: `${name}-${suffix}@example.test`, password: 'character-write-local-only-123' });
  assert.equal(response.status, 201, JSON.stringify(response.body)); return response;
}
const gm = await account('write-gm'); const player = await account('write-player'); const spectator = await account('write-spectator');
const system = await request('/api/systems', gm.cookie, 'POST', { name: 'Character writes fixture', description: '', fields: [
  { key: 'base', label: 'Base', type: 'number', section: 'Stats', min: 0, max: 20, options: null },
  { key: 'double_score', label: 'Double', type: 'number', section: 'Stats', min: 0, max: 40, options: null, formula: 'base * 2' },
  { key: 'gm_only', label: 'Master field', type: 'text', section: 'Notes', min: null, max: null, options: null, editRole: 'gm' }
] });
assert.equal(system.status, 201, JSON.stringify(system.body));
const campaign = await request('/api/campaigns', gm.cookie, 'POST', { name: `Character writes ${suffix}`, description: '', accentColor: '#a54d52' });
assert.equal(campaign.status, 201);
assert.equal((await request(`/api/campaigns/${campaign.body.id}/system`, gm.cookie, 'PATCH', { systemId: system.body.id })).status, 204);
const creation = `/api/campaigns/${campaign.body.id}/characters`;
const input = { name: '  Fixture  ', portraitUrl: null, backgroundUrl: null, health: 15, maxHealth: 30, sanity: 10, maxSanity: 20, mana: 10, maxMana: 20, fields: { base: 3 }, version: 900 };
assert.equal((await request(creation, player.cookie, 'POST', input)).status, 404);
assert.equal((await request(creation, null, 'POST', input)).status, 401);
for (const [account, role] of [[player, 'player'], [spectator, 'spectator']]) {
  const invite = await request(`/api/campaigns/${campaign.body.id}/invites`, gm.cookie, 'POST', { role });
  assert.equal(invite.status, 201);
  assert.equal((await request(`/api/invites/${invite.body.token}/accept`, account.cookie, 'POST', {})).status, 200);
}
assert.equal((await request(creation, spectator.cookie, 'POST', input)).status, 403);
assert.equal((await request(creation, player.cookie, 'POST', { ...input, fields: { base: 3, gm_only: 'forged' } })).status, 400);
const created = await request(creation, player.cookie, 'POST', input);
assert.equal(created.status, 201, JSON.stringify(created.body));
assert.equal(created.body.ownerId, player.body.id); assert.equal(created.body.version, 1);
assert.equal(created.body.name, 'Fixture'); assert.equal(created.body.fields.double_score, 6); assert.equal(created.body.canEdit, true);
const route = `/api/characters/${created.body.id}`; const versioned = `/api/v1/characters/${created.body.id}/sheet`;
let character = created.body;
const protectedSave = await request(route, gm.cookie, 'PUT', { ...character, fields: { ...character.fields, gm_only: 'Preserved master value' } });
assert.equal(protectedSave.status, 200, JSON.stringify(protectedSave.body)); character = protectedSave.body;
const { version: _revision, ...withoutRevision } = character;
assert.equal((await request(versioned, player.cookie, 'PUT', withoutRevision)).status, 400);
const malformed = await fetch(new URL(versioned, base), { method: 'PUT', headers: { Cookie: player.cookie, 'Content-Type': 'application/json' }, body: '{' });
assert.equal(malformed.status, 400, 'Malformed JSON stays a client error'); assert.ok(malformed.headers.get('x-correlation-id'));
assert.ok((await malformed.json()).errors.request.length, 'Binding failures match the documented validation-error contract');
assert.equal((await request(versioned, player.cookie, 'PUT', { ...character, version: 0 })).status, 400);
assert.equal((await request(versioned, player.cookie, 'PUT', { ...character, version: 1 })).status, 409);
assert.equal((await request(versioned, spectator.cookie, 'PUT', character)).status, 403);
assert.equal((await request(versioned, null, 'PUT', character)).status, 401);
assert.equal((await request(versioned, player.cookie, 'PUT', { ...character, fields: { ...character.fields, gm_only: 'changed' } })).status, 400);
const ownSave = await request(versioned, player.cookie, 'PUT', { ...character, name: '  Saved through v1  ', fields: { ...character.fields, base: 4, double_score: 999 } });
assert.equal(ownSave.status, 200, JSON.stringify(ownSave.body));
assert.equal(ownSave.body.revision, 3); assert.equal(ownSave.body.state.name, 'Saved through v1');
assert.equal(ownSave.body.state.fields.double_score, 8); assert.equal(ownSave.body.state.fields.gm_only, 'Preserved master value');
assert.equal(ownSave.headers.get('etag'), '"3"'); assert.equal(ownSave.headers.get('cache-control'), 'private, no-store');
character = (await request(route, player.cookie)).body;
const omitted = await request(route, player.cookie, 'PUT', { ...character, health: 11, fields: null });
assert.equal(omitted.status, 200); assert.equal(omitted.body.fields.gm_only, 'Preserved master value'); character = omitted.body;
const legacyInput = { ...character }; delete legacyInput.version;
const legacy = await request(route, player.cookie, 'PUT', legacyInput);
assert.equal(legacy.status, 200, JSON.stringify(legacy.body)); character = legacy.body;
assert.equal(character.version, 5, 'Legacy optional revision remains compatible');
const responses = await Promise.all([10, 9].map(health => request(versioned, player.cookie, 'PUT', { ...character, health })));
assert.equal(responses.filter(r => r.status === 200).length, 1, JSON.stringify(responses));
assert.equal(responses.filter(r => r.status === 409).length, 1, JSON.stringify(responses));
const winner = responses.find(r => r.status === 200).body;
assert.equal(winner.revision, 6);
const reloaded = await request(versioned, player.cookie);
assert.equal(reloaded.body.state.health, winner.state.health); assert.equal(reloaded.body.revision, 6);
assert.equal((await request(`${route}/revisions`, player.cookie)).body.length, 5);
const gmCharacter = await request(creation, gm.cookie, 'POST', input); assert.equal(gmCharacter.status, 201);
assert.equal((await request(`/api/v1/characters/${gmCharacter.body.id}/sheet`, player.cookie, 'PUT', gmCharacter.body)).status, 403);
console.log('Character HTTP: authenticated creation, GM/player/spectator access, formulas, strict v1 revisions, legacy compatibility, field preservation, concurrent 409 and exact history passed.');
