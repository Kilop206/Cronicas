interface Context { request: Request; env: { API_ORIGIN: string }; }
export const onRequest = async ({ request, env }: Context): Promise<Response> => {
  if (!env.API_ORIGIN) return new Response('API_ORIGIN não configurada.', { status: 503 });
  const incoming = new URL(request.url);
  const target = new URL(incoming.pathname + incoming.search, env.API_ORIGIN);
  const headers = new Headers(request.headers);
  headers.delete('host');
  headers.set('origin', incoming.origin);
  return fetch(new Request(target, { method: request.method, headers, body: ['GET', 'HEAD'].includes(request.method) ? undefined : request.body, redirect: 'manual' }));
};
