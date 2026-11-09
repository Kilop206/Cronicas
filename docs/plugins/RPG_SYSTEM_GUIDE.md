# Como acrescentar um sistema

## Template personalizado sem programação

Use o editor de sistemas existente para definir campos, listas/tabelas, cálculos, páginas e blocos/tema. O backend continua validando estrutura, tipos, limites, visibilidade de edição e fórmulas. Associe o sistema à campanha e crie personagens; os dados pertencem ao sistema registrado pelo usuário. Não reutilize `templateId` de um RPG conhecido para executar regras diferentes; deixe o template livre ou crie um plugin próprio.

## Plugin próprio compilado

1. Defina um ID estável, edição e versão de schema. Declare em `builtin-rpg-plugins.json` quais schemas têm apresentação e quais têm regras. Exemplo conceitual: `meu_rpg`, edição `1e`, schema `1`, ruleset `meu_rpg:1e`, renderer `meu_rpg`. Não registrar versões oficiais que não foram implementadas/verificadas.
2. Defina os `SystemField` e o layout inicial em um template legítimo do produto. Revise fórmulas com `SheetFormula` e valores default. O schema é declarativo e persistido por usuário, não código recebido do navegador.
3. Implemente `IRpgRulesPlugin` em Domain com métodos de descrição, execução e validação de mudança. Use `RuleState`/dados/expressões existentes quando a mecânica combinar; um sistema percentual não precisa passar por D20. Preserve invariantes e limites de custo/recursos no servidor.
4. Registre explicitamente esse executor em `RpgPluginRegistry.BuiltIn()`. O catálogo sozinho não habilita execução. Não modificar o default dos motores existentes para simular o RPG novo. Para outra edição, crie outro ruleset e declare schemas compatíveis.
5. Execute `node scripts/sync-rpg-plugins.mjs` e acrescente testes de versão desconhecida, schema, ações válidas/inválidas e permissões. `CustomPlugin` na suíte de arquitetura mostra uma extensão independente.
6. Crie um componente Angular em `features/sheets/renderers/<sistema>/`. Implemente os inputs/outputs do host e um layout adequado às mecânicas. Registre o componente em `PRESENTATIONS`, acrescente ID/recursos/requisitos de apresentação no renderer registry. Não enviar regras executáveis pelo HTTP nem duplicar persistência no componente.
7. Verifique impressão, OBS, readonly, GM/jogador, mobile e reabertura após autosave. Faça capturas reais e teste com API/PostgreSQL, além de fixtures visuais.

## Evolução de um template existente

Não alterar o significado de uma versão já salva. Acrescentar versão nova, manter leitura da anterior e preparar migração explícita com `SheetSchemaMigration`/renomes/validação. Preservar campos desconhecidos históricos conforme a compatibilidade atual; não apagar inventário, habilidades ou campos antigos apenas para encaixar no modelo.

O editor prepara atualização do sistema com revisão/histórico. Personagens sem vínculo continuam acompanhando a campanha; mestres podem usar Fixar versão atual para capturar o modelo e impedir alterações automáticas nessa ficha. A API exige ID de origem e revisões atuais e mantém a definição em JSONB imutável. Novas edições de template migram automaticamente apenas fichas não fixadas. Para fichas fixadas, Migrar versão da ficha abre a prévia com mapeamentos/ajustes/recibo. O mesmo ID de origem e a mesma edição são obrigatórios; declarar um novo número no manifest não cria um conversor de edição. Cópia, regras e OBS usam a mesma definição efetiva. Regras nunca capturadas não podem ser recuperadas automaticamente. Ver ADR 0005 e DATA_ARCHITECTURE.

## Checklist verificável

- Registry resolve edição/schema certos e recusa desconhecidos/duplicados.
- Executar ação e editar campos diretamente passam pelo validador do plugin.
- Personagem fora da campanha retorna 404; edição sem permissão retorna 403; revisão antiga retorna 409.
- Histórico, recursos, rolagens e eventos confirmam juntos ou são revertidos juntos.
- Renderizador não é importado diretamente no editor de personagem.
- `npm run test:plugins`, `npm run test:architecture`, suites .NET e integração/navegador passam.

Conteúdo protegido, ilustrações, textos de livros e catálogos oficiais não são distribuídos automaticamente por esse contrato.
