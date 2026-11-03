import assert from 'node:assert/strict';
import {createHmac} from 'node:crypto';

const base=process.env.SMOKE_BASE_URL || 'http://127.0.0.1:5099';
assert.ok(['127.0.0.1','localhost'].includes(new URL(base).hostname),'This suite writes only to a local test API.');
const key=process.env.PROXY_IDENTITY_KEY;
assert.ok(key && key.length>=32,'Start the local API and this suite with the same test-only PROXY_IDENTITY_KEY.');
const suffix=crypto.randomUUID();
async function request(path,ip,body,cookie) {
  const time=String(Math.floor(Date.now()/1000));
  const response=await fetch(base+path,{method:'POST',headers:{'content-type':'application/json',...(cookie?{cookie}:{}),'X-Cronicas-Client-IP':ip,'X-Cronicas-Proxy-Time':time,'X-Cronicas-Proxy-Signature':createHmac('sha256',key).update(`v1\n${time}\n${ip}`).digest('hex')},body:JSON.stringify(body)});
  const text=await response.text();return {status:response.status,body:text?JSON.parse(text):null,cookie:response.headers.get('set-cookie')?.split(';')[0],retryAfter:response.headers.get('Retry-After')};
}
const a='203.0.113.101',b='203.0.113.102';
const first=await request('/api/auth/register',a,{displayName:'Limit A',email:`limits-a-${suffix}@example.test`,password:'local-limits-password-123'});
const second=await request('/api/auth/register',b,{displayName:'Limit B',email:`limits-b-${suffix}@example.test`,password:'local-limits-password-123'});
assert.equal(first.status,201,JSON.stringify(first.body));assert.equal(second.status,201,JSON.stringify(second.body));
for(let i=0;i<20;i++) assert.equal((await request('/api/auth/login',a,{email:`absent-${suffix}@example.test`,password:'wrong-password'})).status,401);
const exhausted=await request('/api/auth/login',a,{email:`absent-${suffix}@example.test`,password:'wrong-password'});
assert.equal(exhausted.status,429);assert.ok(Number(exhausted.retryAfter)>0);assert.ok(exhausted.body.retryAfterSeconds>0);
const fresh=await request('/api/auth/login',b,{email:`limits-b-${suffix}@example.test`,password:'local-limits-password-123'});
assert.equal(fresh.status,200,'a second visitor behind the same socket can sign in on the first attempt');
const campaign=await request('/api/campaigns',a,{name:'Limites por conta',description:'',accentColor:'#aa9acd'},first.cookie);assert.equal(campaign.status,201);
const path=`/api/campaigns/${campaign.body.id}/invites`;
const invite=await request(path,a,{role:'player'},first.cookie);assert.equal(invite.status,201,'exhausted login bucket cannot block a signed-in owner');
const membership=await request(`/api/invites/${invite.body.token}/accept`,a,{},second.cookie);assert.equal(membership.status,200);
for(let i=1;i<60;i++) assert.equal((await request(path,a,{role:'player'},first.cookie)).status,201);
const capped=await request(path,b,{role:'player'},first.cookie);assert.equal(capped.status,429,'the account budget follows the verified session across networks');
const other=await request('/api/campaigns',a,{name:'Segunda conta',description:'',accentColor:'#aa9acd'},second.cookie);assert.equal(other.status,201);
const otherInvite=await request(`/api/campaigns/${other.body.id}/invites`,a,{role:'player'},second.cookie);assert.equal(otherInvite.status,201,'another signed-in account on the same network retains its own budget');
console.log('Rate-limit integration: real sessions, same proxy, first login, isolated invites, account limits and retry metadata passed.');
