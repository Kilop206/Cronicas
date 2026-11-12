import { pageMetadata, PUBLIC_ORIGIN } from '../src/app/core/seo/page-metadata.ts';
import { secureResponse } from '../security-headers.mjs';

// Only the landing page is indexable. Legal drafts remain noindex until validated.
export const onRequest: PagesFunction = async context => {
  const url = new URL(context.request.url);
  const path = url.pathname;
  if (path === '/api' || path.startsWith('/api/')) return secureResponse(await context.next(), { privatePage: true });
  if (path !== '/' && path.endsWith('/') && pageMetadata(path.replace(/\/+$/, '')).known && ['GET', 'HEAD'].includes(context.request.method)) {
    url.pathname = path.replace(/\/+$/, '');
    return secureResponse(Response.redirect(url, 301));
  }
  const metadata = pageMetadata(path);
  const isPage = metadata.known || path === '/index.html' || !/\.[a-z0-9]{1,8}$/i.test(path);
  if (!isPage) return secureResponse(await context.next());
  if (path === '/index.html') return secureResponse(Response.redirect(new URL('/', url), 301));
  if (!['GET', 'HEAD'].includes(context.request.method)) return new Response(null, { status: 405, headers: { Allow: 'GET, HEAD' } });

  // Serve the SPA shell explicitly for deep links. Pages redirects do not run
  // for Function routes, and a catch-all rewrite can loop back to index.html.
  const response = path === '/' ? await context.next() : await context.env.ASSETS.fetch(new URL('/', url));
  if (!response.ok) return secureResponse(response, { privatePage: true });
  const isProductionHome = url.origin === PUBLIC_ORIGIN && metadata.public;
  let html = await response.text();
  html = html.replace(/<title>[^<]*<\/title>/, `<title>${metadata.title}</title>`)
    .replace(/<meta name="description" content="[^"]*">/, `<meta name="description" content="${metadata.description}">`)
    .replace(/<meta (property="og:(?:title|description)"|name="twitter:(?:title|description)") content="[^"]*">/g,
      (_tag, attribute: string) => `<meta ${attribute} content="${attribute.includes('title') ? metadata.title : metadata.description}">`);
  if (!metadata.public) html = html.replace(/<link rel="canonical"[^>]*>|<meta property="og:url"[^>]*>|<script type="application\/ld\+json">[\s\S]*?<\/script>/g, '');
  if (!isProductionHome) html = html.replace('</head>', '<meta name="robots" content="noindex, nofollow, noarchive"></head>');
  const headers = new Headers(response.headers);
  headers.delete('content-length'); headers.delete('etag'); headers.delete('content-encoding');
  if (!isProductionHome) headers.set('X-Robots-Tag', 'noindex, nofollow, noarchive');
  return secureResponse(new Response(context.request.method === 'HEAD' ? null : html, {
    status: metadata.known ? (path === '/error' ? 500 : 200) : 404, headers
  }), { privatePage: true });
};
