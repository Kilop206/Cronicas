# Publicação da auditoria de qualidade — 12/11/2026

Publicação solicitada pelo proprietário após a auditoria: “faça os commits e o deploy”. Acesso Render confirmado para o workspace Guilherme Döge. Nenhum plano, domínio, credencial ou limite foi alterado.

## Commits e origem

- `afad3b0`: proteção HTTP, limites de entrada do login, recuperação de senha e testes de segurança/persistência.
- `fbeb7bb`: branding, metadados, privacidade, acessibilidade, rascunhos, navegação e testes frontend.
- `1fd1834`: relatório, README e evidências visuais.
- `81df1fdd0f0b6f62a146fb3280ee71339c4731cc`: revisão publicada. Integra o histórico de `origin/main`, que já continha os merges da refatoração, sem reescrita de histórico ou mudanças adicionais de conteúdo.

Os commits foram enviados atomicamente para `main` e `refactor/modular-rpg-architecture`. Os SQLs e screenshots que já estavam modificados antes da auditoria ficaram fora dos commits. A árvore de código de API/web não tinha mudanças locais ao publicar.

## Destinos e resultado

| Destino | Identificador | Resultado |
| --- | --- | --- |
| API Render `srv-dbftfiff3r2c73bfrrrg` | `dep-dbqroe3tqb8s73brjdsg`, commit `81df1fd` | `live`, concluído às 13:17:58 UTC; `/health/ready` respondeu 200 Healthy |
| Cloudflare Pages `cronicas`, branch de produção `main` | `95849b60.cronicas-bsy.pages.dev` | Upload de assets e Functions concluído; alias principal atualizado |
| Worker `cronicas-fallback` | versão `2488ab0d-f383-47f9-a8ac-2444ae577be3` | Assets e Worker publicados; trigger workers.dev ativo |

Ordem: API compilada e validada antes de publicar os dois frontends. A API estava anteriormente em `f3f51ae`; o esquema necessário já havia sido aplicado em 11/11, conforme o registro de migrations. Nenhuma migration, backfill ou alteração de dados de campanha foi executada nesta publicação. `autoDeployTrigger` do Render continua desligado.

O Pages recebeu o hash explícito da revisão publicada e o build já validado. Como havia arquivos locais não relacionados à publicação, o metadata `commit-dirty` foi marcado como verdadeiro; esses arquivos não fazem parte do diretório de assets nem das Functions. SHA-256 do index do build: `F80C4F3FFFD8151649E613028C26A6F662CE79A4A2165E60F0AA2C0B2A1FDE90`.

## Verificação em produção

- Render `/health/ready`: 200 Healthy; headers CSP, nosniff, no-referrer e noindex presentes.
- Consultas anônimas `/api/auth/me` e `/api/v1/rpg-plugins`: 401, sem `Retry-After`.
- Chrome real nos dois endereços: `/login` respondeu 200, título `Entrar · Crônicas`, formulário novo carregado. Um login com endereço reservado `example.invalid`, sem conta real, respondeu 401 e exibiu “Email ou senha incorretos.”. Sem erros JavaScript não capturados. Nenhuma conta ou campanha fictícia foi criada em produção.
- No host alternativo: home e `/privacy` retornaram 200 com o bundle `main-S3R6KQ4U.js`; URL inexistente retornou 404; áreas não indexáveis mantiveram noindex/no-store.
- As consultas locais Node/curl ao domínio Pages tiveram reset de conexão TLS nesta rede; a verificação no Chrome ao domínio principal funcionou. O reset não foi tratado como falha da aplicação.
- O endpoint de combined status do GitHub retornou lista vazia; não foi declarado CI remoto aprovado. As verificações locais estão no relatório da auditoria.

## Relato de limite ao entrar

Durante a publicação, o proprietário relatou a mensagem de limite ao entrar. Após a API e os frontends novos estarem publicados, o cenário não foi reproduzido nos testes de login anônimo descritos acima: a resposta foi 401, e não 429. A consulta aos logs do Render pelo texto `returned 429` não encontrou entradas na janela consultada; isso, isoladamente, não prova ausência de bloqueio em todas as sessões.

Foi confirmada a presença do secret `PROXY_IDENTITY_KEY` nos dois hosts Cloudflare, sem ler seus valores. Não houve rotação de segredos, remoção de rate limiting ou aumento de quotas para mascarar o relato. A autenticação real do proprietário depende de sua própria sessão e não foi testada com credenciais pessoais. Foi solicitada nova tentativa após recarregar a versão atualizada. Se o relato persistir, coletar host, status e `Retry-After` da requisição específica antes de alterar a política.

## Limites operacionais

As pendências jurídicas, de backup, SMTP/DNS e validações de produção do [relatório de auditoria](../../quality-audit.md) permanecem. A configuração efetiva do serviço Render consultada tinha `healthCheckPath` vazio, apesar do Blueprint declarar `/health`; esta publicação verificou `/health/ready` explicitamente e não alterou essa configuração. Não houve teste autenticado com conta real, envio externo de email ou transmissão WebRTC/OBS real.
