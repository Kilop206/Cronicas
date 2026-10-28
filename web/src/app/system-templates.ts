import type { SheetBlock, SheetLayout, SheetPage, SystemField } from './api';

export interface SheetTemplate {
  id: string;
  version: number;
  rulesetVersion: string | null;
  name: string;
  summary: string;
  sourceUrl: string;
  fields: SystemField[];
  layout: SheetLayout;
}

const numberField = (key: string, label: string, section: string, initial = 0, rollExpression: string | null = null, formula: string | null = null, span = 3): SystemField =>
  ({ key, label, section, type: 'number', min: null, max: null, options: null, span, defaultValue: formula ? null : initial, formula, rollExpression });
const textField = (key: string, label: string, section: string, span = 6): SystemField =>
  ({ key, label, section, type: 'text', min: null, max: null, options: null, span });
const longField = (key: string, label: string, section: string): SystemField =>
  ({ key, label, section, type: 'longtext', min: null, max: null, options: null, span: 12 });
const listField = (key: string, label: string, section: string): SystemField =>
  ({ key, label, section, type: 'list', min: null, max: null, options: null, span: 12 });
const tableField = (key: string, label: string, section: string, columns: [string, string, 'text' | 'number' | 'boolean' | 'dice'][]): SystemField =>
  ({ key, label, section, type: 'table', min: null, max: null, options: null, span: 12,
    columns: columns.map(([columnKey, columnLabel, type]) => ({ key: columnKey, label: columnLabel, type })) });

const block = (id: string, kind: SheetBlock['kind'], title: string, span: number, key: string | null = null, style: SheetBlock['style'] = 'panel'): SheetBlock =>
  ({ id, kind, title, span, key, text: null, style });
const resource = (key: 'health' | 'mana' | 'sanity', title: string, span: number) => block(`recurso_${key}`, 'resource', title, span, key, 'accent');
const identity = (span = 8) => block('identidade', 'identity', 'Personagem', span, null, 'plain');
const portrait = (span = 4) => block('retrato', 'portrait', 'Retrato', span);
const inventory = () => block('colecao_inventario', 'inventory', 'Inventário', 12, null, 'plain');
const abilities = () => block('colecao_habilidades', 'abilities', 'Habilidades e poderes', 12, null, 'plain');
const history = () => block('colecao_historico', 'history', 'Histórico do personagem', 12, null, 'plain');
const highlight = (key: string, span = 3) => block(`campo_${key}`, 'field', '', span, key, 'accent');
const note = (id: string, title: string, text: string): SheetBlock => ({ ...block(id, 'note', title, 12, null, 'plain'), text });
type PageSpec = { id: string; title: string; items: (string | SheetBlock)[] };
type FieldPresentation = Record<string, { style: SheetBlock['style']; span: number }>;

function makeLayout(fields: SystemField[], specs: PageSpec[], skin: SheetLayout['skin'], accentColor: string, headerLabel: string, presentation: FieldPresentation): SheetLayout {
  const byKey = new Map(fields.map(field => [field.key, field]));
  const pages: SheetPage[] = specs.map(spec => ({ id: spec.id, title: spec.title, blocks: spec.items.map(item => {
    if (typeof item !== 'string') return item;
    const field = byKey.get(item);
    if (!field) throw new Error(`Campo ${item} não existe no modelo`);
    const display = presentation[field.section];
    return block(`campo_${item}`, 'field', field.label, display?.span || field.span || 6, item, display?.style || 'panel');
  }) }));
  return { accentColor, skin, headerLabel, pages };
}

const sectionKeys = (fields: SystemField[], section: string) => fields.filter(field => field.section === section).map(field => field.key);

