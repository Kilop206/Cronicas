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
      if (token === 'floordiv') {
        if (tokens[cursor++] !== '(') throw new Error('Função inválida.');
        const numerator = sum();
        if (tokens[cursor++] !== ',') throw new Error('Função inválida.');
        const denominator = sum();
        if (tokens[cursor++] !== ')') throw new Error('Função inválida.');
        return Math.floor(numerator / denominator);
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
