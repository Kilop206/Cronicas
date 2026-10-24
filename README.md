# Crônicas

Workspace para campanhas de RPG de mesa. A fundação entrega cadastro, login, recuperação de senha por email, sessões persistidas, perfil, cor de destaque pessoal, campanhas básicas e dashboard. Os demais módulos estão identificados como planejados.

## Iniciar com Docker

1. Copie `.env.example` para `.env` e altere `POSTGRES_PASSWORD`.
2. Execute `docker compose up --build`.
3. Acesse `http://localhost:8081`.

O PostgreSQL usa o volume `postgres_data`. A API aplica as migrações automaticamente ao iniciar. O health check está em `http://localhost:8081/health`. Os emails de recuperação locais ficam no Mailpit em `http://127.0.0.1:8025`.

## Desenvolvimento sem Docker

Requer .NET SDK 10, Node 24 (ou versão compatível com Angular 21) e PostgreSQL 17.

1. Configure `ConnectionStrings__Default` com a string PostgreSQL e `ALLOWED_ORIGINS=http://127.0.0.1:4200`. Para recuperação de senha, configure `SMTP_HOST`, `SMTP_PORT`, `SMTP_FROM` e, se necessário, `SMTP_TLS`, `SMTP_USERNAME` e `SMTP_PASSWORD`.
2. Em `api/Chronicles.Api`, rode `dotnet run --urls http://127.0.0.1:5099`.
3. Em `web`, rode `npm ci` e `npm start`.
4. Acesse `http://127.0.0.1:4200`.

Para usar banco e caixa de email do Compose no desenvolvimento, rode `docker compose up -d db mailpit`; eles ficam disponíveis em `127.0.0.1:5433` e `127.0.0.1:8025`.

Para conferir: `dotnet build api/Chronicles.Api/Chronicles.Api.csproj`, `npm run lint` e `npm run build` em `web`, `docker compose config` e `node scripts/smoke.mjs` com o stack em execução. Para incluir recuperação de senha no smoke test, defina `SMOKE_MAILPIT_URL=http://127.0.0.1:8025`.

## Publicação

O frontend está preparado para Cloudflare Pages com raiz `web`, build `npm run build` e diretório `dist/cronicas/browser`. Configure `API_ORIGIN` com a origem HTTPS do backend; a Pages Function encaminha `/api` ao Render. Configure `ALLOWED_ORIGINS` no backend com a origem pública do frontend. O backend usa `api/Chronicles.Api/Dockerfile` e `ConnectionStrings__Default` para PostgreSQL persistente. Em produção, configure um servidor SMTP real com TLS; Mailpit é apenas para desenvolvimento.

Não publique o banco sem backups. Não inclua `.env` em controle de versão.

## Estrutura

- `api/Chronicles.Api`: API, domínio inicial, EF Core e migração.
- `web`: Angular e design system.
- `docs/architecture.md`: decisões, fluxos e fases seguintes.

## Segurança e escopo

As sessões são cookies HttpOnly. As senhas são armazenadas com hash; a recuperação usa códigos de uso único e revoga sessões antigas. As campanhas são isoladas por participação no servidor. Convites, personagens e conteúdo narrativo pertencem às fases seguintes; consulte o plano em `docs/architecture.md`.