const orderSkills: [string, string, string][] = [
  ['acrobacia', 'Acrobacia', 'agilidade'], ['adestramento', 'Adestramento', 'presenca'], ['artes', 'Artes', 'presenca'],
  ['atletismo', 'Atletismo', 'forca'], ['ciencias', 'Ciências', 'intelecto'], ['crime', 'Crime', 'agilidade'],
  ['atualidades', 'Atualidades', 'intelecto'], ['pilotagem', 'Pilotagem', 'agilidade'],
  ['profissao_1', 'Profissão I', 'intelecto'], ['profissao_2', 'Profissão II', 'intelecto'],
  ['diplomacia', 'Diplomacia', 'presenca'], ['enganacao', 'Enganação', 'presenca'], ['fortitude', 'Fortitude', 'vigor'],
  ['furtividade', 'Furtividade', 'agilidade'], ['iniciativa', 'Iniciativa', 'agilidade'], ['intimidacao', 'Intimidação', 'presenca'],
  ['intuicao', 'Intuição', 'presenca'], ['investigacao', 'Investigação', 'intelecto'], ['luta', 'Luta', 'forca'],
  ['medicina', 'Medicina', 'intelecto'], ['ocultismo', 'Ocultismo', 'intelecto'], ['percepcao', 'Percepção', 'presenca'],
  ['pontaria', 'Pontaria', 'agilidade'], ['reflexos', 'Reflexos', 'agilidade'], ['religiao', 'Religião', 'presenca'],
  ['sobrevivencia', 'Sobrevivência', 'intelecto'], ['tatica', 'Tática', 'intelecto'], ['tecnologia', 'Tecnologia', 'intelecto'],
  ['vontade', 'Vontade', 'presenca']
];
const orderFields: SystemField[] = [
  textField('aparencia', 'Aparência', 'Identidade'), textField('afinidade', 'Afinidade', 'Identidade'),
  longField('vinculos', 'Vínculos e histórico', 'Identidade'),
  listField('condicoes', 'Condições e estados', 'Combate'),
  listField('proficiencias', 'Proficiências', 'Combate'),
  tableField('ataques', 'Ataques', 'Combate', [['nome', 'Ataque', 'text'], ['teste', 'Teste', 'dice'], ['dano', 'Dano', 'text'], ['especial', 'Especial', 'text']]),
  tableField('rituais', 'Rituais', 'Paranormal', [['nome', 'Ritual', 'text'], ['elemento', 'Elemento', 'text'], ['custo', 'Custo', 'text'], ['efeito', 'Efeito', 'text']]),
  textField('protecao', 'Proteção', 'Combate'), longField('resistencias', 'Resistências', 'Combate'),
  numberField('carga_atual', 'Espaços usados', 'Inventário'), numberField('carga_limite', 'Limite de carga', 'Inventário'),
  numberField('iniciativa', 'Iniciativa', 'Combate'),
  textField('origem', 'Origem', 'Identidade'), textField('classe', 'Classe', 'Identidade'), textField('trilha', 'Trilha', 'Identidade'),
  numberField('nex', 'NEX (%)', 'Progressão'), numberField('deslocamento', 'Deslocamento', 'Combate'),
  numberField('equip_defesa', 'Bônus do equipamento', 'Combate'), numberField('outros_defesa', 'Outros bônus de defesa', 'Combate'),
  numberField('defesa', 'Defesa', 'Combate', 0, null, '10 + agilidade + equip_defesa + outros_defesa'),
  numberField('bloqueio', 'Bloqueio', 'Combate'), numberField('esquiva', 'Esquiva', 'Combate'),
  ...([['agilidade', 'Agilidade'], ['intelecto', 'Intelecto'], ['vigor', 'Vigor'], ['presenca', 'Presença'], ['forca', 'Força']] as const)
    .map(([key, label]) => numberField(key, label, 'Atributos', 1, `{${key}}d20kh1`)),
  ...orderSkills.map(([key, label, attribute]) => numberField(`per_${key}`, label, 'Perícias', 0, `{${attribute}}d20kh1+{per_${key}}`)),
  { ...tableField('pericias_detalhes', 'Treino e outros modificadores', 'Perícias', [['chave', 'Perícia', 'text'], ['treino', 'Treino', 'number'], ['outros', 'Outros', 'number']]),
    defaultValue: orderSkills.map(([key]) => ({ chave: `per_${key}`, treino: 0, outros: 0 })) }
];

