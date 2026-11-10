# Fronteiras e responsabilidades

| Contexto | Código em execução | Contrato/fronteira atual |
| --- | --- | --- |
| Identity & Access | `Features/Identity`, `Services/SessionService`, `Domain/Identity/CharacterAccessPolicy` | Política pura para personagem; sessão/cookie e rotas de conta ainda são adaptadores legados |
| Campaign Management | `Domain/Campaigns`, `Application/Campaigns`, `Infrastructure/Campaigns`, `Features/Campaigns` | CRUD básico/convites/participantes usam portas; associações sistema/universo e dashboard ainda legados |
| Character Management | `Features/Characters`, `Features/CharacterSheets`, `Application/Characters`, `Infrastructure/Characters` | Personagens, histórico, coleções, favoritos, segredos e OBS usam casos de uso; autorização ainda usa joins EF entre contextos no adaptador |
| RPG System Registry | `Domain/RpgSystems`, catálogo JSON, `Features/RpgSystems` | Manifest/edição/schema/ruleset/renderer; contrato REST v1; sistema personalizado permanece de propriedade do criador |
| Character Sheet Engine | `Domain/CharacterSheets`, `Application/CharacterSheets` | `CharacterSheetState`, validação pura, queries/commands e `ICharacterRuleStore` |
| RPG Rules Engine | `Domain/Rules/*`, `IRpgRulesPlugin` | Código confiável compilado; RNG injetado; execução autoritativa na API |
| Dice Engine | `Domain/Dice`, `Features/Dice` | Parsers/rolagem puros; persistência/visibilidade/idempotência HTTP ainda legadas |
| Realtime Collaboration | hubs em `Features/CharacterSheets` e `Features/Realtime`, `Infrastructure/Messaging` | Invalidação por metadados; sessão/participação verificadas novamente na entrega |
| Media Management | `features/media` Angular, WebRTC e URLs HTTPS | Não existe backend de uploads; não foi criado serviço S3 fictício |

Marketplace, notificações externas e plugins comunitários são extensões futuras, não módulos vazios.

## Dependências verificadas

1. Domain: apenas bibliotecas base .NET; sem ASP.NET, EF, pacotes NuGet ou referência a API/Infrastructure.
2. Application: somente Domain; sem dependências de framework/persistência.
3. Infrastructure: Application + Domain, EF/Npgsql e ferramentas EF de design privadas. Sem referência ao assembly da API. Namespaces legados `Chronicles.Api.Data/Models/Migrations` são compatibilidade de persistência, não dependência binária reversa.
4. API: registra implementações de portas e recebe HTTP/WebSocket. O adaptador novo de regras não consulta EF.
5. Angular core: contratos/adaptador HTTP não importam implementação de feature. Renderizador é escolhido no host da feature sheets; editor é independente dos componentes concretos.

`Chronicles.Architecture.Tests` verifica assemblies internos e grafo de projetos. `scripts/architecture.test.mjs` verifica fontes C#, ciclos TS e dependências do core/editor. Os testes **não** afirmam isolamento de todos os endpoints legados; esses acessos são dívida explícita, a ser removida por fatias verticais.

## Regras para novas alterações

Novos casos de uso recebem valores/contratos, não HttpContext ou DbContext. Portas específicas pertencem à Application; implementações EF pertencem à Infrastructure. Outro contexto deve consumir uma API pública/porta, nunca reutilizar um repositório interno. Use transação imediata para invariantes que exigem resposta; eventos são efeitos posteriores, não autorização nem substitutos de transações.

`ICharacterSheetReader` é consumido pelas queries; `ICharacterWriteStore` expõe criação/salvamento e `ICharacterRuleStore` expõe commit de ações. Os adapters HTTP novos não acessam EF. O teste de arquitetura verifica que POST/GET/PUT individuais não voltam ao controller legado. O DTO HTTP `VersionedCharacterInput` declara revisão obrigatória sem alterar o estado neutro utilizado pelos clientes legados.

