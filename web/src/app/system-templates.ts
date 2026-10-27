import type { SheetBlock, SheetLayout, SheetPage, SystemField } from './api';

export interface SheetTemplate {
  id: string;
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

const block = (id: string, kind: SheetBlock['kind'], title: string, span: number, key: string | null = null): SheetBlock =>
  ({ id, kind, title, span, key, text: null, style: kind === 'identity' || kind === 'inventory' || kind === 'abilities' || kind === 'history' ? 'plain' : 'panel' });

function makeLayout(fields: SystemField[], sections: { id: string; title: string; sections: string[] }[], resources: { key: string; title: string }[], skin: SheetLayout['skin'], accentColor: string): SheetLayout {
  const pages: SheetPage[] = sections.map((page, index) => ({ id: page.id, title: page.title, blocks: [
    ...(index === 0 ? [block('identidade', 'identity', 'Personagem', 8), block('retrato', 'portrait', 'Retrato', 4), ...resources.map(item => block(`recurso_${item.key}`, 'resource', item.title, resources.length === 3 ? 4 : 6, item.key))] : []),
    ...fields.filter(field => page.sections.includes(field.section)).map(field => block(`campo_${field.key}`, 'field', field.label, field.span || 6, field.key))
  ] }));
  pages.push({ id: 'colecoes', title: 'Equipamento e habilidades', blocks: [block('colecao_inventario', 'inventory', 'Inventário', 12), block('colecao_habilidades', 'abilities', 'Habilidades e poderes', 12)] });
  pages.push({ id: 'historico', title: 'Histórico', blocks: [block('colecao_historico', 'history', 'Histórico do personagem', 12)] });
  return { accentColor, skin, pages };
}

const orderSkills: [string, string, string][] = [
  ['acrobacia', 'Acrobacia', 'agilidade'], ['adestramento', 'Adestramento', 'presenca'], ['artes', 'Artes', 'presenca'],
  ['atletismo', 'Atletismo', 'forca'], ['ciencias', 'Ciências', 'intelecto'], ['crime', 'Crime', 'agilidade'],
  ['diplomacia', 'Diplomacia', 'presenca'], ['enganacao', 'Enganação', 'presenca'], ['fortitude', 'Fortitude', 'vigor'],
  ['furtividade', 'Furtividade', 'agilidade'], ['iniciativa', 'Iniciativa', 'agilidade'], ['intimidacao', 'Intimidação', 'presenca'],
  ['intuicao', 'Intuição', 'presenca'], ['investigacao', 'Investigação', 'intelecto'], ['luta', 'Luta', 'forca'],
  ['medicina', 'Medicina', 'intelecto'], ['ocultismo', 'Ocultismo', 'intelecto'], ['percepcao', 'Percepção', 'presenca'],
  ['pontaria', 'Pontaria', 'agilidade'], ['reflexos', 'Reflexos', 'agilidade'], ['religiao', 'Religião', 'presenca'],
  ['sobrevivencia', 'Sobrevivência', 'intelecto'], ['tatica', 'Tática', 'intelecto'], ['tecnologia', 'Tecnologia', 'intelecto'],
  ['vontade', 'Vontade', 'presenca']
];
const orderFields: SystemField[] = [
  textField('origem', 'Origem', 'Identidade'), textField('classe', 'Classe', 'Identidade'), textField('trilha', 'Trilha', 'Identidade'),
  numberField('nex', 'NEX (%)', 'Progressão'), numberField('deslocamento', 'Deslocamento', 'Combate'),
  numberField('equip_defesa', 'Bônus do equipamento', 'Combate'), numberField('outros_defesa', 'Outros bônus de defesa', 'Combate'),
  numberField('defesa', 'Defesa', 'Combate', 0, null, '10 + agilidade + equip_defesa + outros_defesa'),
  ...([['agilidade', 'Agilidade'], ['intelecto', 'Intelecto'], ['vigor', 'Vigor'], ['presenca', 'Presença'], ['forca', 'Força']] as const)
    .map(([key, label]) => numberField(key, label, 'Atributos', 1, `{${key}}d20kh1`)),
  ...orderSkills.map(([key, label, attribute]) => numberField(`per_${key}`, label, 'Perícias', 0, `{${attribute}}d20kh1+{per_${key}}`))
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
  ])
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
  { id: 'ordem_paranormal', name: 'Ordem Paranormal RPG', summary: 'NEX, atributos, PV, PE, SAN, defesa e perícias com rolagens configuráveis.', sourceUrl: 'https://jamboeditora.com.br/wp-content/uploads/woocommerce_uploads/2023/02/jamboeditora-ordem-paranormal-ficha-de-personagem-oeuvst.pdf', fields: orderFields,
    layout: makeLayout(orderFields, [{ id: 'principal', title: 'Investigador', sections: ['Identidade', 'Progressão', 'Combate'] }, { id: 'atributos', title: 'Atributos', sections: ['Atributos'] }, { id: 'pericias', title: 'Perícias', sections: ['Perícias'] }], [{ key: 'health', title: 'PV' }, { key: 'mana', title: 'PE' }, { key: 'sanity', title: 'SAN' }], 'investigation', '#9b8bc0') },
  { id: 'dnd_5e', name: 'D&D 5e', summary: 'Atributos, modificadores, resistências, perícias, progressão e combate.', sourceUrl: 'https://media.dndbeyond.com/compendium-images/free-rules/downloads/2024-character-sheet.pdf', fields: dndFields,
    layout: makeLayout(dndFields, [{ id: 'principal', title: 'Aventureiro', sections: ['Identidade', 'Progressão', 'Combate'] }, { id: 'atributos', title: 'Atributos e resistências', sections: ['Atributos', 'Modificadores', 'Resistências'] }, { id: 'pericias', title: 'Perícias', sections: ['Perícias'] }], [{ key: 'health', title: 'PV' }], 'fantasy', '#a99167') },
  { id: 'tormenta20', name: 'Tormenta20', summary: 'Raça, origem, classe, seis atributos, PV, PM e perícias.', sourceUrl: 'https://jamboeditora.com.br/wp-content/uploads/2023/06/Tormenta20-Resumo-de-Regras.pdf', fields: t20Fields,
    layout: makeLayout(t20Fields, [{ id: 'principal', title: 'Herói', sections: ['Identidade', 'Progressão', 'Combate'] }, { id: 'atributos', title: 'Atributos', sections: ['Atributos'] }, { id: 'pericias', title: 'Perícias', sections: ['Perícias'] }], [{ key: 'health', title: 'PV' }, { key: 'mana', title: 'PM' }], 'fantasy', '#be825d') },
  { id: 'call_of_cthulhu_7e', name: 'Call of Cthulhu 7e', summary: 'Características percentuais, limiares calculados, perícias, PV, SAN e magia.', sourceUrl: 'https://www.chaosium.com/content/FreePDFs/CoC/CHA23131%20Call%20of%20Cthulhu%207th%20Edition%20Quick-Start%20Rules.pdf', fields: cocFields,
    layout: makeLayout(cocFields, [{ id: 'principal', title: 'Investigador', sections: ['Identidade', 'Progressão'] }, { id: 'caracteristicas', title: 'Características', sections: ['Características'] }, { id: 'limiares', title: 'Metade e quinto', sections: ['Limiares'] }, { id: 'pericias', title: 'Perícias', sections: ['Perícias'] }], [{ key: 'health', title: 'PV' }, { key: 'sanity', title: 'SAN' }, { key: 'mana', title: 'Magia' }], 'investigation', '#748d85') }
];