const dndAbilities: [string, string][] = [['forca', 'Força'], ['destreza', 'Destreza'], ['constituicao', 'Constituição'], ['inteligencia', 'Inteligência'], ['sabedoria', 'Sabedoria'], ['carisma', 'Carisma']];
const dndSkills: [string, string, string][] = [
  ['acrobacia', 'Acrobacia', 'destreza'], ['adestrar_animais', 'Adestrar Animais', 'sabedoria'], ['arcanismo', 'Arcanismo', 'inteligencia'],
  ['atletismo', 'Atletismo', 'forca'], ['atuacao', 'Atuação', 'carisma'], ['enganacao', 'Enganação', 'carisma'],
  ['furtividade', 'Furtividade', 'destreza'], ['historia', 'História', 'inteligencia'], ['intuicao', 'Intuição', 'sabedoria'],
  ['intimidacao', 'Intimidação', 'carisma'], ['investigacao', 'Investigação', 'inteligencia'], ['medicina', 'Medicina', 'sabedoria'],
  ['natureza', 'Natureza', 'inteligencia'], ['percepcao', 'Percepção', 'sabedoria'], ['persuasao', 'Persuasão', 'carisma'],
  ['prestidigitacao', 'Prestidigitação', 'destreza'], ['religiao', 'Religião', 'inteligencia'], ['sobrevivencia', 'Sobrevivência', 'sabedoria']
];
const dndFields: SystemField[] = [
  textField('alinhamento', 'Alinhamento', 'Identidade'),
  listField('condicoes', 'Conditions', 'Combate'),
  listField('magias_preparadas', 'Magias preparadas ou conhecidas', 'Magias'),
  textField('classe', 'Classe', 'Identidade'), textField('subclasse', 'Subclasse', 'Identidade'), textField('especie', 'Espécie', 'Identidade'), textField('antecedente', 'Antecedente', 'Identidade'),
  { ...numberField('nivel', 'Nível', 'Progressão', 1), min: 1, max: 20 }, numberField('experiencia', 'Experiência', 'Progressão'),
  numberField('proficiencia', 'Bônus de proficiência', 'Progressão', 0, null, '2 + floordiv(nivel - 1, 4)'),
  numberField('classe_armadura', 'Classe de armadura', 'Combate', 10), numberField('iniciativa', 'Iniciativa', 'Combate', 0, '1d20+{iniciativa}'),
  numberField('deslocamento', 'Deslocamento', 'Combate'), numberField('pontos_temporarios', 'Pontos de vida temporários', 'Combate'),
  ...dndAbilities.flatMap(([key, label]) => [numberField(key, label, 'Atributos', 10), numberField(`mod_${key}`, `Mod. ${label}`, 'Modificadores', 0, `1d20+{mod_${key}}`, `floordiv(${key} - 10, 2)`)]),
  ...dndAbilities.flatMap(([key, label]) => [
    { ...numberField(`treino_res_${key}`, `Treino: ${label}`, 'Resistências'), min: 0, max: 2 },
    numberField(`res_${key}`, `Resistência: ${label}`, 'Resistências', 0, `1d20+{res_${key}}`, `mod_${key} + proficiencia * treino_res_${key}`)
  ]),
  ...dndSkills.flatMap(([key, label, attribute]) => [
    { ...numberField(`treino_per_${key}`, `Treino: ${label}`, 'Perícias'), min: 0, max: 2 },
    numberField(`per_${key}`, label, 'Perícias', 0, `1d20+{per_${key}}`, `mod_${attribute} + proficiencia * treino_per_${key}`)
  ]),
  textField('dados_vida', 'Hit Dice', 'Combate'),
  { ...numberField('salvaguardas_sucesso', 'Death Saves: sucessos', 'Combate'), min: 0, max: 3 },
  { ...numberField('salvaguardas_falha', 'Death Saves: falhas', 'Combate'), min: 0, max: 3 },
  textField('atributo_conjuracao', 'Spellcasting Ability', 'Magias'),
  numberField('cd_magias', 'Spell Save DC', 'Magias'),
  numberField('ataque_magico', 'Spell Attack Bonus', 'Magias'),
  ...Array.from({ length: 9 }, (_, index) => numberField(`espacos_magia_${index + 1}`, `Spell Slots - nível ${index + 1}`, 'Magias')),
  textField('moedas', 'Moedas', 'Equipamento')
];

