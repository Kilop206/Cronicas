import { DurableObject } from 'cloudflare:workers';

export class ApiContainer extends DurableObject {
  starting;

  async fetch(request) {
    this.starting ??= this.startAndWait().finally(() => { this.starting = undefined; });
    await this.starting;
    const url = new URL(request.url);
    url.protocol = 'http:';
    url.host = 'container';
    const forwarded = new Request(url, request);
    forwarded.headers.delete('host');
    return this.ctx.container.getTcpPort(8080).fetch(forwarded);
  }

  async startAndWait() {
    const container = this.ctx.container;
    if (!this.env.DB_CONNECTION_STRING || !this.env.PUBLIC_ORIGIN) throw new Error('Configure DB_CONNECTION_STRING e PUBLIC_ORIGIN.');
    if (!container.running) {
      container.start({
        image: container.images.base,
        instance: 'lite',
        enableInternet: true,
        env: {
          ASPNETCORE_URLS: 'http://0.0.0.0:8080',
          ASPNETCORE_ENVIRONMENT: 'Production',
          ConnectionStrings__Default: this.env.DB_CONNECTION_STRING,
          ALLOWED_ORIGINS: this.env.PUBLIC_ORIGIN,
          CF_ACCOUNT_ID: this.env.CF_ACCOUNT_ID || '',
          CF_RTK_APP_ID: this.env.CF_RTK_APP_ID || '',
          CF_RTK_API_TOKEN: this.env.CF_RTK_API_TOKEN || '',
          CF_RTK_PRESET: this.env.CF_RTK_PRESET || '',
          CF_RTK_PRESET_GM: this.env.CF_RTK_PRESET_GM || '',
          CF_RTK_PRESET_SPECTATOR: this.env.CF_RTK_PRESET_SPECTATOR || '',
          SMTP_HOST: this.env.SMTP_HOST || '',
          SMTP_PORT: this.env.SMTP_PORT || '',
          SMTP_TLS: this.env.SMTP_TLS || '',
          SMTP_FROM: this.env.SMTP_FROM || '',
          SMTP_USERNAME: this.env.SMTP_USERNAME || '',
          SMTP_PASSWORD: this.env.SMTP_PASSWORD || ''
        }
      });
    }
    await container.setInactivityTimeout(10 * 60 * 1000);
    const port = container.getTcpPort(8080);
    for (let attempt = 0; attempt < 100; attempt++) {
      try {
        const response = await port.fetch('http://container/health', { signal: AbortSignal.timeout(1000) });
        await response.body?.cancel();
        if (response.ok) return;
      } catch { /* container may still be booting */ }
      await scheduler.wait(200);
    }
    throw new Error('API não ficou pronta na porta 8080.');
  }
}

export default {
  fetch(request, env) {
    const path = new URL(request.url).pathname;
    if (path === '/api' || path.startsWith('/api/') || path === '/health') return env.API.getByName('primary').fetch(request);
    return new Response('Not found', { status: 404 });
  }
};
