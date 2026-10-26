# Crônicas

Workspace de campanhas de RPG de mesa com contas e controle de sessões, convites, fichas, NPCs, sistemas editáveis, universos, wiki, documentos, sessões, dados, chat e encontros. A sala usa WebRTC direto entre navegadores para câmeras e múltiplas telas; portraits e recursos têm fontes para OBS.

Campanhas novas começam sem sistema pronto. O proprietário pode criar uma ficha livre, cujos campos cada jogador preenche, ou um sistema estruturado. Neste último, o editor permite montar até oito páginas com blocos de identidade, retrato, recursos, campos tipados, texto, inventário, habilidades e histórico. Cada página pode usar grade de 12 colunas ou posicionamento livre em porcentagens, com imagem de fundo HTTPS e proporção própria. A ficha pode usar visual de investigação, fantasia ou arquivo clássico, com cor de destaque configurável. Cada bloco possui estilo e dimensões. Recursos podem usar os valores básicos ou campos numéricos definidos pelo sistema. Também é possível editar as definições de campos e do layout em JSON, validadas pelo servidor sem execução de código. Sistemas antigos com apenas campos continuam legíveis e podem receber um layout ao serem editados. A composição é autoral e não incorpora regras ou artes do [C.R.I.S.](https://crisordemparanormal.com/) ou do [Fichas de Nimb](https://fichasdenimb.com.br/FichasdeNimb).

O produto não inclui tabletop/VTT. A produção usa Cloudflare Pages, API .NET no Render Free e PostgreSQL no Neon. A transmissão ainda precisa de validação com navegadores e OBS reais.

## Desenvolvimento local

Requer Docker. Para executar API e frontend fora dos contêineres, instale .NET SDK 10 e Node compatível com Angular 21.

1. Copie `.env.example` para `.env` e altere `POSTGRES_PASSWORD`.
2. Execute `docker compose up --build`.
3. Acesse `http://localhost:8081`. O Mailpit fica em `http://127.0.0.1:8025`.

O PostgreSQL usa o volume `postgres_data`; a API aplica migrações ao iniciar. `/health` verifica o banco. Para executar sem os contêineres de API/frontend, inicie `docker compose up -d db mailpit`, configure `ConnectionStrings__Default` e `ALLOWED_ORIGINS=http://127.0.0.1:4200`, execute `dotnet run --urls http://127.0.0.1:5099` em `api/Chronicles.Api` e `npm ci && npm start` em `web`.

## Transmissão e OBS

Entre na sala da campanha para ligar câmera, microfone ou compartilhar a tela. A API autoriza o participante e encaminha a sinalização SignalR; o áudio e vídeo trafegam diretamente entre navegadores usando STUN público. Várias pessoas podem compartilhar telas simultaneamente. Espectadores e fontes OBS não publicam mídia pela interface.

Escolha a qualidade da câmera na sala (360p, 480p ou 720p). A tela compartilhada usa até 15 quadros por segundo. Cada fonte OBS se conecta apenas ao dono do personagem selecionado e recebe somente a câmera, poupando conexões e o envio de áudio e tela para cada Browser Source.

O proprietário gera convites individuais na campanha. Na ficha, edite portrait, fundo e recursos. Gere uma URL de portrait para **Browser Source** no OBS Studio (sugestão: 980 × 360); gerar outra revoga a anterior. Trate a URL como segredo de leitura visual.

A ficha também oferece uma URL de câmera + portrait para Browser Source de 1920 × 1080. Faça login na fonte pelo botão **Interagir**. Ela entra na sala sem publicar mídia e seleciona a câmera do dono da ficha. Cada fonte OBS conta como conexão WebRTC adicional. Redes que bloqueiam conexão direta podem exigir TURN, que não está configurado nesta versão gratuita. A reconexão e a apresentação em OBS ainda precisam de teste com mídia real.

## Cloudflare Pages

O Angular é publicado em **Cloudflare Pages**. Uma Pages Function encaminha `/api/*` para a API .NET em um Web Service gratuito do Render; PostgreSQL fica no projeto Neon dedicado. O navegador usa a origem de Pages, inclusive para os cookies de sessão. O serviço gratuito pode hibernar e exige uma rotina de backup externa antes de armazenar campanhas reais. Consulte [o guia de implantação](docs/cloudflare-pages.md).

## Verificação

- `dotnet build api/Chronicles.Api/Chronicles.Api.csproj`
- `npm run lint`, `npx tsc --noEmit -p tsconfig.app.json` e `npm run build` em `web`
- `node scripts/smoke.mjs` com o stack em execução (`SMOKE_BASE_URL` define a origem). O smoke cobre contas, campanhas, convites, permissões, fichas e versões, sistemas, universos, relações, pesquisa, narrativa, dados, chat, encontros e OBS. `SMOKE_MAILPIT_URL` ativa a verificação de recuperação de senha.
- `node scripts/room-smoke.mjs` testa entrada, sinalização, espectador e revogação da sala SignalR com o stack em execução.
- `docker run --rm -v "${PWD}:/workspace" -w /workspace cronicas-api-build sh -c 'dotnet restore tests/Chronicles.Rules.Tests/Chronicles.Rules.Tests.csproj --ignore-failed-sources && dotnet run --project tests/Chronicles.Rules.Tests/Chronicles.Rules.Tests.csproj --no-restore'` executa os testes determinísticos do parser de dados após criar a imagem com `docker build --target build -t cronicas-api-build ./api/Chronicles.Api`.

## Estrutura

- `api/Chronicles.Api`: API, EF Core e migrações.
- `web`: Angular e design system.
- `render.yaml`: configuração da API no Render Free; `web/wrangler.jsonc` e `web/functions`: Pages e encaminhamento da API.
- `docs/architecture.md`: decisões e próximos módulos.

Não inclua `.env` ou credenciais no repositório. Consulte `docs/architecture.md` para limites atuais e próximos passos.
