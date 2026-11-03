# Limites de requisições e identidade do visitante

O incidente de 03/11/2026 tinha duas causas no código: o limitador usava o IP da conexão com a API (um proxy na hospedagem), e era executado antes de `SessionService.AuthenticateAsync`. Login, cadastro, recuperação de senha, convites e revogação de sessões também dividiam a mesma política de 20 requisições a cada cinco minutos. Isso permitia que atividades de outras pessoas ou ações da própria campanha esgotassem o contador antes da primeira tentativa de login de alguém.

`RequestRateLimits` agora mantém:

| Política | Identidade | Limite |
| --- | --- | --- |
| `auth` | Visitante + operação de autenticação | 20 por 5 minutos, separados entre login, cadastro e cada operação de recuperação |
| `account` | Conta autenticada | 60 por minuto para convites e revogação de outras sessões |
| `rolls` | Conta autenticada | 120 por minuto para as ações que já utilizavam essa política |

`SessionService` autentica antes de `UseRateLimiter`. O identificador é a conta validada pelo servidor, e não o cookie bruto ou um nome enviado pelo cliente. Assim, duas contas no mesmo endereço mantêm contadores distintos, e mudar de rede não renova o orçamento da mesma conta. Rotas de autenticação normalizam caixa e barra final. O limite continua ativo; rejeições retornam HTTP 429, `Retry-After`, uma mensagem com o tempo de espera e `Cache-Control: no-store`.

## Cloudflare → Render

O IP da conexão não é o visitante quando a API está atrás de proxies. O módulo de borda `web/proxy-identity.mjs` lê `CF-Connecting-IP`, fornecido pelo Cloudflare, remove quaisquer cabeçalhos `X-Cronicas-*` do cliente e produz:

- `X-Cronicas-Client-IP`: IP do visitante.
- `X-Cronicas-Proxy-Time`: timestamp Unix em segundos.
- `X-Cronicas-Proxy-Signature`: HMAC-SHA256 hexadecimal do conteúdo UTF-8 `v1\n{timestamp}\n{ip}`.

Pages Functions e o Worker alternativo usam o mesmo módulo. `ClientRequestIdentity` valida a assinatura em tempo constante, exige um IP válido e aceita uma diferença máxima de 90 segundos. Assinaturas ausentes, adulteradas, expiradas ou inválidas não alteram a identidade de rede: o servidor usa o IP da conexão. IPv4 mapeado em IPv6 é normalizado. `X-Forwarded-For` e `CF-Connecting-IP` enviados diretamente à API não são confiados automaticamente.

Configure a mesma chave aleatória de pelo menos 32 caracteres em `PROXY_IDENTITY_KEY` na API Render, nos secrets do projeto Pages `cronicas` e nos secrets do Worker `cronicas-fallback`. A chave nunca pertence ao Angular, aos arquivos de assets, ao Git ou ao valor de uma variável pública. `render.yaml` declara somente `sync: false`; os valores ficam nos provedores. Alterar a chave exige atualizar os três destinos e publicar a API e os proxies com a mesma configuração. O fallback para o IP da conexão preserva a proteção se houver configuração ausente; a separação de visitantes anônimos no site exige que a chave esteja configurada corretamente.

## Verificação

- `dotnet run --project tests/Chronicles.Rules.Tests`: testa assinatura, adulteração, expiração, IPs mapeados, isolamento por visitante/operação/conta e `Retry-After`, com um servidor HTTP real efêmero.
- `node --test scripts/proxy-identity.test.mjs scripts/pages-proxy.test.mjs scripts/spa-routes.test.mjs`: testa os dois proxies, a substituição de cabeçalhos do cliente, metadados de rejeição e deep links.
- `node scripts/rate-limit-integration.mjs`: exige API/banco locais e a mesma chave **de teste** em `PROXY_IDENTITY_KEY`. Cria duas contas, esgota o login de um visitante e confirma a primeira autenticação do outro; esgota convites de uma conta e confirma que outra na mesma rede permanece funcional.
- `node scripts/smoke.mjs` e `node web/rules-integration.mjs`: regressão funcional existente. Não execute o teste de esgotamento junto de outras suítes que dependam do mesmo orçamento anônimo; identidades de teste assinadas ou um processo local reiniciado isolam esse estado.

Os contadores continuam em memória por instância. Um futuro deploy com várias réplicas precisa de armazenamento de limites compartilhado. Esta correção não muda o plano ou a quantidade de instâncias da API.
