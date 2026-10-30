import type { CharacterInput, RpgSystem, SheetFieldValue } from './api';

// Read-only projections; authoritative checks and mutations always run in the API.
export function d20ConditionState(values: Record<string, SheetFieldValue>, health?: number) {
  const rows = values['estado_regras'];
  const state: Record<string, number> = {};
  if (Array.isArray(rows)) for (const row of rows) if (row && typeof row === 'object' && !Array.isArray(row) && typeof row['chave'] === 'string' && typeof row['valor'] === 'number') state[row['chave']] = row['valor'];
  const active = new Set(Object.keys(state).filter(key => key.startsWith('cond_') && state[key] !== 0).map(key => key.slice(5)));
  if (health === 0 || state['morto'] || state['asfixia_inconsciente']) active.add('inconsciente');
  const relationships: Record<string, string[]> = { agarrado: ['desprevenido', 'imovel'], atordoado: ['desprevenido'], cego: ['desprevenido', 'lento'], enredado: ['lento', 'vulneravel'], exausto: ['debilitado', 'lento', 'vulneravel'], fatigado: ['fraco', 'vulneravel'], inconsciente: ['indefeso'], indefeso: ['desprevenido'], paralisado: ['imovel', 'indefeso'], petrificado: ['inconsciente'], surpreendido: ['desprevenido'] };
  for (const key of active) for (const implied of relationships[key] || []) active.add(implied);
  return active;
}
export function hasD20Conditions(system: RpgSystem | null) {
  return !!system && system.fields.some(field => field.key === 'estado_regras') && (
    system.templateId === 'ordem_paranormal' && [6, 7].includes(system.templateVersion) && system.rulesetVersion === '1.3' ||
    system.templateId === 'tormenta20' && [3, 4].includes(system.templateVersion) && system.rulesetVersion === 'jogo-do-ano-2024-v1.3');
}
export function d20Defense(values: Record<string, SheetFieldValue>, paranormal: boolean, ranged = false, health?: number) {
  const active = d20ConditionState(values, health);
  let penalty = active.has('indefeso') ? 10 : active.has('desprevenido') ? 5 : active.has('vulneravel') ? 2 : 0;
  if (paranormal && active.has('sobrecarregado')) penalty = Math.max(penalty, 5);
  const prone = active.has('caido') ? ranged ? 5 : -5 : 0;
  return Number(values['defesa'] || 0) - (prone < 0 && paranormal ? Math.max(penalty, -prone) : penalty) + (prone < 0 && paranormal ? 0 : prone);
}
export function d20Speed(draft: CharacterInput) {
  const active = d20ConditionState(draft.fields, draft.health);
  if (active.has('imovel') || active.has('inconsciente')) return 0;
  let speed = Number(draft.fields['deslocamento'] || 0);
  if (active.has('lento')) speed = Math.floor(speed / 3) * 1.5;
  if (active.has('sobrecarregado')) speed = Math.max(0, speed - 3);
  if (active.has('caido')) speed = Math.min(speed, 1.5);
  return speed;
}
