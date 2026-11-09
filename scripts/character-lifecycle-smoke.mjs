import assert from 'node:assert/strict';

const base = process.env.SMOKE_BASE_URL || 'http://127.0.0.1:8082';
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Lifecycle tests only use local servers');
const suffix = crypto.randomUUID();
async function request(route, cookie, method = 'GET', body) {
  const response = await fetch(new URL(route, base), { method, headers: { ...(cookie ? { Cookie: cookie } : {}),
    ...(body ? { 'Content-Type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const text = await response.text();
  return { status: response.status, body: text ? JSON.parse(text) : null, headers: response.headers,
    cookie: response.headers.get('set-cookie')?.split(';')[0] };
}
async function account(name) {
  const result = await request('/api/auth/register', null, 'POST', { displayName: name, email: `${name}-${suffix}@example.test`, password: 'lifecycle-local-only-123' });
  assert.equal(result.status, 201, JSON.stringify(result.body)); return result;
}
function expect(result, status) { assert.equal(result.status, status, JSON.stringify(result.body)); return result.body; }
const gm = await account('lifecycle-gm'), player = await account('lifecycle-player'), spectator = await account('lifecycle-spectator');
const system = expect(await request('/api/systems', gm.cookie, 'POST', { name: 'Lifecycle fixture', description: '', fields: [
  { key: 'score', label: 'Score', type: 'number', section: 'Stats', min: 0, max: 20 },
  { key: 'double_score', label: 'Double', type: 'number', section: 'Stats', min: 0, max: 40, formula: 'score * 2' },
  { key: 'gm_only', label: 'Master field', type: 'text', section: 'Notes', editRole: 'gm' }
] }), 201);
const campaign = expect(await request('/api/campaigns', gm.cookie, 'POST', { name: `Lifecycle ${suffix}`, description: '', accentColor: '#a54d52' }), 201);
expect(await request(`/api/campaigns/${campaign.id}/system`, gm.cookie, 'PATCH', { systemId: system.id }), 204);
const listing = `/api/campaigns/${campaign.id}/characters`;
expect(await request(listing, player.cookie), 404);
expect(await request(listing, null), 401);
for (const [account, role] of [[player, 'player'], [spectator, 'spectator']]) {
  const invite = expect(await request(`/api/campaigns/${campaign.id}/invites`, gm.cookie, 'POST', { role }), 201);
  expect(await request(`/api/invites/${invite.token}/accept`, account.cookie, 'POST', {}), 200);
}
const input = { name: 'Source', portraitUrl: null, backgroundUrl: null, health: 15, maxHealth: 30,
  sanity: 10, maxSanity: 20, mana: 10, maxMana: 20, fields: { score: 3 }, version: 1 };
let character = expect(await request(listing, player.cookie, 'POST', input), 201);
const route = `/api/characters/${character.id}`;
const other = expect(await request(listing, gm.cookie, 'POST', { ...input, name: 'Other' }), 201);
for (const [account, editable] of [[player, [false, true]], [gm, [true, true]], [spectator, [false, false]]]) {
  const items = expect(await request(listing, account.cookie), 200);
  assert.deepEqual(items.map(i => i.name), ['Other', 'Source']);
  assert.deepEqual(items.map(i => i.canEdit), editable);
}
const itemInput = { name: 'Rope', category: 'item', quantity: 2, weight: 1.5, notes: 'Keep these notes', version: 1 };
const item = expect(await request(`${route}/inventory`, player.cookie, 'POST', itemInput), 201);
const abilityInput = { name: 'Custom action', description: 'Description', cost: '1', effect: 'Effect', duration: 'Turn',
  limitations: 'Limit', recharge: 'Rest', rollExpression: '1d6+2', version: 1 };
const ability = expect(await request(`${route}/abilities`, player.cookie, 'POST', abilityInput), 201);
expect(await request(`${route}/secret`, gm.cookie, 'PUT', { notes: 'Master secret must stay here', revision: 1 }), 200);
expect(await request(`${route}/favorite`, player.cookie, 'PUT'), 204);
const overlay = expect(await request(`${route}/overlay-key`, player.cookie, 'POST', {}), 200);
character = expect(await request(route, gm.cookie, 'PUT', { ...character, fields: { score: 4, gm_only: 'A' } }), 200);
character = expect(await request(route, gm.cookie, 'PUT', { ...character, health: 8, fields: { score: 5, gm_only: 'B' } }), 200);
assert.equal(character.version, 3);
expect(await request(`/api/characters/${other.id}`, gm.cookie, 'PUT', { ...other, health: 9 }), 200);
const foreignRevision = expect(await request(`/api/characters/${other.id}/revisions`, gm.cookie), 200)[0];
const history = expect(await request(`${route}/revisions`, player.cookie), 200);
assert.deepEqual(history.map(r => r.version), [2, 1]);
assert.ok(history.every(r => Object.keys(r).sort().join(',') === 'createdAt,editedBy,id,version'), 'History returns metadata only');
const restore = revision => `${route}/revisions/${revision.id}/restore`;
expect(await request(restore(history[0]), player.cookie, 'POST', { expectedVersion: 3 }), 400);
expect(await request(restore(history[0]), gm.cookie, 'POST', { expectedVersion: 2 }), 409);
expect(await request(restore(foreignRevision), gm.cookie, 'POST', { expectedVersion: 3 }), 404);
expect(await request(`${route}/revisions`, spectator.cookie), 404);
expect(await request(`/api/characters/${other.id}/revisions`, player.cookie), 404);
expect(await request(restore(history[1]), spectator.cookie, 'POST', { expectedVersion: 3 }), 404);
expect(await request(`${route}/duplicate`, spectator.cookie, 'POST', {}), 404);
expect(await request(`/api/characters/${other.id}/duplicate`, player.cookie, 'POST', {}), 403);
expect(await request(`${route}/duplicate`, null, 'POST', {}), 401);
character = expect(await request(restore(history[0]), gm.cookie, 'POST', { expectedVersion: 3 }), 200);
assert.equal(character.version, 4); assert.equal(character.health, 15);
assert.deepEqual(character.fields, { score: 4, double_score: 8, gm_only: 'A' });
expect(await request(restore(history[1]), player.cookie, 'POST', { expectedVersion: 4 }), 400);
character = expect(await request(restore(history[0]), player.cookie, 'POST', { expectedVersion: 4 }), 200);
assert.equal(character.version, 5); assert.equal(character.fields.double_score, 8);
assert.equal(character.fields.gm_only, 'A', 'Player restore must also preserve the presence of master fields');
assert.deepEqual(expect(await request(`${route}/revisions`, player.cookie), 200).map(r => r.version), [4, 3, 2, 1]);
for (const account of [player, gm]) {
  const copy = expect(await request(`${route}/duplicate`, account.cookie, 'POST', {}), 201);
  assert.notEqual(copy.id, character.id); assert.equal(copy.ownerId, account.body.id);
  assert.equal(copy.version, 1); assert.equal(copy.name, 'Source (cópia)');
  assert.deepEqual(copy.fields, character.fields); assert.equal(copy.health, character.health);
  const copyRoute = `/api/characters/${copy.id}`;
  assert.deepEqual(expect(await request(`${copyRoute}/revisions`, account.cookie), 200), []);
  assert.equal(expect(await request(`${copyRoute}/secret`, gm.cookie), 200).notes, '');
  assert.equal(expect(await request(`${copyRoute}/favorite`, player.cookie), 200).favorite, false);
  const copiedItems = expect(await request(`${copyRoute}/inventory`, account.cookie), 200);
  const copiedAbilities = expect(await request(`${copyRoute}/abilities`, account.cookie), 200);
  assert.equal(copiedItems.length, 1); assert.equal(copiedAbilities.length, 1);
  assert.notEqual(copiedItems[0].id, item.id); assert.notEqual(copiedAbilities[0].id, ability.id);
  for (const [key, value] of Object.entries(itemInput)) assert.equal(copiedItems[0][key], value, `Inventory ${key}`);
  for (const [key, value] of Object.entries(abilityInput)) assert.equal(copiedAbilities[0][key], value, `Ability ${key}`);
  expect(await request(copyRoute, account.cookie, 'PUT', { ...copy, health: 1 }), 200);
  assert.equal(expect(await request(route, player.cookie), 200).health, character.health, 'Copies are independent');
}
assert.equal(expect(await request(`/api/overlay/${overlay.key}`, null), 200).health, character.health, 'Original OBS capability still belongs to the original');
console.log('Character lifecycle HTTP: list ownership, private history, revision conflicts, master fields, historical restore, independent inventory/ability copies and secret/favorite/OBS isolation passed.');
