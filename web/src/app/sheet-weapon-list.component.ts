import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ApiService, Character, CharacterInput, RpgSystem, SheetCellValue, apiError } from './api';
import { SheetCollectionFieldComponent, numericColumnOptions } from './sheet-collection-field.component';
type SheetRow = Record<string, SheetCellValue>;

@Component({ selector: 'app-sheet-weapon-list', standalone: true, imports: [CommonModule, FormsModule, SheetCollectionFieldComponent], template: `
  <section *ngIf="field()" class="weapon-list" aria-label="Ataques do personagem">
    <div *ngIf="mode !== 'print' && rows().length" class="weapon-config"><label *ngIf="system.templateId !== 'call_of_cthulhu_7e'">Defesa do alvo<input type="number" aria-label="Defesa do alvo dos ataques" [(ngModel)]="target" [ngModelOptions]="{standalone:true}" min="0" max="9999"></label>
      <label *ngIf="system.templateId === 'call_of_cthulhu_7e'">Dificuldade<select [(ngModel)]="difficulty" [ngModelOptions]="{standalone:true}"><option [ngValue]="1">Regular</option><option [ngValue]="2">Difícil</option><option [ngValue]="5">Extrema</option></select></label>
      <label>Visibilidade<select [(ngModel)]="visibility" [ngModelOptions]="{standalone:true}"><option value="campaign">Mesa</option><option value="private">Privada</option></select></label>
    </div>
    <div *ngFor="let row of rows(); let index=index" class="weapon-row"><div><strong>{{ text(row,'nome') || 'Ataque sem nome' }}</strong><small>{{ baseLabel(row) }}<span *ngIf="ammo(row) >= 0"> · munição {{ ammo(row) }}</span></small></div><div class="damage"><b>{{ text(row,'dano') || '1' }}</b><small *ngIf="text(row,'dano_extra')">+ {{ text(row,'dano_extra') }} extra</small></div>
      <button *ngIf="mode !== 'print' && character.canEdit" type="button" [disabled]="!ready || busy() || ammo(row) === 0 || !text(row,'nome')" [attr.aria-label]="'Atacar com ' + text(row,'nome')" (click)="attack(index)">{{ busy() ? '…' : 'Rolar' }}</button>
    </div>
    <p *ngIf="!rows().length">Nenhum ataque registrado neste modelo.</p>
    <p *ngIf="message()" role="status">{{ message() }}</p><p *ngIf="error()" role="alert" class="form-error">{{ error() }}</p>
    <details *ngIf="rolls().length"><summary>Dados e composição</summary><p *ngFor="let roll of rolls()"><code>{{ roll.expression }} [{{ roll.rolls.join(', ') }}] = {{ roll.total }}</code></p></details>
    <details *ngIf="mode === 'edit' && character.canEdit && (field()?.editRole !== 'gm' || isMaster) && field() as attackField" class="weapon-manager"><summary>Editar ataques e munição</summary><p>Base: chave de um modificador ou perícia da ficha. Munição −1 indica ataque sem limite. Dano base e extra são calculados separadamente conforme o sistema.</p><app-sheet-collection-field [field]="attackField" [columnOptions]="baseOptions()" [draft]="draft" [campaignId]="character.campaignId" mode="edit" (changed)="changed.emit()" /></details>
  </section>
`, styles: [`
  :host{display:block;min-width:0}.weapon-list{min-width:0;color:inherit;font:11px Arial,sans-serif}.weapon-config{display:flex;gap:8px;flex-wrap:wrap;margin-bottom:7px}.weapon-config label{display:grid;gap:3px;font-size:9px;color:inherit}.weapon-config input,.weapon-config select{width:110px;min-height:27px;padding:3px 5px;border:1px solid currentColor;border-radius:2px;background:var(--surface-raised,#fff);color:var(--text,inherit)}.weapon-row{display:grid;grid-template-columns:minmax(0,1fr) minmax(55px,.65fr) 46px;align-items:center;gap:7px;padding:8px 5px;border-block-end:1px solid currentColor}.weapon-row>div{min-width:0}.weapon-row strong,.weapon-row b{display:block;overflow-wrap:anywhere}.weapon-row small{display:block;font-size:9px;opacity:.8;margin-top:3px;overflow-wrap:anywhere}.weapon-row button{padding:5px 3px;min-height:28px;border:1px solid currentColor;border-radius:3px;background:transparent;color:inherit;font:700 10px Arial,sans-serif;cursor:pointer}.weapon-row button:disabled{opacity:.45;cursor:default}.weapon-row button:focus-visible{outline:2px solid currentColor;outline-offset:3px}.weapon-manager,details{margin-top:8px}summary{font-weight:700;cursor:pointer}p{font-size:11px;line-height:1.5;overflow-wrap:anywhere}code{white-space:normal;overflow-wrap:anywhere}@media(max-width:500px){.weapon-config{display:grid;grid-template-columns:1fr 1fr}.weapon-config input,.weapon-config select{width:100%;min-width:0}.weapon-row{grid-template-columns:minmax(0,1fr) minmax(60px,.7fr) 43px}}@media print{.weapon-config,.weapon-row button,.weapon-manager{display:none}}
`] })
export class SheetWeaponListComponent {
  @Input({ required: true }) character!: Character;
  @Input({ required: true }) draft!: CharacterInput;
  @Input({ required: true }) system!: RpgSystem;
  @Input() ready = false;
  @Input() isMaster = false;
  @Input() mode: 'read' | 'edit' | 'print' = 'read';
  @Output() working = new EventEmitter<boolean>();
  @Output() applied = new EventEmitter<Character>();
  @Output() changed = new EventEmitter<void>();
  private api = inject(ApiService);
  busy = signal(false); message = signal(''); error = signal('');
  rolls = signal<{ expression: string; rolls: number[]; total: number }[]>([]);
  target = 0; difficulty = 1; visibility: 'campaign' | 'private' = 'campaign';
  field() { return this.system.fields.find(field => field.key === 'ataques_regras'); }
  baseOptions() { return numericColumnOptions(this.system.fields); }
  rows(): SheetRow[] { const value = this.draft.fields['ataques_regras']; return Array.isArray(value) && value.every(row => row && typeof row === 'object' && !Array.isArray(row)) ? value as SheetRow[] : []; }
  text(row: SheetRow, key: string) { return typeof row[key] === 'string' ? row[key] as string : ''; }
  baseLabel(row: SheetRow) { return this.system.fields.find(field => field.key === this.text(row, 'base'))?.label || 'Teste de ataque'; }
  ammo(row: SheetRow) { return typeof row['municao'] === 'number' ? row['municao'] : -1; }
  attack(index: number) {
    if (!this.character.canEdit || !this.ready || this.busy()) return;
    this.busy.set(true); this.working.emit(true); this.error.set(''); this.message.set(''); this.rolls.set([]);
    this.api.applyCharacterRule(this.character.id, this.character.version, { action: 'attack', amount: this.system.templateId === 'call_of_cthulhu_7e' ? this.difficulty : this.target, option: String(index), visibility: this.visibility })
      .pipe(finalize(() => { this.busy.set(false); this.working.emit(false); })).subscribe({ next: result => { this.message.set(result.message); this.rolls.set(result.rolls); this.applied.emit(result.character); }, error: error => this.error.set(apiError(error)) });
  }
}
