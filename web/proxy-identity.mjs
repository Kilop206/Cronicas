// Cloudflare supplies CF-Connecting-IP at the edge. Never trust client-supplied Cronicas headers.
export async function setProxyIdentity(headers, incoming, secret) {
  for (const name of ['X-Cronicas-Client-IP', 'X-Cronicas-Proxy-Time', 'X-Cronicas-Proxy-Signature']) headers.delete(name);
  const ip = incoming.headers.get('CF-Connecting-IP');
  if (typeof secret !== 'string' || secret.length < 32 || !ip || !/^[0-9a-f:.]{3,64}$/i.test(ip)) return;
  const timestamp = String(Math.floor(Date.now() / 1000));
  const encoder = new TextEncoder();
  const key = await crypto.subtle.importKey('raw', encoder.encode(secret), {name:'HMAC', hash:'SHA-256'}, false, ['sign']);
  const signature = await crypto.subtle.sign('HMAC', key, encoder.encode(`v1\n${timestamp}\n${ip}`));
  headers.set('X-Cronicas-Client-IP', ip);
  headers.set('X-Cronicas-Proxy-Time', timestamp);
  headers.set('X-Cronicas-Proxy-Signature', Array.from(new Uint8Array(signature), byte => byte.toString(16).padStart(2,'0')).join(''));
}
