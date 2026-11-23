# Gerenciamento dos convites da campanha — 23/11/2026

O proprietário agora vê os convites ativos, com papel, criação e expiração, e pode desativar cada link antes dos sete dias. A lista usa páginas de vinte. Desativar não remove participantes nem altera seus papéis. O link recém-gerado é retirado da tela quando desativado; falhar ao gerar outro não apaga o link anterior ainda válido.

Convites indisponíveis exibem orientação para solicitar outro ao proprietário, inclusive se expirarem ou forem desativados entre a prévia e o aceite. Botões e mensagens têm espaço em telas pequenas. Depois da desativação, o foco volta ao título da lista.

## Contratos e consistência

- `GET /api/campaigns/{id}/invites?page=1`: proprietário, `private, no-store`, somente ID, papel e datas; nenhum token/hash.
- `DELETE /api/campaigns/{id}/invites/{inviteId}`: proprietário, escopo da campanha, idempotente, `204`.
- A criação preserva token/papel/expiração e acrescenta o ID para gerenciar o link.
- Desativar antecipa `ExpiresAt` sob transação. A ordem de locks é convite, participação do proprietário, campanha. A autoridade é conferida novamente após os locks. Aceites já concluídos permanecem; aceites posteriores são negados.
- Não há migration ou alteração em massa de convites/participações. Links continuam reutilizáveis até expirar/desativar; links legados consumidos continuam fechados.

## Validação

Compilação Angular, lint, testes de aplicação/arquitetura .NET, compilação da suíte PostgreSQL e testes Node passaram localmente. A varredura visual executou 384 casos (96 estados em 320/390/768/1440 px), sem overflow, controles sobrepostos ou erros de JavaScript. Após o ajuste final de espaço da mensagem, os 32 casos de convites passaram novamente, incluindo paginação com vinte itens, falhas e convite indisponível antes/depois do aceite.

Os testes novos cobrem autorização, escopo entre campanhas, privacidade da listagem, desativação idempotente, corrida com aceite, rollback e preservação dos participantes. Docker não estava em execução localmente; testes PostgreSQL e HTTP reais foram executados com sucesso no [CI do commit publicado](https://github.com/Kilop206/Cronicas/actions/runs/45279812691), incluindo a varredura completa de 384 casos no build final, migrations/outbox e os smokes existentes. O roteiro manual `web/campaigns-integration.mjs` foi atualizado para o novo comportamento, mas não foi executado localmente nesta entrega.

## Publicação

Código: `2af1ef8722e6c32d6b6aa750e8789cb0d715f06a`. [CI completo aprovado](https://github.com/Kilop206/Cronicas/actions/runs/45279812691).

API no Render, workspace Guilherme Döge, serviço `srv-dbftfiff3r2c73bfrrrg`, deploy `dep-dc2buus9v7es73bch1lg`, `live` em `2026-11-23T22:35:46Z`. O deploy confirmou o commit acima; `/health/ready` respondeu 200 antes de publicar o frontend. Plano e configuração do serviço foram preservados.

Frontend no [Worker existente](https://cronicas-fallback.guilhermextra118.workers.dev), versão `178fc060-4058-4f4b-a9d8-699d7cc1219a`. Artefatos: `main-GZJNELTG.js` e `styles-Y65XQUGB.css`. SHA-256 do índice: `1A0081BF663DC26FE8614D21289B615F127AA4760769B4E6EC56341977197100`; CSS: `650E80AED76E45172887DE45B798AF203D46CABEF59F2ACC45C9DB0BEB3EDE9C`. A versão servida e o hash do CSS conferem com a compilação validada.

O projeto Pages `cronicas` usado no release anterior não foi encontrado na conta autenticada; `wrangler pages project list` retornou vazio, e `cronicas-bsy.pages.dev` não resolveu no DNS. Nenhum projeto substituto foi criado. Este release está disponível pelo Worker; não há publicação confirmada em Pages nesta entrega.

O Worker passou por 24 verificações de interface em 320/390/768/1440 px: início/login reais, mensagens longas, prévia, gerenciamento/desativação e convite indisponível. Os cenários autenticados usaram respostas de API simuladas no navegador com os assets realmente publicados, sem criar contas/campanhas ou revogar convites de produção. Uma tentativa inválida com identidade aleatória `example.invalid` retornou 401 `application/problem+json`, exibiu “Email ou senha incorretos.” e não gerou erro de navegação/JavaScript. Não foi realizado login válido de usuário real.

Capturas do painel publicado com dados sintéticos: [celular](../../visual-validation/quality/campaign-invites-managed-mobile.png) e [desktop](../../visual-validation/quality/campaign-invites-managed-desktop.png).
