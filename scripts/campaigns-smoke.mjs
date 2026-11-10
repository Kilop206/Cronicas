import assert from 'node:assert/strict';

const base = process.env.SMOKE_BASE_URL || 'http://127.0.0.1:8082';
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Only local fixtures are allowed');
const suffix = crypto.randomUUID();
async function request(route, cookie, method = 'GET', body) {
  const response = await fetch(new URL(route, base), { method, signal: AbortSignal.timeout(15000), headers: { ...(cookie ? { Cookie: cookie } : {}), ...(body ? { 'Content-Type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const text = await response.text(); return { status: response.status, body: text ? JSON.parse(text) : null, headers: response.headers, cookie: response.headers.get('set-cookie')?.split(';')[0] };
}
function expect(response, status) { assert.equal(response.status, status, JSON.stringify(response.body)); return response.body; }
async function account(name) { const response = await request('/api/auth/register', null, 'POST', { displayName: name, email: `${name}-${suffix}@example.test`, password: 'campaign-smoke-local-only-123' }); expect(response, 201); return response; }
const owner = await account('campaign-owner'), player = await account('campaign-player'), other = await account('campaign-other');
expect(await request('/api/campaigns', null), 401);
const details = { name: `Campaign module ${suffix}`, description: '  Description  ', accentColor: '#aBc123' };
expect(await request('/api/campaigns', owner.cookie, 'POST', { ...details, accentColor: '#abcdef\n' }), 400);
let campaign = expect(await request('/api/campaigns', owner.cookie, 'POST', details), 201);
assert.deepEqual(Object.keys(campaign).sort(), ['id', 'name', 'description', 'status', 'accentColor', 'role', 'updatedAt', 'systemId', 'worldId'].sort());
assert.equal(campaign.description, 'Description'); assert.equal(campaign.role, 'owner');
const path = `/api/campaigns/${campaign.id}`;
expect(await request(path, other.cookie), 404);
assert.equal(expect(await request('/api/campaigns', other.cookie), 200).total, 0);
const list = expect(await request(`/api/campaigns?page=-1&search=${encodeURIComponent(suffix)}`, owner.cookie), 200);
assert.equal(list.page, 1); assert.equal(list.items[0].id, campaign.id);
campaign = expect(await request(path, owner.cookie, 'PATCH', { ...details, name: `Updated ${suffix}`, description: '' }), 200);
expect(await request(`${path}/status`, owner.cookie, 'PATCH', { status: 'active' }), 204);
assert.equal(expect(await request(path, owner.cookie), 200).status, 'active');
expect(await request(`${path}/status`, owner.cookie, 'PATCH', { status: 'invented' }), 400);
expect(await request(`${path}/invites`, owner.cookie, 'POST', { role: 'owner' }), 400);
async function invite(role = 'player') { return expect(await request(`${path}/invites`, owner.cookie, 'POST', { role }), 201); }
const first = await invite(), second = await invite();
assert.match(first.token, /^[a-f0-9]{48}$/);
expect(await request(`/api/invites/${first.token}`, null), 401);
const previewResponse = await request(`/api/invites/${first.token.toUpperCase()}`, player.cookie);
assert.equal(expect(previewResponse, 200).campaignId, campaign.id); assert.match(previewResponse.headers.get('cache-control'), /no-store/);
const race = await Promise.all([request(`/api/invites/${first.token}/accept`, player.cookie, 'POST', {}), request(`/api/invites/${second.token}/accept`, player.cookie, 'POST', {})]);
assert.deepEqual(race.map(r => r.status).sort(), [200, 409], 'distinct token race has one membership and no 500');
const unused = race[0].status === 200 ? second : first;
expect(await request(`/api/invites/${unused.token}/accept`, other.cookie, 'POST', {}), 200);
expect(await request(`/api/invites/${unused.token}/accept`, player.cookie, 'POST', {}), 404, 'used token is single-use');
assert.equal(expect(await request(`${path}/members`, player.cookie), 200).length, 3);
expect(await request(path, player.cookie, 'PATCH', details), 403);
expect(await request(`${path}/invites`, player.cookie, 'POST', { role: 'player' }), 403);
expect(await request(`${path}/members/${player.body.id}`, player.cookie, 'PATCH', { role: 'gm' }), 404);
expect(await request(`${path}/members/${owner.body.id}`, owner.cookie, 'PATCH', { role: 'player' }), 400);
expect(await request(`${path}/members/${owner.body.id}`, player.cookie, 'DELETE'), 403);
expect(await request(`${path}/members/${other.body.id}`, player.cookie, 'DELETE'), 403);
const character = expect(await request(`${path}/characters`, player.cookie, 'POST', { name: 'Campaign access fixture', portraitUrl: null, backgroundUrl: null, health: 10, maxHealth: 10, sanity: 10, maxSanity: 10, mana: 10, maxMana: 10, fields: {}, version: 1 }), 201);
expect(await request(`${path}/members/${other.body.id}`, owner.cookie, 'PATCH', { role: 'co-gm' }), 204);
expect(await request(`/api/characters/${character.id}/secret`, other.cookie), 200);
expect(await request(`${path}/members/${other.body.id}`, owner.cookie, 'PATCH', { role: 'spectator' }), 204);
expect(await request(`/api/characters/${character.id}/secret`, other.cookie), 404);
expect(await request(`${path}/members/${player.body.id}`, owner.cookie, 'PATCH', { role: 'spectator' }), 204);
expect(await request(`/api/characters/${character.id}`, player.cookie, 'PUT', character), 403);
expect(await request(`${path}/members/${player.body.id}`, owner.cookie, 'PATCH', { role: 'player' }), 204);
expect(await request(`${path}/members/${player.body.id}`, player.cookie, 'DELETE'), 204);
expect(await request(`/api/characters/${character.id}`, player.cookie), 404);
expect(await request(`${path}/members`, player.cookie), 404);
assert.equal(expect(await request(`/api/characters/${character.id}`, owner.cookie), 200).name, character.name, 'leave preserves character data');
expect(await request(`${path}/members/${owner.body.id}`, owner.cookie, 'DELETE'), 403);
console.log('Campaign HTTP: legacy contracts, list/search/details/status, capability/no-store, distinct-token concurrency, roles, protected owner, resource permissions, self-leave and preserved characters passed.');
