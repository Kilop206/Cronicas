# Recuperação da tela de acesso — 14/11/2026

Relato: ao tentar entrar, aparecia o aviso global “Não foi possível abrir a página”, em vez do formulário.

## Reprodução e causa no frontend

O problema foi reproduzido nos dois endereços publicados. A navegação para `/login` dependia de `/api/auth/me`; qualquer erro diferente de 401 escapava do guard como `NavigationError`, impedindo a criação do formulário.

Na verificação real, a consulta retornou 429. Houve respostas `text/plain` e HTML com título `Just a moment...`, sem `X-Correlation-ID` ou `Retry-After`. Também foi observada uma resposta HTML 429 na API direta. Esse formato difere do bloqueio de quota da aplicação, que retorna JSON, correlação e tempo de espera; os dados são compatíveis com uma resposta da camada de proteção anterior à API. Não foi constatado erro de senha nessas consultas anônimas.

## Correção

Commit `b603c2f8a3d7702aa278df4189098f05ab0281e1`:

- A falha da verificação de sessão passa a permitir a abertura do formulário de login/cadastro com aviso específico.
- A consulta inicial das páginas de acesso tem limite de cinco segundos; o cancelamento evita que uma resposta antiga sobrescreva um login posterior.
- Rotas privadas continuam exigindo confirmação de autenticação. Na falha, redirecionam para o login com o destino preservado, sem repetir a consulta em um ciclo.
- Login bem-sucedido remove o aviso. Falhas de envio preservam os campos; não há repetição automática de POSTs.
- Respostas HTML/texto de proteção em 403/429 recebem mensagem de indisponibilidade. O 429 em JSON da aplicação mantém sua mensagem e `Retry-After`.

Limites, autenticação do backend, assinatura do proxy e secrets não foram alterados.

## Testes e publicação

Build e lint passaram. `web/auth-browser.mjs` verifica, com API simulada e bundle real: visitante, sessão existente, challenge 429, quota JSON, erro 503, rede indisponível, resposta lenta, cancelamento concorrente com login, redirecionamento de área privada, destino após entrar e repetição manual após falha. O [CI completo](https://github.com/Kilop206/Cronicas/actions/runs/43661780163) passou, incluindo estes testes de navegador, regras, arquitetura e integração PostgreSQL.

| Destino | Versão |
| --- | --- |
| Cloudflare Pages | `c318dc38.cronicas-bsy.pages.dev`, produção `main`, commit `b603c2f` |
| Worker alternativo | `46618017-105b-40ac-a34a-02cc8a7d04f1` |
| API Render | `dep-dbsgfumk1f9s7397drlg`, commit `b603c2f`, `live` em 15/11 às 01:17:20 UTC |

Os dois sites foram conferidos no navegador com `main-S5MIY7D3.js`. O SHA-256 do `index.html` local é `0C26EDC1EE4D99CCDF2663332AE5DD8DAA0431151C4975219563DC156A2C8A68`.

A primeira verificação pública confirmou o formulário disponível e a ausência do erro global. Duas tentativas únicas com identidades aleatórias inválidas, uma por host, ainda receberam HTML 429 na infraestrutura e exibiram o aviso correto.

Foi feita uma única reimplantação da instância existente da API, sem mudanças de plano, secrets ou limites. Após o status `live`, a verificação foi repetida nos dois sites: consulta anônima de sessão normal, formulário sem aviso de indisponibilidade, tentativa com identidade inválida respondendo **401 `application/problem+json`**, correlação presente e mensagem **“Email ou senha incorretos.”**, sem erros JavaScript. `/health/ready` respondeu **200 `Healthy`**. O 429 observado antes não persistiu nessa verificação final.

Não foram usadas credenciais pessoais nem criadas contas em produção. Os testes de autenticação bem-sucedida usam contas sintéticas no ambiente isolado do CI. A restauração observada após a reimplantação não estabelece, sozinha, a causa do bloqueio temporário da infraestrutura, nem comprova o comportamento depois de um novo período de hibernação do Render Free.

O diagnóstico usa a distinção documentada pelo [Cloudflare para respostas de challenge](https://developers.cloudflare.com/cloudflare-challenges/challenge-types/challenge-pages/detect-response/). Os testes com fixtures não substituem a confirmação do caminho real de autenticação.
