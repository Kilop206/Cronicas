# Links de convite da campanha — 16/11/2026

O mesmo convite válido pode ser compartilhado com vários participantes. Cada pessoa entra com sua conta e recebe o papel escolhido pelo proprietário ao gerar o link. A validade continua sendo de sete dias.

Aceitar novamente um convite disponível abre a campanha para quem já participa. Isso não altera o papel atual, não duplica a participação e não emite outro evento de entrada. Os bloqueios transacionais continuam serializando adesões, inclusive com tokens distintos para a mesma conta.

Convites expirados, emitidos por quem perdeu a autoridade e links legados já consumidos continuam indisponíveis. Gere um novo link para substituir um convite consumido antes desta atualização. Não há migration, reativação de tokens antigos nem alteração de participações existentes.

Os testes PostgreSQL cobrem contas distintas no mesmo link, repetição concorrente com tokens iguais/diferentes, uma participação/evento por conta, papéis preservados, expiração, links legados, rollback e emissor revogado. Os smokes HTTP verificam o mesmo link entre duas contas e retornos idempotentes. A auditoria de navegador aceita e reabre o convite duas vezes, terminando na tela de convite para preservar a cobertura visual.

Implementação em `985b534e581151f9c55e752ad53342cc5be7b69e`. Build da API sem avisos, build Angular, lint e testes de aplicação/arquitetura passaram localmente. A matriz visual de 356 combinações passou, incluindo aceitar o mesmo convite e voltar à prévia duas vezes nas quatro larguras.

O [CI](https://github.com/Kilop206/Cronicas/actions/runs/43976474134) passou com PostgreSQL real e os smokes HTTP atualizados. Os testes confirmam duas contas no mesmo token, repetição concorrente, uma participação/evento por conta e preservação de papéis. As capturas `shared-campaign-invite-mobile.png` e `shared-campaign-invite-desktop.png`, em `docs/visual-validation/quality/`, mostram a nova orientação.

## Publicação

API publicada no Render pelo deploy `dep-dbtotltckfvc7392nte0`, status `live` em `2026-11-16T23:17:27Z`, commit `985b534e581151f9c55e752ad53342cc5be7b69e`. `/health/ready` retornou 200 antes da publicação do frontend. O workspace confirmado foi Guilherme Döge; configuração e plano do serviço permaneceram os existentes.

Frontend no [Pages](https://cronicas-bsy.pages.dev), deploy `d0c74012.cronicas-bsy.pages.dev`, e no [Worker alternativo](https://cronicas-fallback.guilhermextra118.workers.dev), versão `bbc50433-0fe5-4455-b017-6be3ddf785c9`. Ambos usam a compilação de `985b534`; o marcador dirty refere-se somente à documentação e às capturas locais de testes.

Artefatos: `main-FWWQUD2U.js`, `styles-PVBWTK3H.css` e `chunk-4OPTGDP7.js`. SHA-256 do índice local: `A6D2DF9C315D999FCB866BCC8CC097DA1F2D89D477E9E745DE18A50F86D32B11`. SHA-256 do CSS: `BF774958C9806C10EDD22CCE3B042C74169F4FB698F8662E8AE25CD8242878A4`.

Os arquivos publicados passaram na conferência de versão e hash de CSS. Nos dois hosts, início e login passaram nas quatro larguras, com HTTP 200 e sem erros JavaScript. Também foram conferidos, com os assets reais e respostas de API simuladas somente no navegador, o erro longo de login e a nova prévia de convite: 32 verificações de interface no total. Não foram criadas contas ou campanhas reais. Uma tentativa de login com identidade aleatória `example.invalid` por host retornou 401 JSON e manteve o formulário sem erro de navegação.
