import type { CharacterInput, OverlayResource, RpgSystem, SheetFieldValue } from './api';

export type SheetRendererId = 'ordem_paranormal' | 'dnd_5e' | 'tormenta20' | 'call_of_cthulhu_7e' | 'custom';

interface SheetRendererDefinition {
  id: Exclude<SheetRendererId, 'custom'>;
  version: number;
  required: string[];
  resources: { key: 'health' | 'sanity' | 'mana'; label: string }[];
}

// The identifier and version are persisted with the system. A renamed system keeps its renderer.
const renderers: SheetRendererDefinition[] = [
  { id: 'ordem_paranormal', version: 1, required: ['nex', 'agilidade', 'forca', 'intelecto', 'presenca', 'vigor', 'defesa'], resources: [{ key: 'health', label: 'PV' }, { key: 'mana', label: 'PE' }, { key: 'sanity', label: 'SAN' }] },
  { id: 'dnd_5e', version: 1, required: ['nivel', 'forca', 'destreza', 'constituicao', 'inteligencia', 'sabedoria', 'carisma', 'classe_armadura'], resources: [{ key: 'health', label: 'HP' }] },
  { id: 'tormenta20', version: 1, required: ['nivel', 'forca', 'destreza', 'defesa'], resources: [{ key: 'health', label: 'PV' }, { key: 'mana', label: 'PM' }] },
  { id: 'call_of_cthulhu_7e', version: 1, required: ['ocupacao', 'forca', 'poder', 'per_escutar'], resources: [{ key: 'health', label: 'HP' }, { key: 'sanity', label: 'SAN' }] }
];

export function resolveSheetRenderer(system: RpgSystem | null): SheetRendererDefinition | null {
  if (!system?.templateId || system.rendererMode === 'layout') return null;
  const definition = renderers.find(item => item.id === system.templateId && item.version === system.templateVersion);
  if (!definition) return null;
  const keys = new Set(system.fields.map(field => field.key));
  return definition.required.every(key => keys.has(key)) ? definition : null;
}

export function canAssociateTemplate(templateId: string, fields: { key: string }[]): boolean {
  const definition = renderers.find(item => item.id === templateId);
  if (!definition) return false;
  const keys = new Set(fields.map(field => field.key));
  return definition.required.every(key => keys.has(key));
}

export function sheetResources(system: RpgSystem | null) {
  const renderer = resolveSheetRenderer(system);
  if (renderer) return renderer.resources;
  if (system?.layout) return system.layout.pages.flatMap(page => page.blocks)
    .filter(block => block.kind === 'resource' && (block.key === 'health' || block.key === 'sanity' || block.key === 'mana'))
    .map(block => ({ key: block.key as 'health' | 'sanity' | 'mana', label: block.title || 'Recurso' }))
    .filter((item, index, all) => all.findIndex(other => other.key === item.key) === index);
  return [{ key: 'health' as const, label: 'Vida' }, { key: 'sanity' as const, label: 'Sanidade' }, { key: 'mana' as const, label: 'Mana' }];
}

export function sheetOverlayResources(system: RpgSystem | null, character: CharacterInput): OverlayResource[] {
  if (!resolveSheetRenderer(system) && system?.layout) {
    const blocks = system.layout.pages.flatMap(page => page.blocks).filter(block => (block.kind === 'resource' || block.kind === 'tracker') && !!block.key);
    return blocks.filter((block, index) => blocks.findIndex(other => other.key === block.key) === index).map(block => ({
      label: block.title || system.fields.find(field => field.key === block.key)?.label || 'Recurso',
      current: overlayValue(character, block.key!),
      max: block.maxKey ? overlayValue(character, block.maxKey) : overlayMax(character, block.key!)
    }));
  }
  return sheetResources(system).map(item => ({ label: item.label, current: overlayValue(character, item.key), max: overlayMax(character, item.key) }));
}

function overlayValue(character: CharacterInput, key: string): number {
  if (key === 'health' || key === 'sanity' || key === 'mana') return character[key];
  const value = character.fields[key];
  return typeof value === 'number' && Number.isFinite(value) ? value : 0;
}

function overlayMax(character: CharacterInput, key: string): number {
  return key === 'health' ? character.maxHealth : key === 'sanity' ? character.maxSanity : key === 'mana' ? character.maxMana : 1;
}

const indicatorKeys: Record<string, { key: string; label: string }[]> = {
  ordem_paranormal: [{ key: 'nex', label: 'NEX' }, { key: 'defesa', label: 'Defesa' }],
  dnd_5e: [{ key: 'nivel', label: 'Nível' }, { key: 'classe_armadura', label: 'AC' }, { key: 'iniciativa', label: 'Iniciativa' }],
  tormenta20: [{ key: 'nivel', label: 'Nível' }, { key: 'defesa', label: 'Defesa' }],
  call_of_cthulhu_7e: [{ key: 'sorte', label: 'Sorte' }]
};

export function sheetIndicators(system: RpgSystem | null, values: Record<string, SheetFieldValue>) {
  const renderer = resolveSheetRenderer(system);
  const configured = !renderer && system?.layout ? system.layout.pages.flatMap(page => page.blocks)
    .filter(block => block.kind === 'field' && block.style === 'score' && !!block.key)
    .filter((block, index, all) => all.findIndex(other => other.key === block.key) === index).slice(0, 6)
    .map(block => ({ key: block.key!, label: block.title || system.fields.find(field => field.key === block.key)?.label || 'Indicador' })) : [];
  return (renderer ? indicatorKeys[renderer.id] || [] : configured)
    .filter(item => values[item.key] !== undefined)
    .map(item => ({ label: item.label, value: String(values[item.key]) }));
}
