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
  const r = await request('/api/auth/register', null, 'POST', { displayName: name, email: `${name}-${suffix}@example.test`, password: 'companions-local-only-123' });
  expect(r, 201); return r;
}
const gm = await account('companion-gm'), player = await account('companion-player'), spectator = await account('companion-spectator');
const campaign = expect(await request('/api/campaigns', gm.cookie, 'POST', { name: `Companions ${suffix}`, description: '', accentColor: '#a54d52' }), 201);
const input = { name: 'Companion character', portraitUrl: null, backgroundUrl: null, health: 10, maxHealth: 20,
  sanity: 10, maxSanity: 30, mana: 5, maxMana: 10, fields: { nex: 35, nivel: 5, defesa: 18, sorte: 50, focus: 3, limit: 8, private_text: 'Unlisted field' }, version: 1 };
const gmCharacter = expect(await request(`/api/campaigns/${campaign.id}/characters`, gm.cookie, 'POST', input), 201);
expect(await request(`/api/characters/${gmCharacter.id}/favorite`, player.cookie, 'PUT'), 404);
for (const [user, role] of [[player, 'player'], [spectator, 'spectator']]) {
  const invite = expect(await request(`/api/campaigns/${campaign.id}/invites`, gm.cookie, 'POST', { role }), 201);
  expect(await request(`/api/invites/${invite.token}/accept`, user.cookie, 'POST', {}), 200);
}
const character = expect(await request(`/api/campaigns/${campaign.id}/characters`, player.cookie, 'POST', input), 201);
const route = `/api/characters/${character.id}`;
expect(await request(`${route}/favorite`, null), 401);
assert.deepEqual(expect(await request(`${route}/favorite`, player.cookie), 200), { favorite: false });
const favorites = await Promise.all([1, 2].map(() => request(`${route}/favorite`, player.cookie, 'PUT')));
assert.ok(favorites.every(r => r.status === 204), JSON.stringify(favorites));
expect(await request(`${route}/favorite`, spectator.cookie, 'PUT'), 204);
assert.equal(expect(await request(`${route}/favorite`, gm.cookie), 200).favorite, false, 'Favorites are personal');
assert.equal(expect(await request('/api/dashboard', player.cookie), 200).favoriteCharacters.filter(c => c.characterId === character.id).length, 1);
for (const user of [player, spectator]) {
  expect(await request(`${route}/secret`, user.cookie), 404);
  expect(await request(`${route}/secret`, user.cookie, 'PUT', { notes: 'Forbidden', revision: 1 }), 404);
}
assert.deepEqual(expect(await request(`${route}/secret`, gm.cookie), 200), { notes: '', revision: 1, updatedAt: null });
expect(await request(`${route}/secret`, gm.cookie, 'PUT', { notes: null, revision: 1 }), 400);
let secret = expect(await request(`${route}/secret`, gm.cookie, 'PUT', { notes: '  Private plan  ', revision: 1, expectedExists: false }), 200);
assert.equal(secret.notes, 'Private plan'); assert.equal(secret.revision, 1);
expect(await request(`${route}/secret`, gm.cookie, 'PUT', { notes: 'Stale empty draft', revision: 1, expectedExists: false }), 409);
secret = expect(await request(`${route}/secret`, gm.cookie, 'PUT', { notes: 'Current private plan', revision: 1 }), 200);
assert.equal(secret.revision, 2);
const secrets = await Promise.all(['First', 'Second'].map(notes => request(`${route}/secret`, gm.cookie, 'PUT', { notes, revision: 2 })));
assert.deepEqual(secrets.map(r => r.status).sort(), [200, 409]);
const secretRead = await request(`${route}/secret`, gm.cookie);
expect(secretRead, 200); assert.equal(secretRead.headers.get('cache-control'), 'private, no-store');
expect(await request(`/api/campaigns/${campaign.id}/members/${spectator.body.id}`, gm.cookie, 'PATCH', { role: 'co-gm' }), 204);
expect(await request(`${route}/secret`, spectator.cookie), 200);
expect(await request(`/api/campaigns/${campaign.id}/members/${spectator.body.id}`, gm.cookie, 'PATCH', { role: 'spectator' }), 204);
expect(await request(`${route}/secret`, spectator.cookie), 404);
expect(await request(`${route}/overlay-key`, gm.cookie, 'POST', {}), 404);
expect(await request(`${route}/overlay-key`, spectator.cookie, 'POST', {}), 404);
const first = await request(`${route}/overlay-key`, player.cookie, 'POST', {});
let key = expect(first, 200).key; assert.match(key, /^[0-9a-f]{64}$/); assert.equal(first.headers.get('cache-control'), 'private, no-store');
const old = key; key = expect(await request(`${route}/overlay-key`, player.cookie, 'POST', {}), 200).key;
assert.notEqual(key, old); expect(await request(`/api/overlay/${old}`, null), 404);
expect(await request('/api/overlay/invalid', null), 404);
let hud = await request(`/api/overlay/${key.toUpperCase()}`, null);
expect(hud, 200); assert.equal(hud.headers.get('cache-control'), 'no-store');
for (const field of ['id', 'ownerId', 'campaignId', 'fields', 'notes', 'overlayKeyHash']) assert.ok(!(field in hud.body), field);
assert.ok(!JSON.stringify(hud.body).includes('Unlisted field'));
const fields = ['nex', 'nivel', 'defesa', 'sorte', 'focus', 'limit'].map(key => ({ key, label: key, type: 'number', section: 'Fixture', min: 0, max: 99, options: null }));
for (const [templateId, templateVersion, rulesetVersion, expected] of [
  ['ordem_paranormal', 7, '1.3', ['PV', 'PE', 'SAN']], ['dnd_5e', 5, '2014', ['HP']],
  ['tormenta20', 4, 'jogo-do-ano-2024-v1.3', ['PV', 'PM']], ['call_of_cthulhu_7e', 6, '7e', ['HP', 'SAN', 'Magic Points']]
]) {
  const system = expect(await request('/api/systems', gm.cookie, 'POST', { name: 'OBS fixture', description: '', fields, templateId, templateVersion, rulesetVersion, rendererMode: 'specialized' }), 201);
  expect(await request(`/api/campaigns/${campaign.id}/system`, gm.cookie, 'PATCH', { systemId: system.id }), 204);
  hud = await request(`/api/overlay/${key}`, null); expect(hud, 200);
  assert.deepEqual(hud.body.resources.map(r => r.label), expected);
}
expect(await request(`/api/campaigns/${campaign.id}/members/${player.body.id}`, gm.cookie, 'PATCH', { role: 'spectator' }), 204);
expect(await request(`${route}/overlay-key`, player.cookie, 'POST', {}), 404);
expect(await request(`/api/overlay/${key}`, null), 200, 'Existing capability remains valid until explicitly rotated');
expect(await request(`${route}/favorite`, player.cookie, 'DELETE'), 204);
assert.equal(expect(await request(`${route}/favorite`, spectator.cookie), 200).favorite, true);
assert.equal(expect(await request(route, gm.cookie), 200).version, 1);
assert.deepEqual(expect(await request(`${route}/revisions`, gm.cookie), 200), []);
console.log('Companion HTTP: personal/idempotent favorites, private notes/conflicts/revocation, owner-only OBS rotation, no-store safe projection and four RPG resource profiles passed.');
