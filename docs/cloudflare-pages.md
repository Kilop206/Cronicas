# Publicação gratuita: Pages, Render e Neon

O frontend Angular fica no Cloudflare Pages. A API ASP.NET Core roda como Web Service Docker no plano Free do Render. O banco PostgreSQL usa o projeto Neon dedicado `cronicas-production` em São Paulo. O navegador chama `/api/*` na origem de Pages; a Pages Function encaminha para a URL HTTPS fixa do Render definida por `API_ORIGIN`, inclusive os cookies de sessão. Credenciais ficam apenas no Render.

Cloudflare Containers exigem Workers Paid e não são usados neste projeto. O Render Free hiberna após 15 minutos sem tráfego; a primeira requisição pode demorar cerca de um minuto. O disco local do serviço é efêmero. Essa configuração serve a um grupo pequeno que aceite partidas ocasionais com início lento, sem garantia de disponibilidade contínua.

## Antes de publicar

1. Instale dependências e valide o frontend: `npm ci` na raiz; `npm ci && npm run build` em `web`.
2. Crie uma conta Render gratuita e conecte o repositório Git. O arquivo `render.yaml` define apenas um Web Service gratuito, sem PostgreSQL temporário do Render e com deploy automático desligado.
3. No primeiro Blueprint do Render, informe `ConnectionStrings__Default` com a conexão TLS do Neon em formato Npgsql e `ALLOWED_ORIGINS` com a origem HTTPS exata do Pages, por exemplo `https://cronicas.pages.dev`. Não registre esses valores no Git. A API aplica migrations no início e `/health` verifica o banco.
4. Configure `API_ORIGIN` nas variáveis de produção do projeto Pages com a origem HTTPS do serviço Render, por exemplo `https://cronicas-api.onrender.com`. Não inclua barra final nem caminho.
5. Autentique o Wrangler (`npx wrangler login`) e publique o frontend com `npm run deploy:pages` somente depois da API responder em `/health`.
6. Verifique cadastro, sessão, campanha e permissões pela URL publicada. `/api/auth/me` sem sessão deve responder 401.

O deploy do Pages deve incluir a pasta `functions`; enviar somente os assets não publica o encaminhamento `/api/*`.

## Serviços opcionais

- Transmissão com câmeras e várias telas: configure no Render `CF_ACCOUNT_ID`, `CF_RTK_APP_ID`, `CF_RTK_API_TOKEN`, `CF_RTK_PRESET` e, conforme os papéis, `CF_RTK_PRESET_GM` e `CF_RTK_PRESET_SPECTATOR` após criar o aplicativo RealtimeKit.
- Recuperação de senha: configure `SMTP_HOST`, `SMTP_PORT`, `SMTP_TLS`, `SMTP_FROM`, `SMTP_USERNAME` e `SMTP_PASSWORD` no Render. O Render Free bloqueia SMTP de saída nas portas 25, 465 e 587; escolha um provedor com API HTTPS ou uma porta permitida antes de ativar recuperação em produção.
- Backup: antes de receber campanhas reais, implemente e teste exportação agendada de PostgreSQL para armazenamento durável separado. O projeto Neon atualmente tem janela de restauração de seis horas e nenhum snapshot programado. O Render Free não fornece disco persistente para os backups.

Nenhuma credencial de Neon, Cloudflare ou RealtimeKit pertence ao frontend, ao Blueprint ou ao repositório.
