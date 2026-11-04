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
      <label class="hostile-confirm" *ngIf="system.templateId==='dnd_5e' && system.templateVersion===5"><input type="checkbox" aria-label="Alvo dos ataques é criatura hostil" [(ngModel)]="hostile" [ngModelOptions]="{standalone:true}"> Alvo é criatura hostil</label>
      <ng-container *ngIf="barbarianAttacks()">
        <label class="hostile-confirm"><input type="checkbox" aria-label="Ataque fora do meu turno" [(ngModel)]="outsideTurn" (ngModelChange)="reckless=false" [ngModelOptions]="{standalone:true}"> Fora do meu turno (reação)</label>
        <label class="hostile-confirm"><input type="checkbox" aria-label="Usar Ataque Descuidado no primeiro ataque" [(ngModel)]="reckless" [ngModelOptions]="{standalone:true}" [disabled]="outsideTurn || ruleState('ataques_turno')>0"> Ataque Descuidado no primeiro ataque</label>
        <label *ngIf="ruleState('nivel_barbaro')>=9">Dado da arma para Crítico Brutal<select aria-label="Dado da arma para Crítico Brutal" [(ngModel)]="brutalDie" [ngModelOptions]="{standalone:true}"><option [ngValue]="0">Automático</option><option *ngFor="let die of weaponDice" [ngValue]="die">d{{ die }}</option></select></label>
      </ng-container>
      <label *ngIf="system.templateId === 'call_of_cthulhu_7e'">Dificuldade<select [(ngModel)]="difficulty" [ngModelOptions]="{standalone:true}"><option [ngValue]="1">Regular</option><option [ngValue]="2">Difícil</option><option [ngValue]="5">Extrema</option></select></label>
      <label>Visibilidade<select [(ngModel)]="visibility" [ngModelOptions]="{standalone:true}"><option value="campaign">Mesa</option><option value="private">Privada</option></select></label>
    </div>
    <p *ngIf="barbarianAttacks() && ruleState('descuidado_exposto')" role="status">Ataque Descuidado: ataques contra você têm vantagem até o início do seu próximo turno. {{ ruleState('descuidado_turno') && !outsideTurn ? 'Seus ataques com arma corpo a corpo e FOR recebem vantagem neste turno.' : '' }}</p>
    <p *ngIf="barbarianAttacks() && ruleState('nivel_barbaro')>=9">Crítico Brutal acrescenta dados da arma em críticos corpo a corpo. Informe somente o dano da arma em Dano base; efeitos adicionais pertencem a Dano extra.</p>
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
.weapon-config input[type=checkbox]{width:15px;min-height:15px;height:15px;margin:0;padding:0}.weapon-config label.hostile-confirm{display:flex;align-items:center;gap:5px}
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
  baseOptions() { return {...numericColumnOptions(this.system.fields),...(this.system.templateId==='dnd_5e'?{tipo:[{value:'corpo',label:'Arma corpo a corpo'},{value:'distancia',label:'Arma a distância ou arremessada'}]}:{})}; }
  hostile=false;
  reckless=false; outsideTurn=false; brutalDie=0; weaponDice=[3,4,6,8,10,12,20,100];
  ruleState(key:string){const rows=this.draft.fields['estado_regras'];if(!Array.isArray(rows))return 0;const row=rows.find(row=>row && typeof row==='object' && row['chave']===key);return row && typeof row==='object' && typeof row['valor']==='number'?row['valor']:0;}
  barbarianAttacks(){return this.system.templateId==='dnd_5e' && this.system.templateVersion===5 && this.system.rulesetVersion==='2014' && this.ruleState('nivel_barbaro')>=2;}
  rows(): SheetRow[] { const value = this.draft.fields['ataques_regras']; return Array.isArray(value) && value.every(row => row && typeof row === 'object' && !Array.isArray(row)) ? value as SheetRow[] : []; }
  text(row: SheetRow, key: string) { return typeof row[key] === 'string' ? row[key] as string : ''; }
  baseLabel(row: SheetRow) { return this.system.fields.find(field => field.key === this.text(row, 'base'))?.label || 'Teste de ataque'; }
  ammo(row: SheetRow) { return typeof row['municao'] === 'number' ? row['municao'] : -1; }
  attack(index: number) {
    if (!this.character.canEdit || !this.ready || this.busy()) return;
    this.busy.set(true); this.working.emit(true); this.error.set(''); this.message.set(''); this.rolls.set([]);
    this.api.applyCharacterRule(this.character.id, this.character.version, { action: 'attack', amount: this.system.templateId === 'call_of_cthulhu_7e' ? this.difficulty : this.target, option: String(index), visibility: this.visibility, ...(this.system.templateId==='dnd_5e' && this.hostile?{expression:'hostile'}:{}),...(this.barbarianAttacks()?{weapon:{reckless:this.reckless,outsideTurn:this.outsideTurn,brutalDie:this.brutalDie}}:{}) })
      .pipe(finalize(() => { this.busy.set(false); this.working.emit(false); })).subscribe({ next: result => { this.reckless=false;this.message.set(result.message); this.rolls.set(result.rolls); this.applied.emit(result.character); }, error: error => this.error.set(apiError(error)) });
  }
}
