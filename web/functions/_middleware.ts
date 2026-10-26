// Only the public landing page is indexable. Account and workspace URLs must
// never become search results, including on Cloudflare preview domains.
export const onRequest: PagesFunction = async context => {
  const url = new URL(context.request.url);
  const path = url.pathname;
  const isPage = path === '/' || path === '/index.html' || !/\.[a-z0-9]{1,8}$/i.test(path);
  if (!isPage || path === '/api' || path.startsWith('/api/')) return context.next();

  // Serve the SPA shell explicitly for deep links. Pages redirects do not run
  // for Function routes, and a catch-all rewrite can loop back to index.html.
  const response = path === '/' ? await context.next() : await context.env.ASSETS.fetch(new URL('/', url));
  if (response.status === 101) return response;
  const isProductionHome = url.hostname === 'cronicas-bsy.pages.dev' && path === '/';
  if (isProductionHome) return response;

  const withIndexingPolicy = new Response(response.body, response);
  withIndexingPolicy.headers.set('X-Robots-Tag', 'noindex, nofollow, noarchive');
  return withIndexingPolicy;
};
