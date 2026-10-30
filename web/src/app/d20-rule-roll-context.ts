import { Injectable, OnDestroy, inject, signal } from '@angular/core';
import { Subscription, finalize } from 'rxjs';
import { ApiService, Character, RpgSystem, apiError } from './api';

@Injectable()
export class D20RuleRollContext implements OnDestroy {
  character: () => Character | null = () => null;
  system: () => RpgSystem | null = () => null;
  ready: () => boolean = () => false;
  applied: (character: Character) => void = () => undefined;
  working: (busy: boolean) => void = () => undefined;
  private api = inject(ApiService);
  private request?: Subscription;
  private version = '';
  readonly checks = signal<Record<string, {expression: string | null; error: string | null}>>({});
  refresh() {
    const character = this.character();
    const version = `${character?.id}:${character?.version}:${this.system()?.id}`;
    if (this.version === version) return;
    this.version = version; this.request?.unsubscribe(); this.checks.set({});
    if (!character || !this.supported()) return;
    this.request = this.api.characterRules(character.id).subscribe({ next: result => this.checks.set(result.supported ? (result.rules as {checks?: Record<string, {expression: string | null; error: string | null}>}).checks || {} : {}), error: () => this.checks.set({}) });
  }
  ngOnDestroy() { this.request?.unsubscribe(); }
  supported() {
    const system = this.system();
    return !!system && system.fields.some(f => f.key === 'estado_regras') && (
      system.templateId === 'ordem_paranormal' && [6, 7].includes(system.templateVersion) && system.rulesetVersion === '1.3' ||
      system.templateId === 'tormenta20' && [3, 4].includes(system.templateVersion) && system.rulesetVersion === 'jogo-do-ano-2024-v1.3');
  }
  roll(expression: string, visibility: 'campaign' | 'private', label: string, message: (text: string) => void, busy: (value: boolean) => void) {
    if (!this.supported()) return false;
    const references = [...expression.matchAll(/\{([a-z][a-z0-9_]*)\}/g)].map(match => match[1]);
    const key = references.find(value => value.startsWith('per_')) || references[0];
    if (!key || !this.system()?.fields.some(field => field.key === key && field.rollExpression === expression)) {
      message(`${label}: a rolagem precisa corresponder a um campo do sistema.`); return true;
    }
    const character = this.character();
    if (!character || !this.ready()) { message(`${label}: aguarde o salvamento das alterações.`); return true; }
    busy(true); this.working(true);
    this.api.applyCharacterRule(character.id, character.version, { action: 'test', amount: 0, option: key, visibility })
      .pipe(finalize(() => { busy(false); this.working(false); })).subscribe({ next: result => { message(result.message); this.applied(result.character); }, error: error => message(`${label}: ${apiError(error)}`) });
    return true;
  }
}
