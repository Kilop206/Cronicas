import { SystemField } from './api';

export type SheetValues = Record<string, string | number | boolean>;

export function calculateSheet(fields: SystemField[], values: SheetValues): string | null {
  const calculated = new Map(fields.filter(field => field.formula?.trim()).map(field => [field.key, field]));
  const visiting = new Set<string>();
  const done = new Set<string>();
  const resolve = (key: string): number => {
    const field = calculated.get(key);
    if (!field) return Number(values[key] ?? 0) || 0;
    if (done.has(key)) return Number(values[key]);
    if (visiting.has(key)) throw new Error('Fórmulas em ciclo.');
    visiting.add(key);
    const tokens = field.formula!.match(/[a-z][a-z0-9_]*|\d+|[()+*/-]/g) || [];
    if (tokens.join('') !== field.formula!.replace(/\s/g, '')) throw new Error('Fórmula inválida.');
    let cursor = 0;
    const atom = (): number => {
      const token = tokens[cursor++];
      if (token === '-') return -atom();
      if (token === '+') return atom();
      if (token === '(') { const value = sum(); if (tokens[cursor++] !== ')') throw new Error('Parênteses inválidos.'); return value; }
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
  const match = /^(\d{1,2}d(?:4|6|8|10|12|20|100)(?:kh1|kl1)?)(?:([+-])(\{[a-z][a-z0-9_]*\}|\d{1,4}))?$/i.exec(expression.trim());
  if (!match) return null;
  const value = match[3]?.startsWith('{') ? values[match[3].slice(1, -1)] : match[3];
  const modifier = match[2] ? Number(value ?? 0) * (match[2] === '-' ? -1 : 1) : 0;
  if (!Number.isInteger(modifier) || Math.abs(modifier) > 9999) return null;
  return `${match[1]}${modifier < 0 ? modifier : modifier > 0 ? `+${modifier}` : ''}`;
}