const t20Skills: [string, string][] = [
  ['acrobacia', 'Acrobacia'], ['adestramento', 'Adestramento'], ['atletismo', 'Atletismo'], ['atuacao', 'Atuação'],
  ['cavalgar', 'Cavalgar'], ['conhecimento', 'Conhecimento'], ['cura', 'Cura'], ['diplomacia', 'Diplomacia'],
  ['enganacao', 'Enganação'], ['fortitude', 'Fortitude'], ['furtividade', 'Furtividade'], ['guerra', 'Guerra'],
  ['iniciativa', 'Iniciativa'], ['intimidacao', 'Intimidação'], ['intuicao', 'Intuição'], ['investigacao', 'Investigação'],
  ['jogatina', 'Jogatina'], ['ladinagem', 'Ladinagem'], ['luta', 'Luta'], ['misticismo', 'Misticismo'],
  ['nobreza', 'Nobreza'], ['oficio', 'Ofício'], ['percepcao', 'Percepção'], ['pontaria', 'Pontaria'],
  ['reflexos', 'Reflexos'], ['religiao', 'Religião'], ['sobrevivencia', 'Sobrevivência'], ['vontade', 'Vontade']
];
const t20Fields: SystemField[] = [
  listField('condicoes', 'Condições', 'Combate'),
  tableField('ataques', 'Ataques', 'Combate', [['nome', 'Ataque', 'text'], ['bonus', 'Bônus', 'number'], ['dano', 'Dano', 'text']]),
  textField('raca', 'Raça', 'Identidade'), textField('origem', 'Origem', 'Identidade'), textField('classe', 'Classe', 'Identidade'), textField('divindade', 'Divindade', 'Identidade'),
  numberField('nivel', 'Nível', 'Progressão', 1), numberField('experiencia', 'Experiência', 'Progressão'),
  numberField('defesa', 'Defesa', 'Combate', 10), numberField('deslocamento', 'Deslocamento', 'Combate'),
  ...([['forca', 'Força'], ['destreza', 'Destreza'], ['constituicao', 'Constituição'], ['inteligencia', 'Inteligência'], ['sabedoria', 'Sabedoria'], ['carisma', 'Carisma']] as const)
    .map(([key, label]) => numberField(key, label, 'Atributos', 0, `1d20+{${key}}`)),
  ...t20Skills.map(([key, label]) => numberField(`per_${key}`, label, 'Perícias', 0, `1d20+{per_${key}}`))
];

const cocCharacteristics: [string, string][] = [
  ['forca', 'Força (STR)'], ['constituicao', 'Constituição (CON)'], ['tamanho', 'Tamanho (SIZ)'], ['destreza', 'Destreza (DEX)'],
  ['aparencia', 'Aparência (APP)'], ['educacao', 'Educação (EDU)'], ['inteligencia', 'Inteligência (INT)'], ['poder', 'Poder (POW)']
];
const cocSkills: [string, string][] = [
  ['antropologia', 'Antropologia'], ['arqueologia', 'Arqueologia'], ['arte', 'Arte/Ofício'], ['biblioteca', 'Biblioteca'],
  ['charm', 'Charme'], ['ciencia', 'Ciência'], ['contabilidade', 'Contabilidade'], ['direito', 'Direito'],
  ['escutar', 'Escutar'], ['esquivar', 'Esquivar'], ['furtividade', 'Furtividade'], ['historia', 'História'],
  ['intimidar', 'Intimidar'], ['lutar', 'Lutar'], ['medicina', 'Medicina'], ['mundo_natural', 'Mundo Natural'],
  ['ocultismo', 'Ocultismo'], ['persuadir', 'Persuadir'], ['psicologia', 'Psicologia'], ['rastrear', 'Rastrear']
];
const cocFields: SystemField[] = [
  numberField('sorte', 'Sorte', 'Progressão', 0, '1d100'),
  longField('contatos', 'Contatos e notas', 'Identidade'),
  listField('condicoes', 'Condições e consequências', 'Investigação'),
  listField('desenvolvimento', 'Desenvolvimento', 'Progressão'),
  textField('ocupacao', 'Ocupação', 'Identidade'), textField('local', 'Residência', 'Identidade'), textField('epoca', 'Época', 'Identidade'),
  numberField('idade', 'Idade', 'Identidade'), numberField('credito', 'Crédito', 'Progressão'),
  ...cocCharacteristics.flatMap(([key, label]) => [
    numberField(key, label, 'Características', 0, '1d100'),
    numberField(`${key}_metade`, `${label} · metade`, 'Limiares', 0, null, `floordiv(${key}, 2)`),
    numberField(`${key}_quinto`, `${label} · quinto`, 'Limiares', 0, null, `floordiv(${key}, 5)`)
  ]),
  ...cocSkills.map(([key, label]) => numberField(`per_${key}`, label, 'Perícias', 0, '1d100'))
];

