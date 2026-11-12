import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ApiService, Character, RpgSystem, SheetFieldValue, SystemField, SystemMigrationInput, SystemMigrationOptions, SystemMigrationPreview, SystemMigrationTarget, apiError } from '../../api';

@Component({ selector: 'app-character-system-migration', standalone: true, imports: [CommonModule, FormsModule],
  template: `
  <button type="button" class="text-link" [attr.aria-expanded]="opened()" aria-controls="system-migration-assistant" [disabled]="!available || busy()" (click)="toggle()">{{ opened() ? 'Fechar migração' : 'Migrar versão da ficha' }}</button>
  <section *ngIf="opened()" id="system-migration-assistant" class="migration-panel" aria-label="Migração assistida de versão" [attr.aria-busy]="busy()">
    <header><div><span class="overline">MIGRAÇÃO ASSISTIDA</span><h2>Revisar antes de trocar</h2></div><small>Origem: {{ system.name }} · revisão {{ system.revision }} · schema {{ system.templateVersion }}</small></header>
    <p>Campos retirados continuam guardados. Recursos, inventário e habilidades são preservados. A troca de edição exige um conversor específico.</p>
    <p *ngIf="!available" class="form-error" role="status">Salve as alterações da ficha e as anotações do mestre antes de continuar.</p>
    <p *ngIf="busy()" role="status">{{ applying ? 'Aplicando migração…' : 'Carregando prévia e versões…' }}</p>
    <p *ngIf="error()" class="form-error" role="alert">{{ error() }}</p>
    <fieldset [disabled]="busy() || !available">
      <label>Versão de destino<select aria-label="Versão de destino" [ngModel]="selected" (ngModelChange)="choose($event)"><option value="">Escolha uma versão</option><option *ngFor="let item of options()?.targets" [value]="key(item)" [disabled]="item.versionId === character.systemVersionId">{{ item.name }} · revisão {{ item.revision }} · schema {{ item.templateVersion }}{{ item.current ? ' · atual do sistema' : ' · arquivada' }}</option></select></label>
      <button type="button" class="text-link" (click)="refreshVersions()">Atualizar versões disponíveis</button>
      <ng-container *ngIf="destination() as target">
        <div class="migration-columns">
          <section><h3>Mapear campos retirados</h3><p>Copie valores para novas chaves. A chave de origem será mantida.</p>
            <p *ngIf="!retiredKeys().length">Nenhum campo com valor foi retirado.</p>
            <label *ngFor="let from of retiredKeys()">{{ label(from) }} <code>{{ from }}</code><small>{{ display(character.fields[from]) }}</small><select [attr.aria-label]="'Destino de ' + from" [ngModel]="mappings[from] || ''" (ngModelChange)="map(from, $event)"><option value="">Preservar somente na origem</option><option *ngFor="let field of editableFields()" [value]="field.key" [disabled]="occupied(field.key)">{{ field.label }} · {{ field.key }}{{ occupied(field.key) ? ' (já possui valor)' : '' }}</option></select></label>
          </section>
          <section><h3>Ajustar valores no destino</h3><p>Ative apenas os campos que precisam de correção. Fórmulas são recalculadas no servidor.</p>
            <div *ngFor="let field of editableFields()" class="migration-adjustment">
              <label class="migration-check"><input type="checkbox" [ngModel]="adjusting[field.key] || false" (ngModelChange)="adjust(field, $event)">Ajustar {{ field.label }}</label><small>{{ describe(field) }}</small>
              <ng-container *ngIf="adjusting[field.key]" [ngSwitch]="field.type">
                <label *ngSwitchCase="'boolean'">Valor de {{ field.label }}<select [ngModel]="drafts[field.key]" (ngModelChange)="value(field.key, $event)"><option value="true">Sim</option><option value="false">Não</option></select></label>
                <label *ngSwitchCase="'select'">Valor de {{ field.label }}<select [ngModel]="drafts[field.key]" (ngModelChange)="value(field.key, $event)"><option *ngFor="let option of field.options" [value]="option">{{ option }}</option></select></label>
                <label *ngSwitchDefault>Valor de {{ field.label }}<textarea *ngIf="field.type === 'table' || field.type === 'list' || field.type === 'longtext'; else simple" [ngModel]="drafts[field.key]" (ngModelChange)="value(field.key, $event)" rows="3"></textarea><ng-template #simple><input [type]="field.type === 'number' ? 'number' : 'text'" [min]="field.min" [max]="field.max" [ngModel]="drafts[field.key]" (ngModelChange)="value(field.key, $event)"></ng-template><small *ngIf="field.type === 'table' || field.type === 'list'">Use JSON: uma lista de textos ou de linhas com as colunas do campo.</small></label>
              </ng-container>
            </div>
          </section>
        </div>
        <button type="button" class="button button-small secondary" (click)="review()">Gerar prévia da migração</button>
      </ng-container>
    </fieldset>
    <section *ngIf="preview() as result" class="migration-review" aria-label="Prévia da migração" aria-live="polite">
      <h3>Prévia · {{ result.target.name }} · revisão {{ result.target.revision }}</h3>
      <ul *ngIf="errors().length" class="form-error" role="alert"><li *ngFor="let message of errors()">{{ message }}</li></ul>
      <div class="migration-table"><table><caption>Diferenças de estrutura e valores</caption><thead><tr><th>Campo</th><th>Alteração</th><th>Antes</th><th>Depois</th></tr></thead><tbody><tr *ngFor="let change of result.plan.changes"><th scope="row">{{ change.after?.label || change.before?.label || change.key }}<small>{{ change.key }}</small></th><td data-label="Alteração">{{ kind(change.kind) }}<small>{{ change.before ? describe(change.before) : 'Campo novo' }} → {{ change.after ? describe(change.after) : 'Valor preservado no arquivo' }}</small></td><td data-label="Antes">{{ display(change.previousValue) }}</td><td data-label="Depois">{{ display(change.nextValue) }}</td></tr></tbody></table></div>
      <p *ngIf="!result.plan.changes.length">A estrutura e os valores são iguais; somente a versão e os metadados serão atualizados.</p>
      <p *ngIf="result.plan.preservedKeys.length">Campos guardados fora do modelo: <code>{{ result.plan.preservedKeys.join(', ') }}</code>.</p>
      <p>A revisão anterior será guardada no histórico. Para voltar ao modelo anterior, use este assistente novamente; restaurar o histórico comum altera apenas os valores.</p>
      <label class="migration-check"><input type="checkbox" [ngModel]="confirmed" (ngModelChange)="confirmed = $event" [disabled]="!result.canApply || busy() || !available">Revisei o destino, os valores e os campos preservados.</label>
      <button type="button" class="button button-small" [disabled]="!confirmed || !result.canApply || busy() || !available" (click)="apply()">Aplicar migração revisada</button>
    </section>
    <details *ngIf="options()?.history?.length"><summary>Migrações anteriores (até 50)</summary><p *ngFor="let entry of options()?.history">Revisão {{ entry.characterRevision }} · {{ entry.receipt.appliedAt | date:'dd/MM/yyyy HH:mm' }}<br><code>{{ entry.receipt.sourceVersionId }} → {{ entry.receipt.targetVersionId }}</code></p></details>
  </section>`,
  styles: [`:host{display:block}.migration-panel{margin:12px 0;padding:18px;border:1px solid var(--border,#494047);border-radius:10px;background:var(--panel,#1e1b21);max-width:100%;overflow-wrap:anywhere}.migration-panel header{display:flex;align-items:center;justify-content:space-between;gap:12px}.migration-panel h2{font-size:1.2rem;margin:4px 0}.migration-panel h3{font-size:1rem}.migration-panel p,.migration-panel small{font-size:.85rem;line-height:1.5}.migration-panel fieldset{border:0;padding:0;min-width:0}.migration-panel label{display:flex;flex-direction:column;gap:5px;margin:10px 0}.migration-panel input,.migration-panel select,.migration-panel textarea{width:100%;min-width:0}.migration-panel .migration-check{flex-direction:row;align-items:center;gap:8px}.migration-check input{width:auto}.migration-columns{display:grid;grid-template-columns:1fr 1fr;gap:24px;margin:12px 0}.migration-adjustment{border-bottom:1px solid var(--border,#494047);padding:6px 0}.migration-adjustment small{display:block}.migration-table{overflow:auto;max-height:420px;margin:12px 0}.migration-table table{width:100%;border-collapse:collapse;font-size:.85rem}.migration-table th,.migration-table td{padding:9px;text-align:left;vertical-align:top;border-bottom:1px solid var(--border,#494047);max-width:320px;white-space:pre-wrap}.migration-table small{display:block;font-weight:normal;opacity:.8}.migration-review{margin-top:20px}.migration-panel details{margin-top:18px}@media(max-width:680px){.migration-panel{padding:12px}.migration-panel header{display:block}.migration-columns{grid-template-columns:1fr;gap:12px}.migration-table{overflow:visible;max-height:none}.migration-table thead{position:absolute;width:1px;height:1px;overflow:hidden;clip-path:inset(50%)}.migration-table caption,.migration-table tbody,.migration-table tr,.migration-table th,.migration-table td{display:block;min-width:0;max-width:none;width:100%;box-sizing:border-box}.migration-table tr{border-bottom:1px solid var(--border,#494047);padding:8px 0}.migration-table th,.migration-table td{border:0;padding:6px}.migration-table td::before{content:attr(data-label);display:block;font-weight:600;opacity:.8}}`]
})
export class CharacterSystemMigrationComponent implements OnChanges {
  @Input({ required: true }) character!: Character;
  @Input({ required: true }) system!: RpgSystem;
  @Input() available = false;
  @Output() migrated = new EventEmitter<Character>();
  private api = inject(ApiService);
  opened = signal(false); busy = signal(false); error = signal('');
  options = signal<SystemMigrationOptions | null>(null); preview = signal<SystemMigrationPreview | null>(null);
  selected = ''; mappings: Record<string, string> = {}; adjusting: Record<string, boolean> = {}; drafts: Record<string, string> = {};
  confirmed = false; applying = false; private reviewedInput: SystemMigrationInput | null = null;
  ngOnChanges(changes: SimpleChanges) {
    if (changes['character'] && !changes['character'].firstChange && this.opened()) {
      this.invalidate(); this.error.set('A ficha mudou. Gere uma nova prévia antes de aplicar.');
    }
  }
  toggle() { this.opened.update(open => !open); if (this.opened()) this.load(); }
  refreshVersions() { if (!this.available || this.busy()) return; this.load(); }
  private load() {
    this.busy.set(true); this.error.set(''); this.invalidate();
    this.api.systemMigrationOptions(this.character.id).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: result => this.options.set(result), error: e => this.error.set(apiError(e))
    });
  }
  key(target: SystemMigrationTarget) { return target.current ? 'current' : target.versionId || ''; }
  destination() { return this.options()?.targets.find(t => this.key(t) === this.selected); }
  choose(key: string) { this.selected = key; this.mappings = {}; this.adjusting = {}; this.drafts = {}; this.error.set(''); this.invalidate(); }
  retiredKeys() { const keys = new Set(this.destination()?.fields.map(f => f.key)); return Object.keys(this.character.fields).filter(key => !keys.has(key)); }
  editableFields() { return this.destination()?.fields.filter(f => !f.formula?.trim()) || []; }
  occupied(key: string) { return Object.hasOwn(this.character.fields, key); }
  label(key: string) { return this.system.fields.find(f => f.key === key)?.label || key; }
  map(from: string, to: string) { if (to) this.mappings[from] = to; else delete this.mappings[from]; this.invalidate(); }
  adjust(field: SystemField, enabled: boolean) {
    this.adjusting[field.key] = enabled;
    const initial = this.character.fields[field.key] ?? field.defaultValue;
    this.drafts[field.key] = initial === undefined || initial === null ? (field.type === 'boolean' ? 'false' : field.type === 'select' ? field.options?.[0] || '' : '') : typeof initial === 'object' ? JSON.stringify(initial) : String(initial);
    this.invalidate();
  }
  value(key: string, value: string | number) { this.drafts[key] = String(value ?? ''); this.invalidate(); }
  private invalidate() { this.preview.set(null); this.reviewedInput = null; this.confirmed = false; }
  private input(): SystemMigrationInput {
    const target = this.destination(); if (!target || !this.character.systemVersionId) throw new Error('Escolha uma versão de destino.');
    const values: Record<string, SheetFieldValue> = {};
    for (const field of this.editableFields().filter(f => this.adjusting[f.key])) {
      const text = this.drafts[field.key] || '';
      if (field.type === 'number' && (!text.trim() || !Number.isSafeInteger(Number(text)))) throw new Error(`Informe um inteiro em ${field.label}.`);
      values[field.key] = field.type === 'number' ? Number(text) : field.type === 'boolean' ? text === 'true' : field.type === 'table' || field.type === 'list' ? JSON.parse(text) : text;
    }
    return { expectedVersion: this.character.version, sourceVersionId: this.character.systemVersionId, targetVersionId: target.current ? null : target.versionId,
      targetRevision: target.revision, targetHash: target.contentHash, mappings: { ...this.mappings }, values };
  }
  review() {
    if (!this.available || this.busy()) return;
    this.invalidate(); this.error.set(''); let input: SystemMigrationInput;
    try { input = this.input(); } catch (e) { this.error.set(e instanceof Error ? e.message : 'Confira os valores informados.'); return; }
    this.busy.set(true);
    this.api.previewSystemMigration(this.character.id, input).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: result => { if (input.expectedVersion !== this.character.version) { this.error.set('A ficha mudou. Gere uma nova prévia.'); return; } this.preview.set(result); this.reviewedInput = input; }, error: e => this.error.set(apiError(e))
    });
  }
  apply() {
    const hash = this.preview()?.previewHash;
    if (!this.confirmed || !hash || !this.reviewedInput || !this.available || this.busy()) return;
    this.busy.set(true); this.applying = true; this.error.set('');
    this.api.applySystemMigration(this.character.id, this.reviewedInput, hash).pipe(finalize(() => { this.busy.set(false); this.applying = false; })).subscribe({
      next: item => this.migrated.emit(item), error: e => { this.invalidate(); this.error.set(apiError(e)); }
    });
  }
  display(value: unknown) { return value === null || value === undefined ? 'Sem valor' : typeof value === 'object' ? JSON.stringify(value) : String(value); }
  describe(field: SystemField) { return `${field.type}${field.min != null ? ' · mín. ' + field.min : ''}${field.max != null ? ' · máx. ' + field.max : ''}${field.formula ? ' · ' + field.formula : ''}${field.options?.length ? ' · ' + field.options.join(', ') : ''}`; }
  kind(kind: string) { return ({ added: 'Novo', retired: 'Retirado do modelo', definition: 'Definição alterada', value: 'Valor alterado' } as Record<string, string>)[kind] || kind; }
  errors() { return Object.values(this.preview()?.plan.errors || {}).flat(); }
}
