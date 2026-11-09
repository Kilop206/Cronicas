# Entrega incremental — índice de alterações

Base auditada: `f3f51ae`. Implementação separada em commits de backend e frontend na branch `refactor/modular-rpg-architecture`; documentação/evidências acompanham a branch. Os renomes preservam conteúdo de regras, migrations históricas e componentes, em vez de recriar os módulos.

## Backend

- `api/Chronicles.Api/modules/Chronicles.Domain/`: schemas/campos, validação e fórmulas, estado de ficha, política de acesso, parsers de dados, motores OP/D&D/T20/CoC, ameaças e contratos de eventos/plugins. `CharacterSheetState` substitui dependência no DTO aninhado do endpoint.
- `modules/Chronicles.Application/CharacterSheets/`: `ExecuteCharacterRule`, `ReadCharacterRules`, porta `ICharacterRuleStore`, RNG e DTOs de consulta/resposta. `Events/IGameEventHandler` define o consumidor interno.
- `modules/Chronicles.Infrastructure/Characters/`: adapter EF, histórico e projeção de condições em encontros. `Persistence/` reúne modelos por contexto, DbContext/factory, migrations e política de startup sem alteração automática em produção.
- `Infrastructure/Messaging/`: outbox, ponte de transição de eventos e dispatcher com claims/retry. Migration nova: `20261104172257_IntegrationOutbox`.
- `Features/`: endpoints agrupados por contexto; regras passam pelos casos de uso; hub de fichas, handler autorizado, worker e observabilidade. `Program.cs` permanece composition root e ainda contém rotas legadas.
- `.csproj`, Dockerfile e `.dockerignore`: referências de projetos e build compatíveis com o contexto Docker anterior.

## Frontend

- `web/src/app/core/contracts`, `core/models`, `core/api`: DTOs/modelos e adapter HTTP separados; `api.ts` é fachada de compatibilidade.
- `features/sheets/rendering`: registry e host dinâmico com componentes confiáveis; editor não conhece layouts concretos.
- `features/sheets/renderers/*`: OP/D&D/CoC/T20 e componentes próprios preservados em diretórios especializados. Editor universal e componentes compartilhados permanecem em `sheets/editor` e `sheets/components`.
- `features/sheets/data-access/sheet-updates.ts`: conexão por editor, reconexão, assinatura e deduplicação de invalidações.
- `features/characters/studio-pages.ts`: refetch autoritativo, pausa em conflito com rascunho, autosave e prevenção de saída com alterações pendentes.
- `features/rpg-systems/plugins`: contrato/catálogo sincronizado; `routing` e demais features reúnem as páginas anteriores sem mudar suas rotas.

## Verificação e documentação

- `tests/Chronicles.Architecture.Tests` e `Chronicles.Persistence.Tests`; regras existentes adaptadas para importar o domínio independente.
- `scripts/architecture.test.mjs`, `sync-rpg-plugins.mjs`, `sheet-events-smoke.mjs`, `export-rpg-openapi.mjs`; checker de templates adaptado aos novos caminhos.
- `web/architecture-browser.mjs`: dois editores reais, dados persistidos e capturas em quatro resoluções. Ajuste de espera assíncrona em `web/threats-integration.mjs`.
- CI ampliada para fronteiras, catálogo, migrations/outbox e API v1/SignalR. Build Docker e testes Chrome executados localmente.
- `docs/architecture/*`, `docs/plugins/*`, `docs/development/*`, OpenAPI v1, SQL de release e `docs/visual-validation/architecture/` documentam código, operação, extensão, evidências e limites.

Não foram comitadas regenerações aleatórias das capturas antigas de `docs/visual-validation/rules`; elas foram mantidas no disco. Não houve alteração de produção. Para critérios ainda pendentes e próximos passos, consultar MIGRATION_PLAN e MICROSERVICES_ROADMAP.

## Continuação — 09/11/2026

