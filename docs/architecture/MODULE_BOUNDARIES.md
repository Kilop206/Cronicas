# Fronteiras e responsabilidades

| Contexto | Código em execução | Contrato/fronteira atual |
| --- | --- | --- |
| Identity & Access | `Features/Identity`, `Services/SessionService`, `Domain/Identity/CharacterAccessPolicy` | Política pura para personagem; sessão/cookie e rotas de conta ainda são adaptadores legados |
| Campaign Management | `Features/Campaigns`, rotas restantes em `Program.cs` | Participação/convites são autoridade do banco; extração de casos de uso pendente |
| Character Management | `Features/Characters`, `Infrastructure/Characters`, modelos EF agrupados | Proprietário, inventário, habilidades e histórico preservados; CRUD legado ainda usa EF |
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

Antes de extrair um contexto em assembly independente, separe suas navegações EF e substitua a ponte legada por APIs públicas. Não mover apenas classes para outro processo mantendo joins remotos síncronos.
