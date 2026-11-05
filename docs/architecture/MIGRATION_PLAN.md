# Migração incremental — situação efetiva

Auditoria inicial: 04/11/2026, base `f3f51ae`. Validação desta fatia: 05/11/2026. Não interpretar a migração da execução de regras como conclusão de todos os contextos da plataforma.

| Marco | Implementado | Trabalho ainda necessário |
| --- | --- | --- |
| 1 Auditoria | Stack, fluxos, banco, auth, realtime, CI e riscos inspecionados; CURRENT_ARCHITECTURE | Atualizar métricas conforme outras fatias mudarem |
| 2 Modularização | Assemblies Domain/Application/Infrastructure; fontes e modelos agrupados; Angular organizado por features/core/routing/layouts | Isolar fisicamente cada bounded context, eliminar navegações/repositórios compartilhados legados |
| 3 Hexagonal | Execução/consulta de regras usam portas e casos de uso, estado/validação/política sem frameworks | Migrar CRUD de personagem, conta, campanha, convites, inventário, dados e NPCs para casos de uso próprios |
| 4 Plugins | Catálogo versionado, regras compiladas, OP e outros motores existentes adaptados; host de apresentações exclusivas; teste de novo plugin sem alterar switch legado | Empacotamento/assinatura comunitária e sandbox de terceiros não implementados |
| 5 Persistência | EF extraído sem renomear dados; outbox aditivo; transação/revisão/histórico; SQL e rollback testados | Vínculo imutável do schema por ficha, data de criação confiável e revisão completa de template por histórico |
| 6 Comunicação | API v1 de leitura/catálogo, eventos v1 duráveis, SignalR autorizado, resync e preservação de rascunho | Fanout entre réplicas; migração gradual de contratos legados e eventos explícitos em todos os casos de uso |
| 7 Infraestrutura | Docker compatível com projetos extraídos, CI ampliada, logs JSON/correlação, health checks e instrumentos de métricas | Exporter/collector, alertas, retenção do outbox, validação em staging/produção |
| 8 Validação | Testes de domínio, arquitetura, migrations/outbox, HTTP/SignalR e navegador executados; evidências em TESTING | Teste de carga, auditoria completa de segurança e migração de todas as funcionalidades |

## Ordem para as próximas fatias

1. Extrair `SaveCharacter` e `CreateCharacter` compartilhando validação, revisão e porta de autorização; manter JSON/rotas atuais. Exigir revisão no contrato v1 de escrita, sem quebrar clientes legados silenciosamente.
2. Introduzir revisões imutáveis de sistemas e vínculo opcional por personagem. Primeiro permitir leitura com fallback; depois migração assistida e testes de restauração entre versões. Não inferir edições por nome.
3. Extrair Campaign/Identity, mantendo convites, revogação e verificações por recurso transacionais; depois inventário/habilidades/NPCs/dados. Substituir joins internos de outros contextos por portas públicas.
4. Dividir projetos por bounded context somente após remover acoplamento real. Acrescentar testes de fronteira para cada contrato migrado e reduzir a lista de exceções legadas.
5. Criar staging com snapshot anonimizado; revisar migrations e rollback; só então preparar release de produção.

## Release seguro desta fatia

O release inclui uma tabela nova e mudança na política de migrations de startup. **Não publicar apenas o frontend nem reiniciar a nova API contra banco sem migration.** Validar SQL em staging, ter backup restaurável, aplicar o SQL revisado, publicar API e verificar `/health/ready` e contratos; então publicar frontend e verificar assets/rotas.

Produção não foi alterada automaticamente nesta implementação. O SQL de release está concreto e verificável em `releases/`. Rollback para o binário anterior pode manter o outbox aditivo; não executar Down com eventos pendentes. A autorização histórica para deploy não substitui a restrição deste pedido contra mudanças automáticas nos dados de produção.

## Critérios que ainda impedem declarar migração completa

Isolamento total de domínios, controllers sem regra de negócio em toda a API, histórico/vínculo de schema por ficha e operação realtime em múltiplas réplicas permanecem pendentes. Os testes asseguram as fronteiras novas e a compatibilidade verificada; não certificam funcionalidades que não foram migradas.
