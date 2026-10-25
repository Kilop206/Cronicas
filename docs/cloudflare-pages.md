# Implantação em Cloudflare Pages

## Componentes

- `web/wrangler.jsonc`: projeto Pages `cronicas`, assets Angular e binding `API` para o Worker `cronicas-api`.
- `web/functions/api/[[path]].ts`: encaminha chamadas `/api/*` ao Worker. A API conserva o caminho da requisição e a resposta, inclusive `Set-Cookie`.
- `wrangler.jsonc` e `cloudflare/worker.js`: Worker que inicia o Container da API ASP.NET Core.
- PostgreSQL gerenciado externo: estado durável da aplicação. Nunca use o sistema de arquivos do Container para dados persistentes.

## Pré-requisitos

1. Autentique o Wrangler com `npx wrangler login`.
2. Disponibilize PostgreSQL gerenciado com TLS e backups. Defina uma string de conexão Npgsql para `DB_CONNECTION_STRING`.
3. Confirme que a conta Cloudflare permite Containers. O plano e os limites são definidos pela Cloudflare.
4. Para vídeo e múltiplos compartilhamentos de tela, crie um aplicativo RealtimeKit e presets adequados. Sem ele, o restante da aplicação funciona, mas a sala de mídia informa indisponibilidade.
5. Para recuperação de senha, configure um serviço SMTP. Sem SMTP, o endpoint retorna indisponibilidade em vez de fingir envio.

## Secrets do Worker

Configure com `npx wrangler secret put NOME` na raiz, sem gravar valores no Git:

- Obrigatórios: `DB_CONNECTION_STRING`, `PUBLIC_ORIGIN` (por exemplo, `https://cronicas.pages.dev`).
- Mídia: `CF_ACCOUNT_ID`, `CF_RTK_APP_ID`, `CF_RTK_API_TOKEN`, `CF_RTK_PRESET`; opcionalmente `CF_RTK_PRESET_GM`, `CF_RTK_PRESET_SPECTATOR`.
- Email: `SMTP_HOST`, `SMTP_PORT`, `SMTP_TLS`, `SMTP_FROM`, `SMTP_USERNAME`, `SMTP_PASSWORD`.

`PUBLIC_ORIGIN` também é passado à API como origem permitida para requisições de escrita. Ao usar domínio personalizado, inclua sua origem HTTPS na configuração.

## Ordem de publicação

1. Na raiz, `npm ci` e `npm run check:cloudflare`.
2. Configure secrets e publique o backend com `npm run deploy:api`.
3. Em `web`, `npm ci` e `npm run build`.
4. Crie o projeto Pages `cronicas` por Wrangler, caso ainda não exista: `npx wrangler pages project create cronicas --production-branch main`.
5. Na raiz, `npm run deploy:pages`. O comando usa `--cwd web`, onde ficam `wrangler.jsonc` e `functions`.
6. Verifique cadastro, sessão, campanha e controle de acesso na URL publicada. Verifique `/api/auth/me` sem sessão e o funcionamento da Pages Function antes de convidar jogadores.

O deploy por Wrangler inclui a pasta `functions`; arrastar somente a pasta de assets no painel da Cloudflare não publica essa Function. Não publique o frontend antes de o Worker, binding e banco estarem prontos.
