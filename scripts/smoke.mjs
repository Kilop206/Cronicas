const base = process.env.SMOKE_BASE_URL || 'http://localhost:8081';
const suffix = crypto.randomUUID();

async function request(path, { method = 'GET', body, cookie } = {}) {
  const response = await fetch(new URL(path, base), {
    method,
    headers: { ...(body ? { 'content-type': 'application/json' } : {}), ...(cookie ? { cookie } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await response.text();
  return { status: response.status, body: text ? JSON.parse(text) : null, cookie: response.headers.get('set-cookie')?.split(';')[0] };
}
function expect(condition, message) { if (!condition) throw new Error(message); }

const first = await request('/api/auth/register', { method: 'POST', body: { displayName: 'Mestre', email: `mestre-${suffix}@example.test`, password: 'a-long-test-password-123' } });
expect(first.status === 201 && first.cookie, 'Cadastro e cookie de sessão');
const ownerCookie = first.cookie;
const created = await request('/api/campaigns', { method: 'POST', cookie: ownerCookie, body: { name: 'O Farol das Sombras', description: 'Campanha de teste', accentColor: '#b39a69' } });
expect(created.status === 201, 'Criação da campanha');
const id = created.body.id;
const dashboard = await request('/api/dashboard', { cookie: ownerCookie });
expect(dashboard.status === 200 && dashboard.body.totalCampaigns === 1, 'Dashboard persistido');
const changed = await request(`/api/campaigns/${id}`, { method: 'PATCH', cookie: ownerCookie, body: { name: 'O Farol Reaceso', description: 'História atualizada', accentColor: '#a18866' } });
expect(changed.status === 200 && changed.body.name === 'O Farol Reaceso', 'Edição pelo proprietário');
const active = await request(`/api/campaigns/${id}/status`, { method: 'PATCH', cookie: ownerCookie, body: { status: 'active' } });
expect(active.status === 204, 'Mudança de status pelo proprietário');
const preference = await request('/api/profile/preferences', { method: 'PATCH', cookie: ownerCookie, body: { accent: 'violet' } });
expect(preference.status === 200 && preference.body.accent === 'violet', 'Preferência persistida');
const profile = await request('/api/auth/me', { cookie: ownerCookie });
expect(profile.status === 200 && profile.body.accent === 'violet', 'Preferência carregada na sessão');
const second = await request('/api/auth/register', { method: 'POST', body: { displayName: 'Jogador', email: `jogador-${suffix}@example.test`, password: 'another-test-password-123' } });
expect(second.status === 201 && second.cookie, 'Cadastro do segundo usuário');
const forbidden = await request(`/api/campaigns/${id}`, { cookie: second.cookie });
expect(forbidden.status === 404, 'Isolamento da campanha');
const forbiddenWrite = await request(`/api/campaigns/${id}`, { method: 'PATCH', cookie: second.cookie, body: { name: 'Invasão', description: '', accentColor: '#ffffff' } });
expect(forbiddenWrite.status === 404, 'Bloqueio de edição por não participante');
let currentCookie = ownerCookie;
if (process.env.SMOKE_MAILPIT_URL) {
  const email = `mestre-${suffix}@example.test`;
  const unknown = await request('/api/auth/password-reset/request', { method: 'POST', body: { email: `ausente-${suffix}@example.test` } });
  expect(unknown.status === 202, 'Resposta genérica para email inexistente');
  const requested = await request('/api/auth/password-reset/request', { method: 'POST', body: { email } });
  expect(requested.status === 202, 'Solicitação de recuperação');
  const mailbox = process.env.SMOKE_MAILPIT_URL;
  let code;
  for (let attempt = 0; attempt < 20; attempt++) {
    const message = await fetch(`${mailbox}/view/latest.txt?query=${encodeURIComponent(`to:${email}`)}`);
    if (message.ok) code = (await message.text()).match(/\b[A-F0-9]{20}\b/)?.[0];
    if (code) break;
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  expect(code, 'Email de recuperação entregue');
  const reset = await request('/api/auth/password-reset/complete', { method: 'POST', body: { email, code, password: 'a-new-long-test-password-123' } });
  expect(reset.status === 204, 'Troca da senha');
  const revoked = await request('/api/dashboard', { cookie: ownerCookie });
  expect(revoked.status === 401, 'Sessões antigas revogadas');
  const reuse = await request('/api/auth/password-reset/complete', { method: 'POST', body: { email, code, password: 'another-new-password-123' } });
  expect(reuse.status === 400, 'Código de uso único');
  const oldPassword = await request('/api/auth/login', { method: 'POST', body: { email, password: 'a-long-test-password-123' } });
  expect(oldPassword.status === 401, 'Senha antiga revogada');
  const login = await request('/api/auth/login', { method: 'POST', body: { email, password: 'a-new-long-test-password-123' } });
  expect(login.status === 200 && login.cookie, 'Login com nova senha');
  currentCookie = login.cookie;
}
const logout = await request('/api/auth/logout', { method: 'POST', cookie: currentCookie });
expect(logout.status === 204, 'Logout');
const afterLogout = await request('/api/dashboard', { cookie: currentCookie });
expect(afterLogout.status === 401, 'Revogação da sessão');
console.log('Smoke: cadastro, campanha, preferências, autorização, recuperação configurada e logout passaram.');
