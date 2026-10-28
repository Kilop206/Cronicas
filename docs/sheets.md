# Fichas por sistema

## Contrato e fluxo

Uma campanha aponta para um `RpgSystem`; seus personagens pertencem à campanha. O sistema guarda o esquema em `SchemaJson`, lido por `SystemDefinition.Read`. O documento contém campos, layout, `templateId` estável, `templateVersion`, `rulesetVersion`, `rendererMode`, revisão e até dez revisões anteriores. Esquemas antigos salvos apenas como array de campos continuam legíveis. O identificador do template é independente do nome editável do sistema.

O frontend escolhe o renderizador em `sheet-renderer-registry.ts` usando **ID + versão + campos obrigatórios**. `rendererMode: "layout"` usa o layout visual mesmo em um sistema associado a um template. Se não existir um renderizador compatível, `sheet-layout.component.ts` apresenta o layout do sistema. `paranormal-sheet.component.ts` e `dnd-sheet.component.ts` são renderizadores de composição própria, selecionados em `studio-pages.ts`. Tormenta20 e Call of Cthulhu 7e continuam no renderizador especializado anterior. A navegação e o cabeçalho do Crônicas são compartilhados; a área principal da ficha usa praticamente toda a largura disponível. O registro não seleciona pela grafia do nome.

`system-templates.ts` contém as definições iniciais **somente para sistemas já presentes no catálogo do projeto**. As fichas usam nomenclatura e campos estruturais consultados em fontes oficiais, sem texto de livros, arte, fontes ou regras presumidas. Fórmulas só são cadastradas quando a regra está identificada. O template não é uma implementação integral de todas as opções de criação de personagem publicadas em cada livro. A versão das regras é guardada para permitir diferenças futuras.

Ordem Paranormal tem o template v3 para novas fichas, com perícias detalhadas, proteção, resistências, ataques, rituais e carga configurável. Fichas que já usam v1 ou v2 continuam resolvidas pelo novo renderizador; o criador do sistema pode associar o v3 e salvar a migração quando desejar. A ficha principal usa uma roda pentagonal de atributos, lista compacta de perícias, controles rápidos de PV/SAN/PE e defesa. D&D 5e usa seis caixas individuais de atributos, colunas de resistências, perícias e combate, além de abas de ações, magias, inventário e características. Os controles alteram o `CharacterInput` usado pelo autosave existente; inventário e habilidades continuam nos endpoints próprios.

## Adicionar um sistema especializado

1. Identifique edição e base de regras disponível. Crie uma definição em `system-templates.ts` com `id` estável, `version`, `rulesetVersion`, campos e layout. Não reutilize um ID para mecânicas incompatíveis; publique uma nova versão.
2. Acrescente o ID, a versão, as chaves exigidas e os recursos próprios em `sheet-renderer-registry.ts`.
3. Crie uma apresentação própria em um componente e selecione-a em `studio-pages.ts`. Reutilize `SystemFieldComponent`, `SheetCollectionFieldComponent`, inventário, habilidades e histórico quando atenderem às mecânicas. Mantenha os cálculos em `SheetFormula` e `sheet-rules.ts` ou em funções de regras específicas, fora do template HTML. O modo de impressão precisa selecionar o mesmo renderizador.
4. Acrescente indicadores do OBS no registro do frontend e na projeção controlada de `/api/overlay/{key}`. Essa rota retorna somente retrato, recursos e indicadores configurados; não exponha `FieldsJson` inteiro por uma chave de OBS.
5. Faça teste de resolução por ID e versão em `scripts/check-sheet-templates.mjs`, teste de regras em `tests/Chronicles.Rules.Tests`, compile API e web e confira ficha em desktop e celular. Para capturas reproduzíveis, inicie `npm start` em `web` e, em outro terminal, execute `npm run visual:capture`. O script usa Playwright, dados de ensaio e respostas de API simuladas; salva PNGs em `docs/visual-validation` nas resoluções 1440×900, 1920×1080, 768×1024 e 390×844. Também testa rolagem e persistência simulada após reload. Não substitui teste ponta a ponta com o banco e serviço reais.

