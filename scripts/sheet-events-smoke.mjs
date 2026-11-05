import assert from 'node:assert/strict';
import signalR from '../web/node_modules/@microsoft/signalr/dist/cjs/index.js';

const base = process.env.SMOKE_BASE_URL || 'http://127.0.0.1:5099';
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'This suite only writes to a local server');
const suffix = crypto.randomUUID();
async function request(path, cookie, method = 'GET', body) {
  const response = await fetch(new URL(path, base), { method, headers: {
    ...(cookie ? { Cookie: cookie } : {}), ...(body ? { 'Content-Type': 'application/json' } : {})
  }, body: body ? JSON.stringify(body) : undefined });
  const text = await response.text();
  return { status: response.status, body: text ? JSON.parse(text) : null, headers: response.headers, cookie: response.headers.get('set-cookie')?.split(';')[0] };
}
async function register(name) {
  const response = await request('/api/auth/register', null, 'POST', { displayName: name, email: `${name}-${suffix}@example.test`, password: 'architecture-local-password-123' });
  assert.equal(response.status, 201, JSON.stringify(response.body)); return response;
}
function next(hub, name) {
  return new Promise((resolve, reject) => {
    const handler = message => { clearTimeout(timer); hub.off(name, handler); resolve(message); };
    const timer = setTimeout(() => { hub.off(name, handler); reject(new Error(`Timed out waiting for ${name}`)); }, 12000);
    hub.on(name, handler);
  });
}
const owner = await register('architecture-owner');
const outsider = await register('architecture-player');
const definition = { name: 'Architecture OP fixture', description: '', templateId: 'ordem_paranormal', templateVersion: 7,
  rulesetVersion: '1.3', fields: [
    { key: 'nex', label: 'NEX', type: 'number', section: 'Agent', min: 5, max: 99, options: null },
    { key: 'estado_regras', label: 'State', type: 'table', section: 'Rules', min: null, max: null, options: null,
      columns: [{ key: 'chave', label: 'Key', type: 'text' }, { key: 'valor', label: 'Value', type: 'number' }] }
  ] };
const system = await request('/api/systems', owner.cookie, 'POST', definition); assert.equal(system.status, 201, JSON.stringify(system.body));
const campaign = await request('/api/campaigns', owner.cookie, 'POST', { name: `Architecture ${suffix}`, description: '', accentColor: '#9d4545' });
assert.equal(campaign.status, 201);
assert.equal((await request(`/api/campaigns/${campaign.body.id}/system`, owner.cookie, 'PATCH', { systemId: system.body.id })).status, 204);
const created = await request(`/api/campaigns/${campaign.body.id}/characters`, owner.cookie, 'POST', { name: 'Architecture agent', portraitUrl: null, backgroundUrl: null,
  health: 20, maxHealth: 30, sanity: 40, maxSanity: 50, mana: 10, maxMana: 20, fields: { nex: 35 }, version: 1 });
assert.equal(created.status, 201, JSON.stringify(created.body));
const characterId = created.body.id;
const read = `/api/v1/characters/${characterId}/sheet`;
assert.equal((await request(read)).status, 401);
assert.equal((await request(read, outsider.cookie)).status, 404);
const envelope = await request(read, owner.cookie);
assert.equal(envelope.status, 200); assert.equal(envelope.body.revision, 1); assert.equal(envelope.body.systemVersion, '1.3');
assert.equal(envelope.body.schemaVersion, 7); assert.equal(envelope.body.rendererId, 'ordem_paranormal');
assert.equal(envelope.headers.get('etag'), '"1"'); assert.equal(envelope.headers.get('cache-control'), 'private, no-store');
assert.ok(envelope.headers.get('x-correlation-id'));
assert.equal((await request('/api/v1/rpg-plugins', owner.cookie)).body.filter(p => p.manifest.id === 'ordem_paranormal').length, 1);
assert.equal((await request('/api/v1/rpg-plugins')).status, 401);
const connections = [owner, outsider].map(account => new signalR.HubConnectionBuilder()
  .withUrl(new URL('/api/hubs/sheets', base).toString(), { headers: { Cookie: account.cookie }, withCredentials: true })
  .configureLogging(signalR.LogLevel.Error).build());
try {
  for (const connection of connections) await connection.start();
  await connections[0].invoke('Subscribe', characterId);
  await assert.rejects(connections[1].invoke('Subscribe', characterId), /negado/i);
  const invite = await request(`/api/campaigns/${campaign.body.id}/invites`, owner.cookie, 'POST', { role: 'player' });
  assert.equal(invite.status, 201);
  assert.equal((await request(`/api/invites/${invite.body.token}/accept`, outsider.cookie, 'POST', {})).status, 200);
  await connections[1].invoke('Subscribe', characterId);
  const ownNotice = next(connections[0], 'SheetInvalidated'); const playerNotice = next(connections[1], 'SheetInvalidated');
  const applied = await request(`/api/characters/${characterId}/rules`, owner.cookie, 'POST', { expectedVersion: 1, command: { action: 'damage', amount: 3, visibility: 'private' } });
  assert.equal(applied.status, 200, JSON.stringify(applied.body)); assert.equal(applied.body.character.health, 17);
  const message = await ownNotice; const received = await playerNotice;
  assert.deepEqual(Object.keys(message).sort(), ['characterId', 'eventId', 'revision', 'type']);
  assert.equal(message.revision, 2); assert.equal(received.eventId, message.eventId);
  assert.equal((await request(`/api/characters/${characterId}/rules`, outsider.cookie, 'POST', { expectedVersion: 2, command: { action: 'heal', amount: 1 } })).status, 403);
  assert.equal((await request(`/api/characters/${characterId}/rules`, owner.cookie, 'POST', { expectedVersion: 1, command: { action: 'heal', amount: 1 } })).status, 409);
  assert.equal((await request(read, owner.cookie)).body.state.health, 17);
  let revokedMessages = 0; connections[1].on('SheetInvalidated', () => revokedMessages++);
  assert.equal((await request(`/api/campaigns/${campaign.body.id}/members/${outsider.body.id}`, owner.cookie, 'DELETE')).status, 204);
  const delivered = next(connections[0], 'SheetInvalidated');
  assert.equal((await request(`/api/characters/${characterId}/rules`, owner.cookie, 'POST', { expectedVersion: 2, command: { action: 'heal', amount: 1, visibility: 'private' } })).status, 200);
  await delivered;
  assert.equal((await request(read, outsider.cookie)).status, 404);
  await assert.rejects(connections[1].invoke('Subscribe', characterId), /negado/i);
  await new Promise(resolve => setTimeout(resolve, 250));
  assert.equal(revokedMessages, 0, 'Membership revocation prevents delivery on an already connected socket');
  await connections[0].stop(); await connections[0].start(); await connections[0].invoke('Subscribe', characterId);
  assert.equal((await request(read, owner.cookie)).body.revision, 3, 'Reconnection can resync authoritative state');
  console.log('Sheet API v1 and SignalR: auth, IDOR, versioned metadata, ETag/correlation, plugin catalog, authoritative OP rules, conflict, notification privacy, revocation and resync passed.');
} finally { await Promise.allSettled(connections.map(c => c.stop())); }