`ICharacterLifecycleStore` fornece projeções de lista/histórico, carga de revisão com acesso e duplicação. `ReadCharacterLifecycle` calcula permissões de apresentação; `DuplicateCharacter` aplica a política de cópia; `RestoreCharacter` valida o arquivo e usa a porta de escrita existente. `EfCharacterLifecycleStore` implementa consulta/isolamento transacional sem expor entidades EF. A verificação de fronteiras também impede que esses quatro handlers voltem ao endpoint legado. Exclusão de personagem não é um fluxo existente.

`ManageCharacterInventory` e `ManageCharacterAbilities` orquestram os CRUDs através de `ICharacterInventoryStore`/`ICharacterAbilityStore`. `Domain/Characters/CharacterAssetValues` contém valores, limites, normalização e validação pura, incluindo o parser seguro de dados já existente. Os adapters EF compartilham somente o protocolo concreto de autorização/locks/transação (`CharacterAssetPersistence`), mantendo mapeamentos próprios. IDs dos filhos são sempre consultados junto ao personagem e participação. Os DTOs de entrada continuam na API, separados dos valores de domínio. O teste Node impede que persistência/validação retornem aos dois endpoints.

`ManageCharacterFavorites`, `ManageCharacterSecrets` e `ManageCharacterOverlay` dependem de portas específicas, implementadas por `EfCharacterCompanionStore`. A fonte OBS é uma projeção explícita de Application, sem entidades EF/IDs/campos completos. `IOverlayKeyGenerator` é implementado com RNG criptográfico em Infrastructure; o núcleo valida/hashifica a capacidade com bibliotecas base. `CharacterAssetPersistence` reutiliza os locks com exigências distintas de membro/mestre/dono, sem conceder a edição de ficha a espectadores. `CharacterEndpoints.cs` agora somente registra os grupos e mantém o helper de identificação compartilhado; os sete handlers extraídos não acessam EF. A consulta de conta/dashboard e os contextos de campanha/identidade ainda não foram extraídos por completo.

`FrozenSystemTemplate` é um valor puro de captura. `ICharacterSystemStore` expõe leitura do sistema efetivo e commit da fixação; `PinCharacterSystem`/`ReadCharacterSystem` não conhecem EF/HTTP. `EfCharacterSystemStore` implementa captura/deduplicação transacional, e a persistência compartilhada resolve o FK opcional antes do fallback de campanha. Os adapters legados de NPC/encontros consomem essa resolução por personagem; sua extração integral para casos de uso ainda está pendente. O teste de fronteira impede o editor de voltar à consulta do sistema da campanha.

`CharacterSystemMigration` descreve transformações puras; `MigrateCharacterSystem` orquestra acesso, revisão e digest através de `ICharacterSystemMigrationStore`; `EfCharacterSystemMigrationStore` resolve destinos autorizados e implementa transações/JSONB/locks. O Angular envia mapeamentos/ajustes declarativos e nunca um estado calculado confiável. O adapter HTTP só traduz os contratos. Testes verificam essas dependências e o fluxo real.

`ReadCampaigns`/`ManageCampaigns`/`ManageCampaignMembers`/`ManageCampaignInvites` usam `ICampaignStore`, `ICampaignMembershipStore`, `ICampaignInviteStore`, gerador de capacidade e porta de reconciliação realtime. Valores/políticas estão em Domain/Campaigns; adapters EF em Infrastructure/Campaigns. A sala consome a consulta pública de participação, sem acessar repositórios internos. Onze handlers não importam EF/RoomAccessService nem contêm regras. Autenticação/rotas de conta e associações de sistema/universo ainda não foram extraídas; joins de autorização dos outros adapters são dívida explícita.

Antes de extrair um contexto em assembly independente, separe suas navegações EF e substitua a ponte legada por APIs públicas. Não mover apenas classes para outro processo mantendo joins remotos síncronos.