O servidor aceita campos `text`, `longtext`, `number`, `select`, `boolean`, `list` e `table`. Listas e tabelas têm limites de linhas e colunas. Colunas `dice` aceitam expressões estáticas seguras e mostram um botão de rolagem em cada linha; o resultado entra na campanha com a visibilidade selecionada. `editRole: "gm"` torna o campo somente leitura para jogadores; a API verifica a mudança inclusive em exclusões, importações e restauração de versões. A permissão geral de editar a ficha continua sendo validada pela campanha e pelo proprietário. Dados secretos do mestre vivem no endpoint separado de segredos, fora da ficha pública e do OBS.

## Editor universal

O mestre ou criador do sistema edita campos e compõe até oito páginas e cem blocos. Blocos disponíveis: identidade, retrato, campo, recurso, marcador circular, seção, texto, inventário, habilidades e histórico. A ordem pode ser alterada visualmente; para posicionamento livre há coordenadas percentuais e formato de página. Há campos de lista e tabela, fórmulas, rolagens, visibilidade vinculada a booleanos, fonte, densidade, bordas, fundo HTTPS e cor de destaque. `resource` ou `tracker` com `key` numérica e `maxKey` numérica aparece também no OBS; campos com estilo `score` entram como indicadores (até seis).

Fórmulas são analisadas por `SheetFormula`; não executam JavaScript. Expressões de dados são analisadas por `DiceExpression`. Call of Cthulhu usa o endpoint percentual com um dado de unidades e de zero a dois dados de dezenas de bônus ou penalidade, aplicado ao alvo percentual. Rolagens respeitam a visibilidade da mesa ou privada e são gravadas na campanha. O limite da interface para bônus e penalidade evita expressões arbitrárias.

## Versionamento, migração e dados

O editor do sistema também guarda até 12 modelos de NPC simplificado no mesmo esquema versionado. O mestre escolhe um deles ao criar um NPC, preenchendo aparência, personalidade, motivação, relações, localização, vida e visibilidade. Os segredos não fazem parte do modelo compartilhado e são escritos no NPC com a permissão de mestre.

Atualizações do sistema exigem a revisão atual; a API guarda um snapshot anterior e permite restaurar uma revisão. É possível informar renomeações explícitas de chaves. `SheetSchemaMigration` valida cada valor, remapeia as chaves indicadas, recalcula fórmulas e registra as mudanças nos históricos dos personagens. Valores de campos removidos continuam guardados, permitindo recuperação manual futura. O editor exporta/importa a definição JSON; a ficha exporta/importa os seus dados em JSON vinculados ao ID do sistema. Importar dados aciona o salvamento automático e a validação do servidor.

O personagem é salvo com debounce de 900 ms e versão otimista. Um conflito de versão não sobrescreve silenciosamente a edição de outra pessoa: a API responde 409 e o editor informa o erro. Recursos, condições e coleções ficam nos campos persistidos da ficha. O histórico de personagem permite recuperar revisões autorizadas. Teste migrações em um sistema de campanha antes de mudar chaves de campos que já têm dados.

## Limites atuais

- Os dois URLs diretos de screenshots solicitados (C.R.I.S. e D&D Beyond) retornaram erro de conexão no navegador automatizado em 28/10/2026. A comparação de composição foi feita com os resultados públicos indexados dessas imagens e com as fichas oficiais em PDF; não há comparação de pixels com os arquivos originais.
- As capturas em `docs/visual-validation` usam personagens de ensaio carregados por interceptação da API. Elas comprovam a composição renderizada e as interações no navegador, mas não uma sessão autenticada de produção.
- A nova composição profunda cobre Ordem Paranormal e D&D. Tormenta20 e Call of Cthulhu ainda usam o renderizador especializado anterior. As opções completas de criação de personagem, ações oficiais e regras de cada livro não estão embutidas.
- Pathfinder 2e e Vampiro: A Máscara não estavam cadastrados no projeto e não foram cadastrados automaticamente. A arquitetura permite adicionar definições e renderizadores quando a edição e a base de regras forem escolhidas.
- NPCs continuam com a representação simplificada existente; não recebem ainda o renderizador especializado de personagem.
- O editor permite reordenar blocos por controles, mas ainda não oferece arrastar e soltar nem um editor completo de regras condicionais compostas.
- A API conserva até dez snapshots de sistema e o histórico de personagem existente; não há ferramenta automática para resolver conflitos concorrentes de edição.
