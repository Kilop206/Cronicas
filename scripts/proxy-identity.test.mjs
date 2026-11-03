import assert from 'node:assert/strict';
import {createHmac} from 'node:crypto';
import {test} from 'node:test';
import {setProxyIdentity} from '../web/proxy-identity.mjs';
import worker from '../web/fallback-worker.mjs';

const key='test-only-key-for-the-edge-signature-verification';
test('replaces caller identity headers with an HMAC of the Cloudflare visitor IP',async()=>{
  const incoming=new Request('https://example.test/api/auth/login',{headers:{'CF-Connecting-IP':'2001:db8::1','X-Cronicas-Client-IP':'203.0.113.9','X-Cronicas-Proxy-Time':'1','X-Cronicas-Proxy-Signature':'fake'}});
  const headers=new Headers(incoming.headers); await setProxyIdentity(headers,incoming,key);
  const timestamp=headers.get('X-Cronicas-Proxy-Time');
  assert.equal(headers.get('X-Cronicas-Client-IP'),'2001:db8::1');
  assert.equal(headers.get('X-Cronicas-Proxy-Signature'),createHmac('sha256',key).update(`v1\n${timestamp}\n2001:db8::1`).digest('hex'));
  assert.ok(Math.abs(Number(timestamp)-Date.now()/1000)<2);
});
test('strips caller assertions when no key or valid visitor IP is available',async()=>{
  for(const [ip,secret] of [['203.0.113.1',undefined],['203.0.113.1','short'],['evil,203.0.113.1',key],[null,key]]) {
    const incoming=new Request('https://example.test/',{headers:ip?{'CF-Connecting-IP':ip}:{}});
    const headers=new Headers({'X-Cronicas-Client-IP':'forged','X-Cronicas-Proxy-Time':'1','X-Cronicas-Proxy-Signature':'forged'});
    await setProxyIdentity(headers,incoming,secret);
    assert.equal(headers.get('X-Cronicas-Client-IP'),null); assert.equal(headers.get('X-Cronicas-Proxy-Time'),null); assert.equal(headers.get('X-Cronicas-Proxy-Signature'),null);
  }
});
test('fallback worker forwards a verified identity and preserves retry metadata',async()=>{
  const originalFetch=globalThis.fetch;
  globalThis.fetch=async request=>{
    assert.equal(request.headers.get('X-Cronicas-Client-IP'),'203.0.113.7');
    const timestamp=request.headers.get('X-Cronicas-Proxy-Time');
    assert.equal(request.headers.get('X-Cronicas-Proxy-Signature'),createHmac('sha256',key).update(`v1\n${timestamp}\n203.0.113.7`).digest('hex'));
    return Response.json({retryAfterSeconds:5},{status:429,headers:{'Retry-After':'5'}});
  };
  try {
    const request=new Request('https://example.test/api/auth/login',{method:'POST',headers:{origin:'https://example.test','CF-Connecting-IP':'203.0.113.7'}});
    const response=await worker.fetch(request,{API_ORIGIN:'https://api.example.test',PROXY_IDENTITY_KEY:key});
    assert.equal(response.status,429); assert.equal(response.headers.get('Retry-After'),'5'); assert.equal(response.headers.get('cache-control'),'no-store');
  } finally {globalThis.fetch=originalFetch;}
});
