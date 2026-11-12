import { setProxyIdentity } from './proxy-identity.mjs';
import { onRequest as servePages } from './functions/_middleware.ts';

// Alternate host for networks that reset TLS connections to pages.dev.
export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.pathname === '/api' || url.pathname.startsWith('/api/')) {
      if (!['GET', 'HEAD', 'OPTIONS'].includes(request.method)) {
        const origin = request.headers.get('origin');
        if (origin && origin !== url.origin) return new Response('Origem não permitida.', { status: 403 });
      }
      const upstream = new URL(url.pathname + url.search, env.API_ORIGIN);
      const headers = new Headers(request.headers);
      headers.delete('host');
      headers.delete('origin');
      await setProxyIdentity(headers, request, env.PROXY_IDENTITY_KEY);
      const forwarded = new Request(upstream, { method: request.method, headers, body: request.body, redirect: 'manual' });
      try {
        const response = await fetch(forwarded, { redirect: 'manual' });
        if (response.status === 101) return response;
        const result = new Response(response.body, response);
        result.headers.set('Cache-Control', 'no-store');
        return result;
      } catch {
        return Response.json({ message: 'A API está temporariamente indisponível.' }, { status: 502 });
      }
    }

    return servePages({ request, env, next: () => env.ASSETS.fetch(request) });
  }
};
