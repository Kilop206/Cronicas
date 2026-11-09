import assert from 'node:assert/strict';

const base = process.env.SMOKE_BASE_URL || 'http://127.0.0.1:8082';
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Only local fixtures are allowed');
const suffix = crypto.randomUUID();
async function request(route, cookie, method = 'GET', body) {
  const r = await fetch(new URL(route, base), { method, headers: { ...(cookie ? { Cookie: cookie } : {}),
    ...(body ? { 'Content-Type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const text = await r.text(); return { status: r.status, body: text ? JSON.parse(text) : null, headers: r.headers,
    cookie: r.headers.get('set-cookie')?.split(';')[0] };
}
function expect(r, status) { assert.equal(r.status, status, JSON.stringify(r.body)); return r.body; }
async function account(name) {
  const r = await request('/api/auth/register', null, 'POST', { displayName: name, email: `${name}-${suffix}@example.test`, password: 'assets-local-only-123' });
  expect(r, 201); return r;
}
const gm = await account('assets-gm'), player = await account('assets-player'), spectator = await account('assets-spectator');
const campaign = expect(await request('/api/campaigns', gm.cookie, 'POST', { name: `Assets ${suffix}`, description: '', accentColor: '#a54d52' }), 201);
const input = { name: 'GM character', portraitUrl: null, backgroundUrl: null, health: 10, maxHealth: 20,
  sanity: 10, maxSanity: 20, mana: 10, maxMana: 20, fields: {}, version: 1 };
const other = expect(await request(`/api/campaigns/${campaign.id}/characters`, gm.cookie, 'POST', input), 201);
expect(await request(`/api/characters/${other.id}/inventory`, player.cookie), 404);
for (const [user, role] of [[player, 'player'], [spectator, 'spectator']]) {
  const invite = expect(await request(`/api/campaigns/${campaign.id}/invites`, gm.cookie, 'POST', { role }), 201);
  expect(await request(`/api/invites/${invite.token}/accept`, user.cookie, 'POST', {}), 200);
}
const character = expect(await request(`/api/campaigns/${campaign.id}/characters`, player.cookie, 'POST', { ...input, name: 'Player character' }), 201);
for (const [collection, values, invalid, property, replacement] of [
  ['inventory', { name: ' Rope ', category: 'equipment', quantity: 2, weight: 1.25, notes: ' Notes ', version: 999 }, { weight: 1.001 }, 'quantity', 3],
  ['abilities', { name: ' Action ', description: ' Description ', cost: ' 2 ', effect: ' Effect ', duration: ' Turn ', limitations: ' Limit ', recharge: ' Rest ', rollExpression: ' 1D6+2 ', version: 999 }, { rollExpression: 'eval(1)' }, 'effect', 'Changed']
]) {
  const route = `/api/characters/${character.id}/${collection}`;
  expect(await request(route, null), 401);
  expect(await request(route, spectator.cookie, 'POST', values), 403);
  expect(await request(`/api/characters/${other.id}/${collection}`, player.cookie, 'POST', values), 403);
  expect(await request(route, player.cookie, 'POST', { ...values, ...invalid }), 400);
  expect(await request(route, player.cookie, 'POST', { ...values, name: null }), 400);
  const response = await request(route, player.cookie, 'POST', values);
  const created = expect(response, 201); assert.equal(created.version, 1); assert.equal(created.name, values.name.trim());
  assert.equal(response.headers.get('location'), `${route}/${created.id}`);
  const entry = `${route}/${created.id}`;
  assert.equal(expect(await request(route, spectator.cookie), 200)[0].id, created.id);
  expect(await request(`/api/characters/${other.id}/${collection}/${created.id}`, gm.cookie, 'PUT', { ...values, version: 1 }), 404);
  expect(await request(`${entry}?version=1`, spectator.cookie, 'DELETE'), 403);
  const saved = expect(await request(entry, gm.cookie, 'PUT', { ...values, [property]: replacement, version: 1 }), 200);
  assert.equal(saved.version, 2); assert.equal(saved[property], replacement);
  // PostgreSQL stores microseconds; the immediate .NET response can contain a seventh fractional digit.
  assert.equal(Date.parse(saved.createdAt), Date.parse(created.createdAt));
  expect(await request(entry, player.cookie, 'PUT', { ...values, version: 1 }), 409);
  expect(await request(`${entry}?version=1`, player.cookie, 'DELETE'), 409);
  const concurrent = await Promise.all([1, 2].map(i => request(entry, player.cookie, 'PUT', { ...values, name: `Winner ${i}`, version: 2 })));
  assert.deepEqual(concurrent.map(r => r.status).sort(), [200, 409]);
  const winner = concurrent.find(r => r.status === 200).body;
  assert.equal(winner.version, 3);
  const reloaded = expect(await request(route, player.cookie), 200)[0];
  for (const key of ['createdAt', 'updatedAt']) { assert.equal(Date.parse(reloaded[key]), Date.parse(winner[key])); reloaded[key] = winner[key]; }
  assert.deepEqual(reloaded, winner);
  expect(await request(`/api/characters/${other.id}/${collection}/${created.id}?version=3`, gm.cookie, 'DELETE'), 404);
  expect(await request(entry, player.cookie, 'DELETE'), 400);
  expect(await request(`${entry}?version=3`, player.cookie, 'DELETE'), 204);
  expect(await request(`${entry}?version=3`, player.cookie, 'DELETE'), 404);
  assert.deepEqual(expect(await request(route, player.cookie), 200), []);
}
assert.equal(expect(await request(`/api/characters/${character.id}`, player.cookie), 200).version, 1);
assert.deepEqual(expect(await request(`/api/characters/${character.id}/revisions`, player.cookie), 200), []);
console.log('Character assets HTTP: legacy DTOs/location, public lists, ownership/roles, scoped child IDs, validation, independent revisions, concurrent edits and revisioned deletion passed.');
