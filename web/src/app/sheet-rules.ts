import { SheetFieldValue, SystemField } from './api';

export type SheetValues = Record<string, SheetFieldValue>;

export function calculateSheet(fields: SystemField[], values: SheetValues): string | null {
  for (const field of fields) if (!field.formula?.trim() && values[field.key] === undefined && field.defaultValue !== undefined && field.defaultValue !== null) values[field.key] = field.defaultValue;
  const calculated = new Map(fields.filter(field => field.formula?.trim()).map(field => [field.key, field]));
  const visiting = new Set<string>();
  const done = new Set<string>();
  const resolve = (key: string): number => {
    const field = calculated.get(key);
    if (!field) return Number(values[key] ?? 0) || 0;
    if (done.has(key)) return Number(values[key]);
    if (visiting.has(key)) throw new Error('Fórmulas em ciclo.');
    visiting.add(key);
    const tokens = field.formula!.match(/[a-z][a-z0-9_]*|\d+|[(),+*/-]/g) || [];
    if (tokens.join('') !== field.formula!.replace(/\s/g, '')) throw new Error('Fórmula inválida.');
    let cursor = 0;
    const atom = (): number => {
      const token = tokens[cursor++];
      if (token === '-') return -atom();
      if (token === '+') return atom();
      if (token === '(') { const value = sum(); if (tokens[cursor++] !== ')') throw new Error('Parênteses inválidos.'); return value; }
      if (token === 'floordiv' || token === 'min') {
        if (tokens[cursor++] !== '(') throw new Error('Função inválida.');
        const numerator = sum();
        if (tokens[cursor++] !== ',') throw new Error('Função inválida.');
        const denominator = sum();
        if (tokens[cursor++] !== ')') throw new Error('Função inválida.');
        return token === 'min' ? Math.min(numerator, denominator) : Math.floor(numerator / denominator);
      }
      if (/^\d+$/.test(token || '')) return Number(token);
      if (/^[a-z][a-z0-9_]*$/.test(token || '')) return resolve(token);
      throw new Error('Fórmula inválida.');
    };
    const product = (): number => { let value = atom(); while (tokens[cursor] === '*' || tokens[cursor] === '/') { const op = tokens[cursor++]; const right = atom(); value = op === '*' ? value * right : Math.trunc(value / right); } return value; };
    const sum = (): number => { let value = product(); while (tokens[cursor] === '+' || tokens[cursor] === '-') { const op = tokens[cursor++]; const right = product(); value = op === '+' ? value + right : value - right; } return value; };
    const result = sum();
    if (cursor !== tokens.length || !Number.isSafeInteger(result) || result < -2147483648 || result > 2147483647) throw new Error('Resultado fora dos limites.');
    values[key] = result; visiting.delete(key); done.add(key); return result;
  };
  try { for (const key of calculated.keys()) resolve(key); return null; }
  catch (error) { return error instanceof Error ? error.message : 'Fórmula inválida.'; }
}

export function fieldRoll(expression: string, values: SheetValues): string | null {
  const match = /^(\{[a-z][a-z0-9_]*\}|\d{1,2})d(4|6|8|10|12|20|100)(kh1|kl1)?(?:([+-])(\{[a-z][a-z0-9_]*\}|\d{1,4}))?$/i.exec(expression.trim());
  if (!match) return null;
  const count = Number(match[1].startsWith('{') ? values[match[1].slice(1, -1)] : match[1]);
  const value = match[5]?.startsWith('{') ? values[match[5].slice(1, -1)] : match[5];
  const modifier = match[4] ? Number(value ?? 0) * (match[4] === '-' ? -1 : 1) : 0;
  if (!Number.isInteger(count) || count < 1 || count > 40 || !Number.isInteger(modifier) || Math.abs(modifier) > 9999) return null;
  const keep = count > 1 ? match[3] || '' : '';
  return `${count}d${match[2]}${keep}${modifier < 0 ? modifier : modifier > 0 ? `+${modifier}` : ''}`;
}

// Ordem Paranormal 1.3: atributo 0 rola 2d20 e conserva o menor resultado.
export function paranormalFieldRoll(expression: string, values: SheetValues): string | null {
  const source = /^\{([a-z][a-z0-9_]*)\}d20kh1(?=[+-]|$)/.exec(expression.trim());
  if (source && values[source[1]] === 0)
    return fieldRoll(expression.trim().replace(source[0], '2d20kl1'), values);
  return fieldRoll(expression, values);
}

export type ParanormalClass = 'combatente' | 'especialista' | 'ocultista';

// Baseline only: origins, powers and other choices can change these totals.
export function paranormalV13BaseResources(characterClass: string, nex: number, vigor: number, presenca: number): { pv: number; pe: number; san: number } | null {
  const progression: Record<ParanormalClass, { pv: number; pe: number; san: number; pvStep: number; peStep: number; sanStep: number }> = {
    combatente: { pv: 20, pe: 2, san: 12, pvStep: 4, peStep: 2, sanStep: 3 },
    especialista: { pv: 16, pe: 3, san: 16, pvStep: 3, peStep: 3, sanStep: 4 },
    ocultista: { pv: 12, pe: 4, san: 20, pvStep: 2, peStep: 4, sanStep: 5 }
  };
  const base = progression[characterClass as ParanormalClass];
  if (!base || ![nex, vigor, presenca].every(Number.isInteger) || nex < 5 || nex > 99 || nex !== 99 && nex % 5 !== 0 || vigor < 0 || presenca < 0) return null;
  const steps = nex === 99 ? 19 : (nex - 5) / 5;
  return { pv: base.pv + vigor + steps * (base.pvStep + vigor), pe: base.pe + presenca + steps * (base.peStep + presenca), san: base.san + steps * base.sanStep };
}

const dnd2014LevelXp = [0, 300, 900, 2700, 6500, 14000, 23000, 34000, 48000, 64000, 85000, 100000, 120000, 140000, 165000, 195000, 225000, 265000, 305000, 355000];

export function dnd2014NextLevelXp(level: number): number | null {
  return Number.isInteger(level) && level >= 1 && level < 20 ? dnd2014LevelXp[level] : null;
}

export function coc7eDamageProfile(strength: number, size: number): { bonus: string; build: number; rollExpression: string | null } | null {
  if (![strength, size].every(Number.isInteger) || strength < 0 || size < 0 || strength > 1000 || size > 1000) return null;
  const total = strength + size;
  if (total < 2) return null;
  if (total <= 64) return { bonus: '−2', build: -2, rollExpression: null };
  if (total <= 84) return { bonus: '−1', build: -1, rollExpression: null };
  if (total <= 124) return { bonus: '0', build: 0, rollExpression: null };
  if (total <= 164) return { bonus: '+1d4', build: 1, rollExpression: '1d4' };
  if (total <= 204) return { bonus: '+1d6', build: 2, rollExpression: '1d6' };
  const dice = 2 + Math.floor((total - 205) / 80);
  return { bonus: `+${dice}d6`, build: dice + 1, rollExpression: dice <= 40 ? `${dice}d6` : null };
}

// The target is the number required for success, including the selected difficulty.
export function coc7eSuccessDegree(total: number, target: number): string | null {
  if (!Number.isInteger(total) || total < 1 || total > 100 || !Number.isInteger(target) || target < 0 || target > 100) return null;
  if (total === 1) return 'crítico';
  if (total === 100 || target < 50 && total >= 96) return 'falha crítica';
  if (total <= Math.floor(target / 5)) return 'extremo';
  if (total <= Math.floor(target / 2)) return 'difícil';
  return total <= target ? 'regular' : 'falha';
}
