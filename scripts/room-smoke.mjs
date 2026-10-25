import signalR from '../web/node_modules/@microsoft/signalr/dist/cjs/index.js';
import { rejects } from 'node:assert/strict';

const base = process.env.SMOKE_BASE_URL || 'http://localhost:8081';
const suffix = crypto.randomUUID();
const { HubConnectionBuilder } = signalR;

async function request(path, { method = 'GET', body, cookie } = {}) {
  const response = await fetch(new URL(path, base), {
    method,
    headers: { ...(body ? { 'content-type': 'application/json' } : {}), ...(cookie ? { cookie } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await response.text();
  return { status: response.status, body: text ? JSON.parse(text) : null, cookie: response.headers.get('set-cookie')?.split(';')[0] };
}
function assert(value, message) { if (!value) throw new Error(message); }
async function account(name) {
  const result = await request('/api/auth/register', { method: 'POST', body: {
    displayName: name, email: `${name.toLowerCase()}-${suffix}@example.test`, password: 'room-smoke-password-123',
  } });
  assert(result.status === 201 && result.cookie, `Register ${name}`);
  return result;
}
function connection(cookie) {
  return new HubConnectionBuilder().withUrl(new URL('/api/hubs/campaign', base).toString(), {
    headers: { Cookie: cookie }, withCredentials: true,
  }).build();
}
function event(hub, name) {
  return new Promise((resolve, reject) => {
    const timeout = setTimeout(() => reject(new Error(`Missing ${name} event`)), 5000);
    hub.on(name, (...args) => { clearTimeout(timeout); resolve(args); });
  });
}

const owner = await account('Owner');
const player = await account('Player');
const spectator = await account('Spectator');
const campaign = await request('/api/campaigns', { method: 'POST', cookie: owner.cookie, body: {
  name: `Room ${suffix}`, description: 'SignalR smoke', accentColor: '#b39a69',
} });
assert(campaign.status === 201, 'Campaign created');
const id = campaign.body.id;
const hubs = [connection(owner.cookie), connection(player.cookie), connection(spectator.cookie)];

try {
  for (const hub of hubs) await hub.start();
  await rejects(hubs[1].invoke('JoinCampaign', id, 'room'), /negado|denied/i);
  assert((await hubs[0].invoke('JoinCampaign', id, 'room')).length === 0, 'Owner joins empty room');

  for (const [user, role] of [[player, 'player'], [spectator, 'spectator']]) {
    const invite = await request(`/api/campaigns/${id}/invites`, { method: 'POST', cookie: owner.cookie, body: { role } });
    assert(invite.status === 201, `${role} invite created`);
    const accepted = await request(`/api/invites/${invite.body.token}/accept`, { method: 'POST', cookie: user.cookie });
    assert(accepted.status === 200, `${role} invite accepted (${accepted.status})`);
  }

  const joined = event(hubs[0], 'PeerJoined');
  const previous = await hubs[1].invoke('JoinCampaign', id, 'room');
  assert(previous.length === 1 && previous[0].mode === 'room', 'Player sees owner');
  const [peer] = await joined;
  assert(peer.connectionId === hubs[1].connectionId, 'Owner sees player');
  const signal = event(hubs[0], 'Signal');
  await hubs[1].invoke('SendSignal', id, hubs[0].connectionId, 'ice', '{}');
  const [sender, kind, payload] = await signal;
  assert(sender === hubs[1].connectionId && kind === 'ice' && payload === '{}', 'Signal delivered only to peer');
  await rejects(hubs[1].invoke('SendSignal', crypto.randomUUID(), hubs[0].connectionId, 'ice', '{}'), /indisponível|unavailable/i);
  await hubs[2].invoke('JoinCampaign', id, 'room');
  await rejects(hubs[2].invoke('SetMediaState', true, false), /publicar|publish/i);

  const revoked = event(hubs[1], 'MembershipChanged');
  const left = event(hubs[0], 'PeerLeft');
  const removed = await request(`/api/campaigns/${id}/members/${player.body.id}`, { method: 'DELETE', cookie: owner.cookie });
  assert(removed.status === 204, 'Owner removes player');
  await revoked;
  assert((await left)[0] === hubs[1].connectionId, 'Peer removal broadcast');
  await rejects(hubs[1].invoke('SendSignal', id, hubs[0].connectionId, 'ice', '{}'), /indisponível|unavailable/i);
  await rejects(hubs[1].invoke('JoinCampaign', id, 'room'), /negado|denied/i);
  const changed = event(hubs[2], 'MembershipChanged');
  const role = await request(`/api/campaigns/${id}/members/${spectator.body.id}`, {
    method: 'PATCH', cookie: owner.cookie, body: { role: 'player' },
  });
  assert(role.status === 204, 'Owner changes spectator role');
  await changed;
  await rejects(hubs[2].invoke('SetMediaState', true, false), /Entre|join/i);
  const refreshed = await hubs[2].invoke('JoinCampaign', id, 'room');
  assert(refreshed.some(x => x.connectionId === hubs[0].connectionId), 'Changed role rejoins room');
  await hubs[2].invoke('SetMediaState', true, false);
  console.log('Room signaling, authorization, spectator and revocation passed');
} finally {
  await Promise.allSettled(hubs.map(hub => hub.stop()));
}
