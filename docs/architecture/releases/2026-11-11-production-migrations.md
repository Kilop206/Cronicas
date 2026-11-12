# Migrations de produção — 11/11/2026

## Destino e autorização

Execução solicitada explicitamente pelo proprietário: “faça a migration”. Destino identificado pela configuração operacional documentada, pelo projeto conectado e pelo histórico real do banco, sem presumir o estado local:

- Neon: projeto `cronicas-production` (`proud-shadow-09067377`), região `aws-sa-east-1`.
- Branch principal: `main` (`br-young-bread-b65xa0ue`); banco `cronicas`.
- API existente: `https://cronicas-api-r4nx.onrender.com`, serviço Render `srv-dbftfiff3r2c73bfrrrg` no workspace já autorizado.
- Antes: 24 migrations; última `20261104120000_NpcThreatSheets`. Outbox, tabela de versões e coluna de vínculo ainda ausentes.

## Recuperação e ensaio

Snapshot anterior à mudança: `snap-purple-moon-b65tum9z`, nome `before-architecture-migrations-20261111`, criado às **11:49:22 UTC**, com expiração em **18/11/2026 às 12:00 UTC**. A existência, a origem e o tamanho completo foram confirmados pela API do provedor. Não foi executada restauração de produção nem um ensaio de restauração desse snapshot; ele não substitui uma política de backups duráveis.

Branch de ensaio derivada de produção: `migration-check-architecture-20261111` (`br-orange-violet-b65g9gf1`), com expiração automática em **12/11/2026 às 12:00 UTC**. Não houve troca de credenciais da aplicação ou do ambiente local. Foram aplicados os mesmos blocos EF usados em produção e verificados os objetos criados e a preservação dos dados. A reexecução dos scripts passou, confirmando idempotência nesse banco.

A primeira tentativa de reexecução abortou no verificador de integridade do invólucro: ele comparava a linha anterior contendo a nova coluna com a linha posterior sem essa coluna. O invólucro foi corrigido para excluir `SystemVersionId` dos dois lados; a transação foi repetida com sucesso antes da aplicação em produção. Os scripts EF não precisaram de alteração.

## Aplicação

Transação de produção iniciada em **11/11/2026 às 11:52:01 UTC**, concluída com sucesso e confirmada por leitura independente às **11:52:30 UTC**:

1. `20261104172257_IntegrationOutbox`.
2. `20261109121911_CharacterSystemVersions`.

Os blocos `DO $EF$` dos dois scripts foram executados em uma única transação pelo conector SQL do Neon, sem os delimitadores externos `START TRANSACTION`/`COMMIT` dos arquivos. O invólucro usou isolamento `REPEATABLE READ`, trava consultiva transacional para esta operação, `lock_timeout = 5s` e `statement_timeout = 60s`. Uma asserção de integridade ocorreu antes do commit; qualquer divergência abortaria toda a transação.

Arquivos revisados e seus SHA-256 (bytes locais utilizados na execução):

| Script | SHA-256 |
| --- | --- |
| `integration-outbox-up.sql` | `C29C80584FF7C90D934BEC80B4B17EAB3AEA72DE8FF6AC550758BDBA4858AF1D` |
| `character-system-versions-up.sql` | `C5D003320EAF8C02C00BFDAC5731EC2F0C1692B56BF9879AD056C68C666832CF` |

## Resultado verificado

- Histórico passou de **24 para 26 migrations**, com os dois identificadores esperados.
- **20 tabelas de negócio** mantiveram suas contagens e impressões digitais agregadas antes/depois, na mesma transação. A comparação normaliza apenas a nova coluna nullable dos personagens. Não foram exportados conteúdos de usuários ou fichas.
- Mantidos **7 usuários, 5 campanhas, 6 vínculos de participantes, 3 personagens, 3 sistemas e 29 revisões de personagens**.
- Coluna `characters.SystemVersionId` nullable; nenhum personagem fixado automaticamente.
- `rpg_system_versions` e `integration_outbox` criadas e vazias ao final da operação.
- Duas FKs validadas com `ON DELETE RESTRICT`, três índices previstos presentes, trigger `immutable_system_version` habilitada.
- `/health`: **HTTP 200**, `Healthy`, antes e depois da mudança.
- `/api/auth/me`, sem sessão: **HTTP 401**, após a mudança.

As 20 tabelas verificadas foram: `campaign_invites`, `campaign_memberships`, `campaigns`, `character_abilities`, `character_favorites`, `character_inventory_items`, `character_revisions`, `character_secrets`, `characters`, `chat_messages`, `dice_rolls`, `encounter_participants`, `encounters`, `narrative_entries`, `npcs`, `rpg_systems`, `users`, `world_article_links`, `world_articles` e `worlds`. Sessões e códigos de recuperação não fizeram parte dessa comparação; os scripts não escrevem nessas tabelas.

## Limites e próximos passos de release

Esta operação preparou o schema; não fez merge, deploy, conversão de fichas ou backfill. A API existente continuou respondendo. Não foram executados testes autenticados de edição em produção nem testes de carga. A trigger foi inspecionada no catálogo, sem criar capturas fictícias em produção para exercitá-la.

Para publicar a refatoração, seguir a ordem e os gates do [procedimento de release](README.md), incluindo a validação da imagem final e da API antes do frontend. As regras de retorno continuam válidas: não executar scripts Down com dados ou eventos e não retornar a binários que ignoram vínculos após fixar fichas. A expiração do snapshot é explícita; manter backups independentes para necessidades de recuperação posteriores.
