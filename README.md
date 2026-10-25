# Crônicas

Workspace de campanhas de RPG de mesa. O fluxo local já inclui contas, campanhas, convites e papéis, fichas geradas por sistemas editáveis, inventário persistido, histórico de alterações, notas secretas do mestre e favoritos, universos, enciclopédia com relações e backlinks, documentos e sessões com visibilidade, agenda das próximas sessões, pesquisa global, dados persistidos, chat e encontros. A sala de transmissão usa Cloudflare RealtimeKit quando configurada; portrait, recursos e câmera possuem fontes próprias para OBS.

O produto não inclui módulo de tabletop/VTT. A mesa visual é formada por compartilhamentos de tela, câmeras e fontes para OBS. A implantação de produção ainda depende de PostgreSQL gerenciado, configuração de RealtimeKit e validação com mídia real.

## Desenvolvimento local

Requer Docker, .NET SDK 10 e Node compatível com Angular 21.

1. Copie `.env.example` para `.env` e altere `POSTGRES_PASSWORD`.
2. Execute `docker compose up --build`.
3. Acesse `http://localhost:8081`. O Mailpit fica em `http://127.0.0.1:8025`.

O PostgreSQL usa o volume `postgres_data`; a API aplica migrações ao iniciar. `/health` verifica o banco. Para executar sem os contêineres de API/frontend, inicie `docker compose up -d db mailpit`, configure `ConnectionStrings__Default` e `ALLOWED_ORIGINS=http://127.0.0.1:4200`, execute `dotnet run --urls http://127.0.0.1:5099` em `api/Chronicles.Api` e `npm ci && npm start` em `web`.

## Transmissão e OBS

Configure no backend `CF_ACCOUNT_ID`, `CF_RTK_APP_ID`, `CF_RTK_API_TOKEN` e `CF_RTK_PRESET`. Crie o app e um preset RealtimeKit que permita câmera, microfone e compartilhamento de tela. Configure `max_screenshare_count` maior que 1 para participantes compartilharem telas ao mesmo tempo. `CF_RTK_PRESET_GM` é opcional para proprietários e mestres; `CF_RTK_PRESET_SPECTATOR` é necessário para espectadores e deve limitar a publicação de mídia. A API cria uma reunião quando o primeiro membro entra na sala. Sem essas variáveis, a sala informa que a mídia ainda não foi configurada.

O proprietário gera um link individual na página da campanha e escolhe o papel do participante. O convite expira em sete dias e é consumido no aceite; quem recebe pode entrar ou criar conta antes de aceitar. Na campanha, crie um personagem, edite portrait, fundo e recursos; a ficha salva automaticamente e mantém versões anteriores. Gere uma URL de OBS e adicione como **Browser Source** no OBS Studio; sugestão de tamanho: 980 × 360. A fonte consulta os recursos a cada três segundos. Gerar outra URL revoga a anterior. Trate a URL como segredo de leitura da ficha visual e não a publique.

A ficha também oferece uma URL de câmera + portrait, recomendada como Browser Source de 1920 × 1080. Essa fonte exige login no OBS pelo botão **Interagir** e mostra a câmera do dono do personagem quando ele estiver na sala com vídeo ligado. O OBS entra como outra conexão sem publicar câmera nem microfone. O comportamento com vídeo real ainda depende de um teste com RealtimeKit configurado e duas sessões de navegador.

## Cloudflare Pages

O Angular é publicado em **Cloudflare Pages**. Uma Pages Function encaminha `/api/*` para a API .NET em um Web Service gratuito do Render; PostgreSQL fica no projeto Neon dedicado. O navegador usa a origem de Pages, inclusive para os cookies de sessão. O serviço gratuito pode hibernar e exige uma rotina de backup externa antes de armazenar campanhas reais. Consulte [o guia de implantação](docs/cloudflare-pages.md).

O frontend inclui SDK e UI Kit RealtimeKit em versões fixadas e carrega a sala sob demanda. O teste com mídia real requer conta Cloudflare, aplicativo RealtimeKit e participantes simultâneos.

## Verificação

- `dotnet build api/Chronicles.Api/Chronicles.Api.csproj`
- `npm run lint`, `npx tsc --noEmit -p tsconfig.app.json` e `npm run build` em `web`
- `node scripts/smoke.mjs` com o stack em execução (`SMOKE_BASE_URL` define a origem). O smoke cobre contas, campanhas, convites, permissões, fichas e versões, sistemas, universos, relações, pesquisa, narrativa, dados, chat, encontros e OBS. `SMOKE_MAILPIT_URL` ativa a verificação de recuperação de senha.
- `docker run --rm -v "${PWD}:/workspace" -w /workspace cronicas-api-build sh -c 'dotnet restore tests/Chronicles.Rules.Tests/Chronicles.Rules.Tests.csproj --ignore-failed-sources && dotnet run --project tests/Chronicles.Rules.Tests/Chronicles.Rules.Tests.csproj --no-restore'` executa os testes determinísticos do parser de dados após criar a imagem com `docker build --target build -t cronicas-api-build ./api/Chronicles.Api`.

## Estrutura

- `api/Chronicles.Api`: API, EF Core e migrações.
- `web`: Angular e design system.
- `render.yaml`: configuração da API no Render Free; `web/wrangler.jsonc` e `web/functions`: Pages e encaminhamento da API.
- `docs/architecture.md`: decisões e próximos módulos.

Não inclua `.env` ou credenciais no repositório. Consulte `docs/architecture.md` para limites atuais e próximos passos.
