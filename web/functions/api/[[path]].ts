interface Env { API_ORIGIN?: string; }

export const onRequest: PagesFunction<Env> = async context => {
  let origin: URL;
  try {
    origin = new URL(context.env.API_ORIGIN ?? '');
    if (origin.protocol !== 'https:' || origin.pathname !== '/' || origin.search || origin.hash || origin.username || origin.password) {
      throw new Error('Invalid API origin');
    }
  } catch {
    return Response.json({ message: 'A API ainda não foi configurada.' }, { status: 503 });
  }

  const incoming = new URL(context.request.url);
  const target = new URL(incoming.pathname + incoming.search, origin);
  const request = new Request(target, context.request);
  request.headers.delete('host');
  try {
    const response = await fetch(request, { redirect: 'manual' });
    if (response.status === 101) return response;
    const forwarded = new Response(response.body, response);
    forwarded.headers.set('Cache-Control', 'no-store');
    return forwarded;
  } catch {
    return Response.json({ message: 'A API está temporariamente indisponível.' }, { status: 502 });
  }
};
