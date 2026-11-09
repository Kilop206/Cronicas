# Migração incremental — situação efetiva

Auditoria inicial: 04/11/2026, base `f3f51ae`. Validação desta fatia: 05/11/2026. Não interpretar a migração da execução de regras como conclusão de todos os contextos da plataforma.

| Marco | Implementado | Trabalho ainda necessário |
| --- | --- | --- |
| 1 Auditoria | Stack, fluxos, banco, auth, realtime, CI e riscos inspecionados; CURRENT_ARCHITECTURE | Atualizar métricas conforme outras fatias mudarem |
| 2 Modularização | Assemblies Domain/Application/Infrastructure; fontes e modelos agrupados; Angular organizado por features/core/routing/layouts | Isolar fisicamente cada bounded context, eliminar navegações/repositórios compartilhados legados |
| 3 Hexagonal | Criação/leitura individual/save e execução/consulta de regras usam portas e casos de uso, estado/validação/política sem frameworks | Migrar lista, duplicação, exclusão, histórico/restauração e CRUD de conta, campanha, convites, inventário, dados e NPCs |
| 4 Plugins | Catálogo versionado, regras compiladas, OP e outros motores existentes adaptados; host de apresentações exclusivas; teste de novo plugin sem alterar switch legado | Empacotamento/assinatura comunitária e sandbox de terceiros não implementados |
| 5 Persistência | EF extraído sem renomear dados; outbox aditivo; transação/revisão/histórico; SQL e rollback testados | Vínculo imutável do schema por ficha, data de criação confiável e revisão completa de template por histórico |
| 6 Comunicação | API v1 de leitura/escrita/catálogo, revisão obrigatória na escrita v1, eventos duráveis, SignalR autorizado e resync com rascunho preservado | Fanout entre réplicas; migração gradual dos demais contratos legados e eventos explícitos em todos os casos de uso |
| 7 Infraestrutura | Docker compatível com projetos extraídos, CI ampliada, logs JSON/correlação, health checks e instrumentos de métricas | Exporter/collector, alertas, retenção do outbox, validação em staging/produção |
| 8 Validação | Testes de domínio, arquitetura, migrations/outbox, HTTP/SignalR e navegador executados; evidências em TESTING | Teste de carga, auditoria completa de segurança e migração de todas as funcionalidades |

## Ordem para as próximas fatias

1. Concluído em 09/11/2026: `CreateCharacter`, `SaveCharacter` e `ReadCharacter` usam portas, validação e projeções de aplicação. POST/GET/PUT legados mantêm formato e rotas; PUT v1 exige revisão. Próxima fatia de personagens: lista, duplicação, exclusão, histórico/restauração e recursos associados ainda legados.
2. Introduzir revisões imutáveis de sistemas e vínculo opcional por personagem. Primeiro permitir leitura com fallback; depois migração assistida e testes de restauração entre versões. Não inferir edições por nome.
3. Extrair Campaign/Identity, mantendo convites, revogação e verificações por recurso transacionais; depois inventário/habilidades/NPCs/dados. Substituir joins internos de outros contextos por portas públicas.
4. Dividir projetos por bounded context somente após remover acoplamento real. Acrescentar testes de fronteira para cada contrato migrado e reduzir a lista de exceções legadas.
5. Criar staging com snapshot anonimizado; revisar migrations e rollback; só então preparar release de produção.

## Release seguro desta fatia

O release inclui uma tabela nova e mudança na política de migrations de startup. **Não publicar apenas o frontend nem reiniciar a nova API contra banco sem migration.** Validar SQL em staging, ter backup restaurável, aplicar o SQL revisado, publicar API e verificar `/health/ready` e contratos; então publicar frontend e verificar assets/rotas.

Produção não foi alterada automaticamente nesta implementação. O SQL de release está concreto e verificável em `releases/`. Rollback para o binário anterior pode manter o outbox aditivo; não executar Down com eventos pendentes. A autorização histórica para deploy não substitui a restrição deste pedido contra mudanças automáticas nos dados de produção.

## Critérios que ainda impedem declarar migração completa

Isolamento total de domínios, controllers sem regra de negócio em toda a API, histórico/vínculo de schema por ficha e operação realtime em múltiplas réplicas permanecem pendentes. Os testes asseguram as fronteiras novas e a compatibilidade verificada; não certificam funcionalidades que não foram migradas.

## Segunda fatia — 09/11/2026

Criação, leitura individual e salvamento de personagem foram extraídos. O adapter revalida papel, revisão e definição de sistema antes de salvar; mantém histórico, condições de encontros e outbox na transação. O teste concorrente encontrou colisão no índice único de revisões antes do UPDATE do personagem: essa colisão específica agora retorna conflito, sem mascarar outros erros de integridade.

`PUT /api/v1/characters/{id}/sheet` usa DTO HTTP próprio com `version` obrigatório; o PUT antigo segue aceitando versão omitida por compatibilidade. Campos omitidos mantêm valores anteriores, inclusive campos de mestre; fórmulas são recalculadas em cópia independente. Leitura/resync não executam nem descrevem regras do RPG. Nenhuma migration nova foi necessária e produção permanece sem alterações.
