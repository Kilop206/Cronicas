# Publicação de Feiticeiros & Maldições — 14/11/2026

Solicitação do proprietário: continuar a implementação e fazer o deploy. O workspace Render **Guilherme Döge** foi confirmado nesta sessão. Foram utilizados os serviços existentes, mantendo planos, domínios e secrets.

## Conteúdo

- `4241204a7ecd375fb8007ab6ac93cbbc747b3b4a`: ficha e plugin independentes de F&M 2.5.2; criação, recursos, condições, combate, alma, técnicas, rituais, domínios, Raio Negro e os limites descritos no [relatório de cobertura](../../feiticeiros-maldicoes.md).
- A continuação acrescentou Empolgação/manobras básicas, Corpo Treinado/Superior, Preparo/artes básicas, Ataque Furtivo contextual, canalização por reação, sugestão da Máxima por acesso e inventário na impressão.
- `eb1121aea309bed4bf660a7d434912adb94f469f`: correção de cache identificada pelo CI. `expires -1` do Nginx passou a valer só para assets/HTML; a proteção HTTP da API preserva `private` e continua impondo `no-store`. Inclui regressão HTTP e teste F&M com API/PostgreSQL isolados no CI.

A cobertura **não é integral**. O índice de nomes não executa todos os efeitos; o motor completo do Grimório, estilos/posturas, núcleos mutantes e diversas habilidades individuais continuam pendentes. Não houve distribuição dos PDFs ou das artes originais.

## Publicação

O código foi enviado para `origin/main` sem reescrever o histórico.

| Destino | Identificador | Verificação |
| --- | --- | --- |
| Render `cronicas-api` | `srv-dbftfiff3r2c73bfrrrg`; publicação final `dep-dbsfmek9v7es73cj9udg` de `eb1121a` | `live` em 15/11 às 00:23:05 UTC; `/health/ready` 200 `Healthy` |
| Cloudflare Pages `cronicas` | `49c456bf.cronicas-bsy.pages.dev`; branch `main`, commit `4241204` | Assets e Functions publicados; domínio principal confirmado no navegador |
| Worker `cronicas-fallback` | `5f19a88a-9170-49b8-ad7e-55ef0de35a7c` | Assets publicados e trigger workers.dev ativo |

O primeiro deploy da API foi `dep-dbsfgirl550s738ap7l0`, de `4241204`; o deploy final acima incorpora a correção de cache após o CI aprovado. O build publicado serve `main-X74Z3QSH.js` e `chunk-GBUHEJXK.js`. SHA-256 do `index.html` local: `C7E2F85C20EE3F244A606F6EBCEA8E30C1802EC6859A41686F5045DBCDA29BDC`. A correção posterior altera a API e o Nginx de integração, sem mudar os assets publicados no Cloudflare.

## Evidências

- Build Angular e lint; build Release da API sem avisos; regressões dos cinco sistemas, testes de arquitetura e validação cruzada do schema real do frontend.
- Browser fixtures em 1440, 1920, 768 e 390 px: navegação, edição/autosave/reabertura simulados, contexto de ataque/arte/ritual, recusa de comando, leitura, dados sugeridos da Máxima e inventário na impressão.
- Navegador real nos dois domínios: `/login` 200, título correto, bundles publicados correspondentes ao build validado e nenhum `pageerror` observado. `/api/auth/me` e catálogo autenticado retornaram 401 anonimamente, sem `Retry-After`.
- `/health/ready` 200 `Healthy` após a publicação. Consulta de logs `app` com severidade `error`, de 00:10:52 a 00:12:45 UTC, sem entradas; isso não prova ausência de erros fora dessa janela.
- A verificação pública foi repetida após o deploy final, com os dois bundles e endpoints respondendo conforme esperado. A consulta de erros de 00:23:05 a 00:23:56 UTC também não retornou entradas.

O primeiro [CI](https://github.com/Kilop206/Cronicas/actions/runs/43656775174) passou builds, regras, arquitetura e migrations/outbox, mas parou no contrato de cache do smoke de escrita. A causa foi corrigida em `eb1121a`; o [CI de confirmação](https://github.com/Kilop206/Cronicas/actions/runs/43657464322) concluiu com **sucesso**, incluindo migrations/outbox, todos os smokes de integração e o novo teste F&M HTTP/PostgreSQL. Este último confere schema real, criação de sistema/campanha/ficha, recursos iniciais, arte de combate, ledger persistido, recusa de adulteração e conflito atômico entre revisões concorrentes.

## Limites da verificação

Não foram usadas credenciais pessoais, criadas contas fictícias ou modificadas campanhas em produção. Os testes autenticados com dados sintéticos pertencem ao ambiente local isolado do CI. Não há migration SQL nova para F&M; nenhum backfill ou alteração de dados de campanha foi executado nesta publicação. O Render mantém deploy automático desligado.

Os screenshots são evidências de fixtures, não da conta de produção. A verificação pública comprova disponibilidade e entrega do build; não certifica todas as combinações das regras do livro.
