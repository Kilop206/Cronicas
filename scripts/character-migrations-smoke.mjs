import assert from 'node:assert/strict';

const base = process.env.SMOKE_BASE_URL || 'http://127.0.0.1:8082';
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Only local fixtures are allowed');
const suffix = crypto.randomUUID();
async function request(route, cookie, method = 'GET', body) {
  const r = await fetch(new URL(route, base), { method, headers: { ...(cookie ? { Cookie: cookie } : {}), ...(body ? { 'Content-Type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const text = await r.text(); return { status: r.status, body: text ? JSON.parse(text) : null, headers: r.headers, cookie: r.headers.get('set-cookie')?.split(';')[0] };
}
function expect(r, status) { assert.equal(r.status, status, JSON.stringify(r.body)); return r.body; }
async function account(name) { const r = await request('/api/auth/register', null, 'POST', { displayName: name, email: `${name}-${suffix}@example.test`, password: 'migration-smoke-local-only-123' }); expect(r, 201); return r; }
const master = await account('migration-master'), player = await account('migration-player'), outsider = await account('migration-outsider');
const fields = [{ key: 'score', label: 'Original score', type: 'number', section: 'Stats', min: 0, max: 20, options: null }];
let model = expect(await request('/api/systems', master.cookie, 'POST', { name: 'Migration original', description: '', fields }), 201);
const campaign = expect(await request('/api/campaigns', master.cookie, 'POST', { name: `Migration ${suffix}`, description: '', accentColor: '#a54d52' }), 201);
expect(await request(`/api/campaigns/${campaign.id}/system`, master.cookie, 'PATCH', { systemId: model.id }), 204);
const invite = expect(await request(`/api/campaigns/${campaign.id}/invites`, master.cookie, 'POST', { role: 'player' }), 201);
expect(await request(`/api/invites/${invite.token}/accept`, player.cookie, 'POST', {}), 200);
let character = expect(await request(`/api/campaigns/${campaign.id}/characters`, player.cookie, 'POST', { name: 'Migration character', portraitUrl: null, backgroundUrl: null, health: 3, maxHealth: 20, sanity: 4, maxSanity: 10, mana: 2, maxMana: 10, fields: { score: 4 }, version: 1 }), 201);
const route = `/api/v1/characters/${character.id}/system-migrations`, sheet = `/api/characters/${character.id}`;
expect(await request(`${route}/targets`, master.cookie), 400, 'pin is required');
character = expect(await request(`/api/v1/characters/${character.id}/system-version`, master.cookie, 'POST', { expectedVersion: 1, registrySystemId: model.id, templateRevision: 1 }), 200);
const original = character.systemVersionId;
model = expect(await request(`/api/systems/${model.id}`, master.cookie, 'PUT', { ...model, name: 'Migration revised', fields: [{ ...fields[0], key: 'new_score', max: 10 }, { ...fields[0], key: 'derived', label: 'Derived', formula: 'new_score * 3', max: 100 }] }), 200);
expect(await request(`${route}/targets`, null), 401);
expect(await request(`${route}/targets`, player.cookie), 403);
expect(await request(`${route}/targets`, outsider.cookie), 404);
const targetsResponse = await request(`${route}/targets`, master.cookie); const targets = expect(targetsResponse, 200);
assert.match(targetsResponse.headers.get('cache-control'), /no-store/);
const target = targets.targets.find(t => t.current);
let input = { expectedVersion: character.version, sourceVersionId: original, targetVersionId: null, targetRevision: target.revision, targetHash: target.contentHash, mappings: { score: 'new_score' }, values: {} };
expect(await request(`${route}/preview`, master.cookie, 'POST', { expectedVersion: 2 }), 400);
expect(await request(`${route}/preview`, master.cookie, 'POST', { ...input, targetVersionId: crypto.randomUUID() }), 404);
expect(await request(`${route}/preview`, player.cookie, 'POST', input), 403);
const invalid = expect(await request(`${route}/preview`, master.cookie, 'POST', { ...input, mappings: {}, values: { new_score: 99 } }), 200);
assert.equal(invalid.canApply, false); assert.ok(invalid.plan.errors['value:new_score']);
const preview = expect(await request(`${route}/preview`, master.cookie, 'POST', input), 200);
assert.equal(preview.canApply, true); assert.equal(preview.plan.fields.derived, 12); assert.ok(preview.plan.preservedKeys.includes('score'));
assert.equal(expect(await request(sheet, player.cookie), 200).version, 2, 'preview is read-only');
expect(await request(route, master.cookie, 'POST', { plan: input, previewHash: 'forged' }), 409);
expect(await request(route, master.cookie, 'POST', { plan: null, previewHash: preview.previewHash }), 400);
expect(await request(route, master.cookie, 'POST', { plan: { ...input, values: { new_score: 3 }, mappings: {} }, previewHash: preview.previewHash }), 409);
const concurrent = await Promise.all([request(route, master.cookie, 'POST', { plan: input, previewHash: preview.previewHash }), request(route, master.cookie, 'POST', { plan: input, previewHash: preview.previewHash })]);
assert.deepEqual(concurrent.map(r => r.status).sort(), [200, 409]); character = concurrent.find(r => r.status === 200).body;
assert.equal(character.version, 3); assert.notEqual(character.systemVersionId, original); assert.equal(character.fields.score, 4); assert.equal(character.fields.new_score, 4); assert.equal(character.fields.derived, 12); assert.equal(character.health, 3);
assert.equal(expect(await request(`${sheet}/system`, player.cookie), 200).name, 'Migration revised');
const history = expect(await request(`${route}/targets`, master.cookie), 200);
assert.equal(history.history[0].receipt.targetVersionId, character.systemVersionId); assert.equal(history.history[0].receipt.sourceVersionId, original);
assert.equal(history.history[0].characterRevision, 3);
const old = history.targets.find(t => t.versionId === original);
input = { expectedVersion: 3, sourceVersionId: character.systemVersionId, targetVersionId: old.versionId, targetRevision: old.revision, targetHash: old.contentHash, mappings: {}, values: {} };
const reverse = expect(await request(`${route}/preview`, master.cookie, 'POST', input), 200);
character = expect(await request(route, master.cookie, 'POST', { plan: input, previewHash: reverse.previewHash }), 200);
assert.equal(character.systemVersionId, original); assert.equal(character.fields.new_score, 4); assert.equal(character.version, 4);
const revisionMetadata = expect(await request(`${sheet}/revisions`, player.cookie), 200);
assert.deepEqual(Object.keys(revisionMetadata[0]).sort(), ['id', 'version', 'createdAt', 'editedBy'].sort(), 'legacy history contract unchanged');
// A target may retire its last field; values must remain usable in ordinary saves afterwards.
model = expect(await request(`/api/systems/${model.id}`, master.cookie, 'PUT', { ...model, name: 'Empty model preserving values', fields: [] }), 200);
const empty = expect(await request(`${route}/targets`, master.cookie), 200).targets.find(t => t.current);
input = { expectedVersion: character.version, sourceVersionId: character.systemVersionId, targetVersionId: null, targetRevision: empty.revision, targetHash: empty.contentHash, mappings: {}, values: {} };
const emptyPreview = expect(await request(`${route}/preview`, master.cookie, 'POST', input), 200);
character = expect(await request(route, master.cookie, 'POST', { plan: input, previewHash: emptyPreview.previewHash }), 200);
character = expect(await request(sheet, player.cookie, 'PUT', { ...character, name: 'Preserved values remain editable' }), 200);
assert.equal(character.fields.score, 4); assert.equal(character.fields.new_score, 4);
console.log('Assisted migration HTTP: master authorization, IDOR, required contracts, read-only review, type correction, forged/changed plans, concurrent commit, preserved fields/resources, audit and reverse migration passed.');
