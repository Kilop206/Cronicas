import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription, finalize } from 'rxjs';
import { ApiService, Character, apiError } from './api';

interface RuleAction { id: string; label: string; amountLabel: string | null; options: { value: string; label: string }[] | null; }
interface RuleView { edition: string; actions: RuleAction[]; stats: { label: string; value: string }[]; settings: { key: string; label: string; min: number; max: number }[]; state: Record<string, number>; }

@Component({ selector: 'app-sheet-rule-controls', standalone: true, imports: [CommonModule, FormsModule], template: `
  <section *ngIf="view() as rules" class="rule-controls" aria-label="Ações das regras do sistema">
    <details open><summary>{{ rules.edition }} · Ações e recursos</summary>
      <dl><div *ngFor="let stat of rules.stats"><dt>{{ stat.label }}</dt><dd>{{ stat.value }}</dd></div></dl>
      <form *ngIf="character.canEdit" (ngSubmit)="apply()">
        <label>Ação<select name="ruleAction" [ngModel]="actionId" (ngModelChange)="choose($event)" [disabled]="busy()"><option *ngFor="let item of rules.actions" [value]="item.id">{{ item.label }}</option></select></label>
        <label *ngIf="action()?.amountLabel">{{ action()?.amountLabel }}<input type="number" name="amount" [(ngModel)]="amount" [min]="minimum()" [max]="maximum()" required [disabled]="busy()"></label>
        <label *ngIf="action()?.options?.length">Opção<select name="option" [(ngModel)]="option" [disabled]="busy()"><option *ngFor="let item of action()?.options" [value]="item.value">{{ item.label }}</option></select></label>
        <label>Rolagens<select name="visibility" [(ngModel)]="visibility" [disabled]="busy()"><option value="campaign">Mesa</option><option value="private">Privadas</option><option *ngIf="isMaster" value="gm">Mestres</option></select></label>
        <button type="submit" class="button button-small" [disabled]="busy() || !ready || !action()">{{ busy() ? 'Aplicando…' : 'Aplicar ação' }}</button>
      </form>
      <p *ngIf="character.canEdit && !ready" class="muted">Aguarde o salvamento das alterações antes de aplicar uma ação.</p>
      <p *ngIf="actionId === 'configure' && view()?.state" class="muted">Valor salvo deste indicador: {{ view()?.state?.[option] || 0 }}.</p>
      <p *ngIf="message()" role="status">{{ message() }}</p><p *ngIf="error()" role="alert" class="form-error">{{ error() }}</p>
    </details>
  </section><p *ngIf="!view() && error()" role="alert" class="form-error">{{ error() }}</p>
`, styles: [`
 :host{display:block;min-width:0}.rule-controls{padding:9px 12px;border:1px solid var(--border);margin-block:9px;background:var(--surface);color:var(--text);font:12px var(--font-body,Arial,sans-serif)}summary{cursor:pointer;font-weight:700}dl{display:flex;gap:8px 20px;flex-wrap:wrap;margin:10px 0}dl>div{min-width:100px}dt{font-size:10px;color:var(--muted)}dd{margin:3px 0;font-weight:700}form{display:flex;align-items:end;flex-wrap:wrap;gap:8px}label{display:grid;gap:4px;min-width:0;font-size:11px}select,input{max-width:100%;min-height:32px;padding:4px 7px;background:var(--surface-raised);color:var(--text);border:1px solid var(--border)}input{width:110px}p{margin:8px 0 0;line-height:1.4}select:focus-visible,input:focus-visible{outline:2px solid var(--text);outline-offset:2px}@media(max-width:520px){form{display:grid;grid-template-columns:minmax(0,1fr)}label,input,select{width:100%}}@media print{form,p.muted{display:none}}
`] })
export class SheetRuleControlsComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) character!: Character;
  @Input() ready = false;
  @Input() isMaster = false;
  @Output() applied = new EventEmitter<Character>();
  @Output() working = new EventEmitter<boolean>();
  private api = inject(ApiService);
  private request?: Subscription;
  view = signal<RuleView | null>(null); busy = signal(false); error = signal(''); message = signal('');
  actionId = ''; option = ''; amount = 1; visibility: 'campaign' | 'private' | 'gm' = 'campaign';
  private version = '';
  ngOnChanges() {
    const version = `${this.character.id}:${this.character.version}`;
    if (version === this.version) return;
    this.version = version; this.request?.unsubscribe();
    this.request = this.api.characterRules(this.character.id).subscribe({ next: result => {
      this.view.set(result.supported ? result.rules as RuleView : null);
      if (this.view() && !this.view()!.actions.some(action => action.id === this.actionId)) this.choose(this.view()!.actions[0].id);
    }, error: error => this.error.set(apiError(error)) });
  }
  action() { return this.view()?.actions.find(action => action.id === this.actionId); }
  choose(id: string) { this.actionId = id; this.option = this.action()?.options?.[0]?.value || ''; this.amount = 1; this.message.set(''); }
  minimum() { return this.actionId === 'configure' ? this.view()?.settings.find(item => item.key === this.option)?.min ?? 0 : 0; }
  maximum() { return this.actionId === 'configure' ? this.view()?.settings.find(item => item.key === this.option)?.max ?? 9999 : 9999; }
  apply() {
    if (!this.ready || this.busy() || !this.character.canEdit || !this.action()) return;
    this.busy.set(true); this.working.emit(true); this.error.set(''); this.message.set('');
    this.api.applyCharacterRule(this.character.id, this.character.version, { action: this.actionId, amount: this.amount, option: this.option || null, visibility: this.visibility })
      .pipe(finalize(() => { this.busy.set(false); this.working.emit(false); })).subscribe({
        next: result => { this.message.set(result.message); this.applied.emit(result.character); }, error: error => this.error.set(apiError(error))
      });
  }
  ngOnDestroy() { this.request?.unsubscribe(); }
}
