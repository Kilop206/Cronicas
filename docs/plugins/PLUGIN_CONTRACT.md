# Contrato de plugin de RPG

Fonte do contrato: `modules/Chronicles.Domain/RpgSystems/RpgPluginRegistry.cs`. Catálogo autoritativo: `RpgSystems/builtin-rpg-plugins.json`; cópia do frontend gerada por `node scripts/sync-rpg-plugins.mjs` e verificada no CI.

## Identidade e versões

`RPGSystemManifest`: `id` estável, `name` informativo, `version` da edição/regras e `schemaVersion` mais recente do modelo. `RPGSystemPlugin`: manifest, `rulesetId`, `rendererId`, schemas de apresentação aceitos, schemas executáveis e campos estruturais exigidos. Não confundir versão do contrato HTTP/evento, edição do RPG, versão de schema, revisão do template do usuário e revisão do personagem.

O schema efetivo é `SystemDefinition`, armazenado no registro do sistema da campanha, contendo `SystemField[]`, layout customizado e metadados. O catálogo não distribui textos de livros nem obriga que todos os sistemas tenham os mesmos campos. Manifest e schema formam a definição resolvida; campos particulares continuam JSONB e são validados no servidor.

Resolução de regras exige ID + edição + schema aceito + campos de estado. Versão/edição desconhecida falha fechada. Templates antigos podem ter apresentação sem regras novas. D&D 2024 v1 legado é apenas apresentação; não usa o executor 2014. Sistemas renomeados mantêm o ID estável. Templates sem edição mantêm fallback **somente visual**, sem escolher regras por nome.

## Regras e schema

`IRpgRulesPlugin` implementa:

```csharp
string RulesetId { get; }
RuleView Describe(SystemDefinition system, CharacterSheetState state);
RuleResult Execute(SystemDefinition system, CharacterSheetState state, RuleCommand command, Func<int,int> draw);
string? ValidateStateChange(SystemDefinition system,
    Dictionary<string, JsonElement> before, Dictionary<string, JsonElement> after,
    bool master, bool ruleExecution, bool newCharacter);
```

O plugin recebe estado/valores, nunca DbContext, HttpContext ou credenciais. RNG é injetado pelo caso de uso com implementação criptográfica no servidor. O resultado só se torna confiável após validação de campos/recursos/autorização, conferência da revisão e commit transacional. O método de validação também protege alterações diretas de estado; não delegar invariantes críticas ao componente Angular.

Os quatro motores anteriores usam `CompiledRulesPlugin` como adaptador de compatibilidade. Um RPG novo implementa a interface diretamente; não adiciona um default ao switch de outro RPG. O registry recusa executor ausente e registro duplicado. O teste `CustomPlugin` comprova um campo e ação novos sem modificar o dispatcher legado.

## Apresentação

`SheetPluginHostComponent` registra `Type<SheetPresentation>` de código Angular compilado. Inputs: system, character, draft, mode, isMaster e rulesReady. Outputs: changed, restored e working. A mesma instância é preservada enquanto o tipo não muda, mantendo navegação/foco local. Impressão usa o mesmo host, com mode=print; OBS continua usando projeção de recursos.

Existem quatro layouts realmente diferentes. A seleção só identifica o componente; não transforma uma ficha em formulário genérico. Template livre segue `SheetLayoutComponent`/designer e seus blocos declarativos. Campos e fórmulas editáveis não instalam um componente executável novo.

## Confiança, isolamento e permissões de terceiros

**Suportado hoje:** plugins próprios compilados/revisados junto ao release. Não há endpoint de upload/importação de assemblies, npm, JavaScript ou código de regra. Frontend envia comandos/JSON, nunca funções executáveis.

**Antes de aceitar código comunitário:** pacote assinado/versionado, revisão e allowlist de capacidades; execução de regras em processo/worker isolado sem acesso livre a rede/disco/banco/segredos; quota de CPU/memória/tempo e limite de dados; resultado validado pelo servidor contra revisão e permissões. UI não confiável exigirá iframe/origem isolada, CSP e protocolo mínimo de mensagens. Não usar eval nem carregar módulo remoto no processo atual.

Permissões propostas devem distinguir leitura de estado autorizado, produção de propostas de alteração e solicitação de rolagem ao servidor. Plugins nunca escolhem usuários/campanhas arbitrários, escrevem diretamente em tabelas ou publicam mensagens privadas. Nenhum sandbox de terceiros foi implementado nesta entrega; não anunciar suporte público a código arbitrário.