export const SHEET_TEMPLATES: SheetTemplate[] = [
  { id: 'ordem_paranormal', version: 3, rulesetVersion: null, name: 'Ordem Paranormal RPG', summary: 'Painel de agente com atributos, perícias detalhadas, recursos, defesa, ataques e rituais.', sourceUrl: 'https://jamboeditora.com.br/wp-content/uploads/woocommerce_uploads/2023/02/jamboeditora-ordem-paranormal-ficha-de-personagem-oeuvst.pdf', fields: orderFields,
    layout: makeLayout(orderFields, [
      { id: 'dossie', title: 'Dossiê do agente', items: [identity(), portrait(), ...sectionKeys(orderFields, 'Identidade'), 'nex', resource('health', 'PV', 4), resource('mana', 'PE', 4), resource('sanity', 'SAN', 4), highlight('defesa', 4), 'bloqueio', 'esquiva', 'iniciativa', 'deslocamento', 'equip_defesa', 'outros_defesa'] },
      { id: 'aptidoes', title: 'Aptidões e ação', items: [note('nota_aptidoes', 'Rolagens', 'Ajuste os atributos e bônus de perícia conforme sua mesa.'), ...sectionKeys(orderFields, 'Atributos'), ...orderSkills.filter(([, , attribute]) => ['agilidade', 'forca', 'vigor'].includes(attribute)).map(([key]) => `per_${key}`)] },
      { id: 'investigacao', title: 'Investigação e presença', items: [...orderSkills.filter(([, , attribute]) => ['intelecto', 'presenca'].includes(attribute)).map(([key]) => `per_${key}`), 'pericias_detalhes'] },
      { id: 'arsenal', title: 'Arsenal e rituais', items: ['protecao', 'resistencias', 'proficiencias', 'ataques', 'condicoes', 'rituais', 'carga_atual', 'carga_limite', inventory(), abilities()] },
      { id: 'registros', title: 'Registros', items: [history()] }
    ], 'investigation', '#9b8bc0', 'CRÔNICAS / DOSSIÊ DO AGENTE', { Atributos: { style: 'score', span: 3 }, Perícias: { style: 'compact', span: 6 } }) },
  { id: 'dnd_5e', version: 1, rulesetVersion: '2024', name: 'D&D 5e', summary: 'Atributos, modificadores, resistências, perícias, progressão e combate.', sourceUrl: 'https://media.dndbeyond.com/compendium-images/free-rules/downloads/2024-character-sheet.pdf', fields: dndFields,
    layout: makeLayout(dndFields, [
      { id: 'aventura', title: 'Personagem e combate', items: [identity(), portrait(), ...sectionKeys(dndFields, 'Identidade'), 'nivel', 'experiencia', highlight('proficiencia', 4), resource('health', 'Pontos de vida', 8), highlight('classe_armadura', 4), 'iniciativa', 'deslocamento', 'pontos_temporarios', 'dados_vida', 'salvaguardas_sucesso', 'salvaguardas_falha', 'condicoes'] },
      { id: 'aptidoes', title: 'Atributos e resistências', items: [...dndAbilities.flatMap(([key]) => [key, `mod_${key}`]), ...dndAbilities.flatMap(([key]) => [`treino_res_${key}`, `res_${key}`])] },
      { id: 'pericias', title: 'Perícias e treino', items: [note('nota_treino', 'Treino', 'Use 0 para nenhum, 1 para proficiência e 2 para especialização. Os bônus são recalculados.'), ...dndSkills.flatMap(([key]) => [`treino_per_${key}`, `per_${key}`])] },
      { id: 'equipamento', title: 'Equipamento', items: ['moedas', inventory()] },
      { id: 'recursos', title: 'Características e magias', items: [...sectionKeys(dndFields, 'Magias'), abilities()] },
      { id: 'jornada', title: 'Jornada', items: [history()] }
    ], 'fantasy', '#b56d63', 'CRÔNICAS / AVENTURA', { Atributos: { style: 'score', span: 3 }, Modificadores: { style: 'score', span: 3 }, Resistências: { style: 'compact', span: 6 }, Perícias: { style: 'compact', span: 6 } }) },
  { id: 'tormenta20', version: 1, rulesetVersion: null, name: 'Tormenta20', summary: 'Raça, origem, classe, seis atributos, PV, PM e perícias.', sourceUrl: 'https://jamboeditora.com.br/wp-content/uploads/2023/06/Tormenta20-Resumo-de-Regras.pdf', fields: t20Fields,
    layout: makeLayout(t20Fields, [
      { id: 'heroi', title: 'Herói de Arton', items: [identity(), portrait(), ...sectionKeys(t20Fields, 'Identidade'), 'nivel', 'experiencia', resource('health', 'PV', 6), resource('mana', 'PM', 6), highlight('defesa', 6), 'deslocamento'] },
      { id: 'combate', title: 'Atributos e combate', items: [...sectionKeys(t20Fields, 'Atributos'), 'ataques', 'condicoes', ...t20Skills.slice(0, 10).map(([key]) => `per_${key}`)] },
      { id: 'oficios', title: 'Conhecimentos e ofícios', items: [note('nota_pericias', 'Perícias', 'Informe o bônus total de cada perícia para usar as rolagens da campanha.'), ...t20Skills.slice(10).map(([key]) => `per_${key}`)] },
      { id: 'poderes', title: 'Poderes e magias', items: [abilities()] },
      { id: 'mochila', title: 'Mochila', items: [inventory()] },
      { id: 'cronica', title: 'Crônica', items: [history()] }
    ], 'mythic', '#be825d', 'CRÔNICAS / LENDA', { Atributos: { style: 'score', span: 4 }, Perícias: { style: 'compact', span: 6 } }) },
  { id: 'call_of_cthulhu_7e', version: 1, rulesetVersion: '7e', name: 'Call of Cthulhu 7e', summary: 'Características percentuais, limiares calculados, perícias, PV, SAN e magia.', sourceUrl: 'https://www.chaosium.com/content/FreePDFs/CoC/CHA23131%20Call%20of%20Cthulhu%207th%20Edition%20Quick-Start%20Rules.pdf', fields: cocFields,
    layout: makeLayout(cocFields, [
      { id: 'investigador', title: 'Arquivo do investigador', items: [identity(), portrait(), ...sectionKeys(cocFields, 'Identidade'), 'credito', 'sorte', 'desenvolvimento', resource('health', 'PV', 4), resource('sanity', 'SAN', 4), resource('mana', 'Magia', 4)] },
      { id: 'caracteristicas', title: 'Características e limiares', items: [note('nota_limiares', 'Valores percentuais', 'Metade e quinto são calculados a partir de cada característica.'), ...cocCharacteristics.flatMap(([key]) => [key, `${key}_metade`, `${key}_quinto`])] },
      { id: 'pericias', title: 'Perícias', items: sectionKeys(cocFields, 'Perícias') },
      { id: 'pertences', title: 'Pertences e conhecimentos', items: [inventory(), abilities()] },
      { id: 'anotacoes', title: 'Anotações', items: ['condicoes', history()] }
    ], 'archive', '#748d85', 'CRÔNICAS / ARQUIVO DO INVESTIGADOR', { Características: { style: 'score', span: 4 }, Limiares: { style: 'compact', span: 4 }, Perícias: { style: 'compact', span: 6 } }) }
];
