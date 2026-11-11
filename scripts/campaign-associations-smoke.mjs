import assert from 'node:assert/strict';
import signalR from '../web/node_modules/@microsoft/signalr/dist/cjs/index.js';

const base = process.env.SMOKE_BASE_URL || 'http://127.0.0.1:8082';
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Only local fixtures are allowed');
const suffix = crypto.randomUUID();
async function request(route, cookie, method = 'GET', body) {
  const r = await fetch(new URL(route, base), { method, signal: AbortSignal.timeout(15000), headers: { ...(cookie ? { Cookie: cookie } : {}), ...(body ? { 'Content-Type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const text = await r.text(); return { status: r.status, body: text ? JSON.parse(text) : null };
}
function expect(r, status) { assert.equal(r.status, status, JSON.stringify(r.body)); return r.body; }
async function account(name) {
  const r = await fetch(new URL('/api/auth/register', base), { method: 'POST', signal: AbortSignal.timeout(15000), headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ displayName: name, email: `${name}-${suffix}@example.test`, password: 'association-smoke-local-only-123' }) });
  assert.equal(r.status, 201); return { cookie: r.headers.get('set-cookie')?.split(';')[0], user: await r.json() };
}
const owner = await account('association-owner'), player = await account('association-player'), other = await account('association-other');
const fields = [{ key: 'score', label: 'Original score', type: 'number', section: 'Stats', min: 0, max: 20, options: null }];
async function system(name, actor = owner) { return expect(await request('/api/systems', actor.cookie, 'POST', { name, description: '', fields }), 201); }
async function world(name, actor = owner) { return expect(await request('/api/worlds', actor.cookie, 'POST', { name, description: '' }), 201); }
const first = await system('Original association'), second = await system('Next association'), foreignSystem = await system('Foreign system', other);
const ownedWorld = await world('Owned world'), foreignWorld = await world('Foreign world', other);
const campaign = expect(await request('/api/campaigns', owner.cookie, 'POST', { name: `Associations ${suffix}`, description: '', accentColor: '#abcdef' }), 201);
const route = `/api/campaigns/${campaign.id}`;
expect(await request(`${route}/system`, owner.cookie, 'PATCH', { systemId: first.id }), 204);
const invitation = expect(await request(`${route}/invites`, owner.cookie, 'POST', { role: 'player' }), 201);
expect(await request(`/api/invites/${invitation.token}/accept`, player.cookie, 'POST', {}), 200);
const input = { name: 'Associated character', portraitUrl: null, backgroundUrl: null, health: 3, maxHealth: 20, sanity: 4, maxSanity: 10, mana: 2, maxMana: 10, fields: { score: 4 }, version: 1 };
const dynamic = expect(await request(`${route}/characters`, player.cookie, 'POST', input), 201);
let pinned = expect(await request(`${route}/characters`, player.cookie, 'POST', { ...input, name: 'Pinned character' }), 201);
pinned = expect(await request(`/api/v1/characters/${pinned.id}/system-version`, owner.cookie, 'POST', { expectedVersion: 1, registrySystemId: first.id, templateRevision: first.revision }), 200);
expect(await request(`${route}/system`, null, 'PATCH', { systemId: second.id }), 401);
expect(await request(`${route}/system`, player.cookie, 'PATCH', { systemId: second.id }), 404);
expect(await request(`${route}/world`, other.cookie, 'PATCH', { worldId: foreignWorld.id }), 404);
expect(await request(`${route}/system`, owner.cookie, 'PATCH', { systemId: foreignSystem.id }), 400);
expect(await request(`${route}/world`, owner.cookie, 'PATCH', { worldId: foreignWorld.id }), 400);
expect(await request(`${route}/system`, owner.cookie, 'PATCH', { systemId: crypto.randomUUID() }), 400);
expect(await request(`/api/worlds/${ownedWorld.id}`, player.cookie), 404);
const live = new signalR.HubConnectionBuilder().withUrl(new URL('/api/hubs/sheets', base).toString(), { headers: { Cookie: player.cookie } }).configureLogging(signalR.LogLevel.Error).build();
const fixed = new signalR.HubConnectionBuilder().withUrl(new URL('/api/hubs/sheets', base).toString(), { headers: { Cookie: player.cookie } }).configureLogging(signalR.LogLevel.Error).build();
let pinnedChanges = 0; fixed.on('SheetInvalidated', event => { if (event.type === 'CampaignSystemChanged') pinnedChanges++; });
try {
  await live.start(); await fixed.start(); await live.invoke('Subscribe', dynamic.id); await fixed.invoke('Subscribe', pinned.id);
  const changed = new Promise((resolve, reject) => { const timeout = setTimeout(() => reject(new Error('Missing campaign system invalidation')), 7000); live.on('SheetInvalidated', event => { if (event.type === 'CampaignSystemChanged') { clearTimeout(timeout); resolve(event); } }); });
  expect(await request(`${route}/system`, owner.cookie, 'PATCH', { systemId: second.id }), 204);
  const event = await changed;
  assert.deepEqual(Object.keys(event).sort(), ['eventId', 'type', 'characterId', 'revision'].sort()); assert.equal(event.characterId, dynamic.id); assert.equal(event.revision, null);
  expect(await request(`${route}/world`, owner.cookie, 'PATCH', { worldId: ownedWorld.id }), 204);
  assert.equal(expect(await request(`/api/worlds/${ownedWorld.id}`, player.cookie), 200).id, ownedWorld.id);
  assert.equal(expect(await request(`/api/characters/${pinned.id}/system`, player.cookie), 200).id, first.id);
  assert.equal(expect(await request(`/api/characters/${dynamic.id}/system`, player.cookie), 200).id, second.id);
  assert.equal(expect(await request(`/api/characters/${dynamic.id}`, player.cookie), 200).version, dynamic.version);
  assert.deepEqual(expect(await request(`/api/characters/${pinned.id}`, player.cookie), 200).fields, pinned.fields);
  expect(await request(`/api/worlds/${ownedWorld.id}`, owner.cookie, 'DELETE'), 409);
  expect(await request(`/api/systems/${second.id}`, owner.cookie, 'DELETE'), 409);
  const before = expect(await request(route, owner.cookie), 200);
  expect(await request(`${route}/system`, owner.cookie, 'PATCH', { systemId: second.id }), 204);
  assert.equal(expect(await request(route, owner.cookie), 200).updatedAt, before.updatedAt, 'no-op does not rewrite campaign timestamp');
  for (const kind of ['system', 'world']) {
    const target = kind === 'system' ? await system('Deletion race') : await world('Deletion race');
    const results = await Promise.all([request(`${route}/${kind}`, owner.cookie, 'PATCH', { [`${kind}Id`]: target.id }), request(`/api/${kind === 'system' ? 'systems' : 'worlds'}/${target.id}`, owner.cookie, 'DELETE')]);
    assert.ok(results[0].status === 204 && results[1].status === 409 || results[0].status === 400 && results[1].status === 204, JSON.stringify(results));
  }
  expect(await request(`${route}/system`, owner.cookie, 'PATCH', { systemId: null }), 204);
  expect(await request(`${route}/world`, owner.cookie, 'PATCH', { worldId: null }), 204);
  assert.equal(expect(await request(`/api/characters/${dynamic.id}/system`, player.cookie), 200), null);
  assert.equal(expect(await request(`/api/characters/${pinned.id}/system`, player.cookie), 200).id, first.id);
  expect(await request(`/api/worlds/${ownedWorld.id}`, player.cookie), 404);
  assert.equal(pinnedChanges, 0, 'campaign association invalidations exclude pinned characters');
  console.log('Campaign associations HTTP/SignalR: owner scope, private targets, public world access/revocation, immutable sheets, preserved state, no-op, delete/link races and metadata-only unbound invalidation passed.');
} finally { await Promise.allSettled([live.stop(), fixed.stop()]); }