- `Application/CharacterSheets`: `CreateCharacter`, `ReadCharacter`, `SaveCharacter`, contratos de escrita/leitura e projeções de resposta. `CharacterSheetSnapshot` é compartilhado por consultas e comandos; cálculos ocorrem em uma cópia do rascunho.
- `Infrastructure/Characters`: `EfCharacterWriteStore` e `CharacterSheetPersistence` compartilham leitura, locks, mapeamento e classificação de conflitos com o adapter de regras. Inclui colisão específica do índice de histórico em saves simultâneos.
- `Features/CharacterSheets`: adapters finos de criação/leitura/salvamento e DTO `VersionedCharacterInput`; removidos os handlers correspondentes com EF do endpoint antigo. `PluginEndpoints` oferece PUT v1 com revisão obrigatória. Formato e revisão opcional do PUT legado foram mantidos.
- `Program.cs`/`RequestObservability`: DI dos novos casos de uso; entrada inválida mantém status 400 e erro de validação estruturado; correlação também é garantida após o tratamento central de exceções.
- `CharacterWriteTests`, `CharacterWritePersistenceTests`, `scripts/character-writes-smoke.mjs`, teste de fronteira e CI cobrem os novos fluxos. OpenAPI e documentos foram atualizados. Nenhuma migration adicional foi criada.

## Ciclo de vida — 09/11/2026

- `Application/CharacterSheets/{CharacterLifecycleContracts,ReadCharacterLifecycle,DuplicateCharacter,RestoreCharacter}.cs`: projeções, porta e casos de uso para os quatro fluxos existentes. `CharacterSheetProjection` compartilha mapeamento de lista/documento.
- `Infrastructure/Characters/EfCharacterLifecycleStore.cs`: histórico autorizado, reconstrução do arquivo e cópia com inventário/habilidades em transação. `CharacterSheetPersistence` compartilha o mapeamento neutro de estado.
- `Features/CharacterSheets/CharacterLifecycleEndpoints.cs`: mesmos caminhos e formatos HTTP; removidos os quatro handlers e auxiliares sem uso de `Features/Characters/CharacterEndpoints.cs`. DI registrada em `Program.cs`.
- `CharacterLifecycleTests`, `CharacterLifecyclePersistenceTests`, `scripts/character-lifecycle-smoke.mjs`, teste de fronteira Node e CI cobrem acesso, restauração e isolamento/rollback da cópia. Documentos de fronteiras, dados, segurança e testes atualizados. Nenhuma migration ou mudança de layout. Exclusão de personagem não existia e não foi adicionada.

## Inventário e habilidades — 09/11/2026

- `Domain/Characters/CharacterAssetValues.cs`: valores, limites, validação e normalização puros, com parser seguro de dados existente.
- `Application/Characters/`: `CharacterAssetContracts`, `ManageCharacterInventory` e `ManageCharacterAbilities`, duas portas de persistência e respostas/projeções sem framework.
- `Infrastructure/Characters/`: `EfCharacterInventoryStore`, `EfCharacterAbilityStore` e `CharacterAssetPersistence`; autorização no commit, locks e contagens serializadas por personagem para impedir exceder limites com requisições simultâneas.
- `Features/Characters/CharacterInventoryEndpoints`, `CharacterAbilityEndpoints` e `CharacterAssetHttpResult`: adapters HTTP finos, mantendo DTOs/rotas/status; DI em Program.cs.
- `CharacterAssetTests`, `CharacterAssetPersistenceTests`, fronteira Node, `scripts/character-assets-smoke.mjs` e CI; `web/character-assets-integration.mjs` verifica a interface existente em desktop/mobile, conflito, recarga, exclusão e rolagem.
- Documentação de dados/eventos/segurança/fronteiras/testes atualizada. Sem migrations, mudanças de layout ou publicação em produção. Coleções continuam sem histórico/invalidação realtime específicos.
