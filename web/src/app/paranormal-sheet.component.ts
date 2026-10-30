import { D20RuleRollContext } from './d20-rule-roll-context';
import { d20Defense, d20Speed, hasD20Conditions } from './d20-condition-display';
import { SheetWeaponListComponent } from './sheet-weapon-list.component';
import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ApiService, Character, CharacterInput, RpgSystem, SheetCellValue, SheetFieldValue, SystemField, apiError } from './api';
import { CharacterAbilitiesComponent } from './character-abilities.component';
import { CharacterHistoryPanelComponent } from './character-history.component';
import { CharacterInventoryComponent } from './character-inventory.component';
import { SheetCollectionFieldComponent } from './sheet-collection-field.component';
import { SystemFieldComponent } from './specialized-sheet.component';
import { resourceMaximum, paranormalFieldRoll, paranormalV13BaseResources } from './sheet-rules';
import { SHEET_TEMPLATES } from './system-templates';

type SheetMode = 'read' | 'edit' | 'print';
type ResourceKey = 'health' | 'sanity' | 'mana';
type Visibility = 'campaign' | 'private';

@Component({ selector: 'app-paranormal-roll', standalone: true, imports: [CommonModule], template: `
  <button type="button" class="paranormal-roll" [disabled]="busy() || !expression" [attr.aria-label]="'Rolar ' + label" [title]="'Rolar ' + label" (click)="roll()">{{ busy() ? '…' : '◈' }}</button>
`, styles: [`.paranormal-roll{width:27px;height:27px;display:grid;place-items:center;padding:0;border:1px solid #a54d52;border-radius:50%;background:#4b171e;color:#ffe3d5;cursor:pointer;font-size:16px}.paranormal-roll:hover,.paranormal-roll:focus-visible{background:#a54d52;outline:2px solid #e6a29c;outline-offset:2px}.paranormal-roll:disabled{opacity:.45;cursor:default}@media print{.paranormal-roll{display:none}}`]
})
export class ParanormalRollComponent {
  @Input() campaignId = '';
  @Input() expression = '';
  @Input() values: Record<string, SheetFieldValue> = {};
  @Input() label = 'teste';
  @Input() visibility: Visibility = 'campaign';
  @Output() rolled = new EventEmitter<string>();
  private api = inject(ApiService);
  private context = inject(D20RuleRollContext, { optional: true });
  busy = signal(false);
  roll() {
    if (this.context?.roll(this.expression, this.visibility, this.label, message => this.rolled.emit(message), busy => this.busy.set(busy))) return;
    const expression = paranormalFieldRoll(this.expression, this.values);
    if (!expression) { this.rolled.emit(`${this.label}: confira os atributos e a expressão da rolagem.`); return; }
    this.busy.set(true);
    this.api.roll(this.campaignId, expression, this.visibility, crypto.randomUUID()).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: result => this.rolled.emit(`${this.label}: ${result.total} · ${result.expression} [${result.rolls.join(', ')}]`),
      error: error => this.rolled.emit(`${this.label}: ${apiError(error)}`)
    });
  }
}

@Component({ selector: 'app-paranormal-attribute-wheel', standalone: true, imports: [CommonModule, FormsModule, ParanormalRollComponent], template: `
  <div class="attribute-wheel" aria-label="Atributos do agente">
    <svg class="wheel-lines" viewBox="0 0 320 320" aria-hidden="true"><circle cx="160" cy="160" r="121"/><path d="M160 39 277 123 232 260 88 260 43 123Z"/><path d="M160 39 160 160 277 123 M232 260 160 160 88 260 M43 123 160 160"/></svg>
    <div class="wheel-core" aria-hidden="true"><span>CRÔNICAS</span><b>AGENTE</b></div>
    <div *ngFor="let item of attributes" class="attribute-node" [style.left.%]="item.x" [style.top.%]="item.y">
      <span class="attribute-name">{{ item.short }}</span>
      <input *ngIf="canEdit(item.key); else score" type="number" [attr.aria-label]="item.label" [ngModel]="value(item.key)" (ngModelChange)="set(item.key,$event)" [ngModelOptions]="{standalone:true}" min="0" max="20">
      <ng-template #score><strong>{{ value(item.key) }}</strong></ng-template>
      <span class="attribute-label">{{ item.label }}</span>
      <app-paranormal-roll *ngIf="mode !== 'print'" [campaignId]="character.campaignId" [expression]="field(item.key)?.rollExpression || ''" [values]="draft.fields" [label]="item.label" [visibility]="visibility" (rolled)="rolled.emit($event)" />
    </div>
  </div>
`, styles: [`
  .attribute-wheel{width:min(100%,336px);aspect-ratio:1;margin:0 auto;position:relative;isolation:isolate}
  .wheel-lines{position:absolute;inset:0;width:100%;height:100%;fill:none;stroke:#85404b;stroke-width:1.2;opacity:.82}
  .wheel-lines circle{stroke-dasharray:3 5;stroke:#7b6266}.wheel-lines path:last-child{stroke:#77333c;opacity:.65}
  .wheel-core{position:absolute;left:50%;top:50%;transform:translate(-50%,-50%);width:77px;height:77px;border:1px solid #865058;border-radius:50%;display:grid;align-content:center;text-align:center;background:#171418;color:#c99894;box-shadow:0 0 0 8px #171418}
  .wheel-core span{font-size:8px;letter-spacing:.12em}.wheel-core b{font-size:13px;letter-spacing:.12em}
  .attribute-node{position:absolute;transform:translate(-50%,-50%);width:83px;height:83px;border:2px solid #ad6064;border-radius:50%;background:radial-gradient(circle at 50% 20%,#513036,#211b20 70%);box-shadow:0 0 0 5px #171418,0 4px 12px #0008;display:flex;align-items:center;justify-content:center;flex-direction:column;color:#f5e8de;z-index:1}
  .attribute-name{font-size:10px;font-weight:800;letter-spacing:.15em;color:#edaaa1}.attribute-node strong,.attribute-node input{font:700 27px Georgia,serif;line-height:1;color:#fff;text-align:center}.attribute-node input{width:48px;background:transparent;border:0;border-bottom:1px solid #ce8d87;outline-offset:2px}.attribute-label{font-size:8px;line-height:1.1;text-transform:uppercase;letter-spacing:.06em;color:#cbb8b3}
  .attribute-node app-paranormal-roll{position:absolute;right:-8px;bottom:1px}.attribute-node:focus-within{z-index:3}
  @media(max-width:480px){.attribute-wheel{width:min(100%,310px)}.attribute-node{width:75px;height:75px}.attribute-node strong,.attribute-node input{font-size:24px}}
`]
})
export class ParanormalAttributeWheelComponent {
  @Input({ required: true }) system!: RpgSystem;
  @Input({ required: true }) draft!: CharacterInput;
  @Input({ required: true }) character!: Character;
  @Input() mode: SheetMode = 'read';
  @Input() visibility: Visibility = 'campaign';
  @Output() changed = new EventEmitter<void>();
  @Output() rolled = new EventEmitter<string>();
  attributes = [
    {key:'agilidade',short:'AGI',label:'Agilidade',x:50,y:12},
    {key:'forca',short:'FOR',label:'Força',x:86.5,y:38.5},
    {key:'vigor',short:'VIG',label:'Vigor',x:72.5,y:81.5},
    {key:'presenca',short:'PRE',label:'Presença',x:27.5,y:81.5},
    {key:'intelecto',short:'INT',label:'Intelecto',x:13.5,y:38.5}
  ];
  field(key: string) { return this.system.fields.find(item => item.key === key); }
  value(key: string) { const value = this.draft.fields[key]; return typeof value === 'number' ? value : 0; }
  canEdit(key: string) { return this.mode === 'edit' && this.character.canEdit && !this.field(key)?.formula; }
  set(key: string, value: number) { if (!this.canEdit(key)) return; this.draft.fields[key] = Number(value); this.changed.emit(); }
}

@Component({ selector: 'app-paranormal-resource', standalone: true, imports: [CommonModule, FormsModule], template: `
  <div class="paranormal-resource" [class.health]="key === 'health'" [class.sanity]="key === 'sanity'" [class.effort]="key === 'mana'">
    <div class="resource-top"><span>{{ label }}</span><strong>{{ draft[key] }}<small> / {{ draft[maxKey()] }}</small></strong></div>
    <div class="resource-track" role="meter" [attr.aria-label]="label" [attr.aria-valuenow]="draft[key]" aria-valuemin="0" [attr.aria-valuemax]="draft[maxKey()]"><i [style.width.%]="percent()"></i></div>
    <div *ngIf="mode !== 'print' && canEdit" class="resource-controls"><button type="button" [disabled]="draft[key] <= 0" [attr.aria-label]="'Diminuir ' + label" (click)="adjust(-1)">−</button><button type="button" [disabled]="draft[key] >= draft[maxKey()]" [attr.aria-label]="'Aumentar ' + label" (click)="adjust(1)">+</button><input *ngIf="mode === 'edit'" type="number" [attr.aria-label]="'Valor atual de ' + label" [ngModel]="draft[key]" (ngModelChange)="setCurrent($event)" [ngModelOptions]="{standalone:true}" min="0" [max]="draft[maxKey()]"><label *ngIf="mode === 'edit'">Máx. <input type="number" [attr.aria-label]="'Máximo de ' + label" [ngModel]="draft[maxKey()]" (blur)="setMax($any($event.target).value)" [ngModelOptions]="{standalone:true}" min="0" max="9999"></label></div>
  </div>
`, styles: [`
  .paranormal-resource{--meter:#bc5b5d;padding:9px 11px;background:#201b20;border:1px solid #554048;min-width:0}.paranormal-resource.sanity{--meter:#777ab5}.paranormal-resource.effort{--meter:#af9655}
  .resource-top{display:flex;align-items:baseline;justify-content:space-between;gap:8px}.resource-top span{text-transform:uppercase;letter-spacing:.12em;font-size:10px;font-weight:800;color:#d5c4c0}.resource-top strong{font:700 21px Georgia,serif}.resource-top small{font:12px Arial,sans-serif;color:#a89898}
  .resource-track{height:8px;margin-top:6px;background:#352930;border:1px solid #53464b;overflow:hidden}.resource-track i{display:block;height:100%;background:var(--meter);box-shadow:0 0 10px var(--meter);transition:width .2s}
  .resource-controls{display:flex;gap:5px;align-items:center;margin-top:7px}.resource-controls button{width:26px;height:24px;border:1px solid #80666a;color:#f0dfd7;background:#35252d;cursor:pointer}.resource-controls button:disabled{opacity:.4;cursor:default}.resource-controls input{width:48px;margin:0;padding:2px;color:#fff;background:#1b171a;border:1px solid #80666a}.resource-controls label{margin-left:auto;font-size:10px;color:#bbaaaa;display:flex;align-items:center;gap:4px}
  @media print{.resource-controls{display:none}}
`]
})
export class ParanormalResourceBarComponent {
  @Input({ required: true }) draft!: CharacterInput;
  @Input({ required: true }) key!: ResourceKey;
  @Input() label = '';
  @Input() canEdit = false;
  @Input() mode: SheetMode = 'read';
  @Output() changed = new EventEmitter<void>();
  maxKey(): 'maxHealth' | 'maxSanity' | 'maxMana' { return this.key === 'health' ? 'maxHealth' : this.key === 'sanity' ? 'maxSanity' : 'maxMana'; }
  percent() { return Math.max(0, Math.min(100, this.draft[keyOf(this.key)] / this.draft[this.maxKey()] * 100)); }
  adjust(step: number) { if (!this.canEdit) return; this.draft[this.key] = Math.max(0, Math.min(this.draft[this.maxKey()], this.draft[this.key] + step)); this.changed.emit(); }
  setCurrent(value: number) { if (!this.canEdit) return; this.draft[this.key] = Math.max(0, Math.min(this.draft[this.maxKey()], Number(value) || 0)); this.changed.emit(); }
  setMax(value: number) { const max = resourceMaximum(value); if (max === null || !this.canEdit) return; this.draft[this.maxKey()] = max; this.draft[this.key] = Math.min(this.draft[this.key], this.draft[this.maxKey()]); this.changed.emit(); }
}

function keyOf(key: ResourceKey): ResourceKey { return key; }

@Component({ selector: 'app-paranormal-skill-table', standalone: true, imports: [CommonModule, FormsModule, ParanormalRollComponent], template: `
  <div class="paranormal-skills" role="region" aria-label="Perícias do agente">
    <div class="skill-head"><span>PERÍCIA</span><span>ATR</span><span>DADOS</span><span>BÔNUS</span><span>TREINO</span><span>OUTROS</span><span>ROLAR</span></div>
    <div *ngFor="let skill of skills()" class="skill-row">
      <span class="skill-name">{{ skill.label }}</span><span class="skill-attr">{{ attributeShort(skill) }}</span><span class="skill-dice" [title]="skillPreview(skill)">{{ skillDiceLabel(skill) }}</span>
      <input *ngIf="canEdit(skill); else baseValue" type="number" [attr.aria-label]="'Bônus base de ' + skill.label" [ngModel]="number(skill.key)" (ngModelChange)="setBase(skill,$event)" [ngModelOptions]="{standalone:true}" min="-999" max="999">
      <ng-template #baseValue><strong class="skill-number">{{ signed(number(skill.key)) }}</strong></ng-template>
      <input *ngIf="canEditDetail(); else trainingValue" type="number" [attr.aria-label]="'Treino de ' + skill.label" [ngModel]="detail(skill.key,'treino')" (ngModelChange)="setDetail(skill.key,'treino',$event)" [ngModelOptions]="{standalone:true}" min="-99" max="99">
      <ng-template #trainingValue><span class="skill-number">{{ signed(detail(skill.key,'treino')) }}</span></ng-template>
      <input *ngIf="canEditDetail(); else otherValue" type="number" [attr.aria-label]="'Outros modificadores de ' + skill.label" [ngModel]="detail(skill.key,'outros')" (ngModelChange)="setDetail(skill.key,'outros',$event)" [ngModelOptions]="{standalone:true}" min="-99" max="99">
      <ng-template #otherValue><span class="skill-number">{{ signed(detail(skill.key,'outros')) }}</span></ng-template>
      <app-paranormal-roll *ngIf="mode !== 'print'" [campaignId]="character.campaignId" [expression]="skill.rollExpression || ''" [values]="rollValues(skill)" [label]="skill.label" [visibility]="visibility" (rolled)="rolled.emit($event)" />
    </div>
  </div>
`, styles: [`
  .paranormal-skills{border:1px solid #60464a;background:#1b181d;max-width:100%;overflow-x:auto}
  .skill-head,.skill-row{display:grid;grid-template-columns:minmax(118px,1fr) 38px 47px 48px 48px 49px 34px;align-items:center;gap:2px;padding:2px 7px;min-width:390px}
  .skill-head{height:31px;background:#40232b;color:#e8bbb4;font-size:8px;letter-spacing:.07em;font-weight:800;text-align:center}.skill-head span:first-child{text-align:left}
  .skill-row{min-height:29px;border-top:1px solid #3a2d34;font-size:11px;color:#e9ded9}.skill-row:nth-child(odd){background:#211c21}.skill-row:hover{background:#30242b}
  .skill-name{white-space:nowrap;overflow:hidden;text-overflow:ellipsis;font-weight:650}.skill-attr,.skill-dice{color:#a7a1a9;text-align:center;font-size:10px}.skill-number{text-align:center;font-variant-numeric:tabular-nums}.skill-row input{width:100%;min-width:0;height:23px;padding:1px;color:#fff;background:#281e24;border:1px solid #745158;text-align:center;font-size:10px}.skill-row app-paranormal-roll{justify-self:center}
  @media(max-width:620px){.skill-head,.skill-row{grid-template-columns:minmax(90px,1fr) 27px 36px 34px 34px 34px 27px;min-width:0;gap:1px;padding-inline:3px}.skill-head{font-size:7px}}
`]
})
export class ParanormalSkillTableComponent {
  private context = inject(D20RuleRollContext, {optional: true});
  @Input({ required: true }) system!: RpgSystem;
  @Input({ required: true }) draft!: CharacterInput;
  @Input({ required: true }) character!: Character;
  @Input() mode: SheetMode = 'read';
  @Input() visibility: Visibility = 'campaign';
  @Output() changed = new EventEmitter<void>();
  @Output() rolled = new EventEmitter<string>();
  skills() { return this.system.fields.filter(field => field.section === 'Perícias' && field.key.startsWith('per_') && field.type === 'number'); }
  number(key: string) { const value = this.draft.fields[key]; return typeof value === 'number' ? value : 0; }
  signed(value: number) { return value > 0 ? `+${value}` : `${value}`; }
  attributeKey(skill: SystemField) { return /^\{([a-z][a-z0-9_]*)\}d20/.exec(skill.rollExpression || '')?.[1] || ''; }
  attributeShort(skill: SystemField) { return ({ agilidade:'AGI', forca:'FOR', intelecto:'INT', presenca:'PRE', vigor:'VIG' } as Record<string,string>)[this.attributeKey(skill)] || '—'; }
  attributeValue(skill: SystemField) { return this.number(this.attributeKey(skill)); }
  skillPreview(skill: SystemField) { const preview = this.context?.checks()[skill.key]; return preview?.error || preview?.expression || ''; }
  skillDiceLabel(skill: SystemField) { const preview = this.mode === 'edit' ? null : this.context?.checks()[skill.key]; if (preview) { if (!preview.expression) return '?'; const match = /^(\d+d20)(kl1|kh1)?/.exec(preview.expression); if (match) return match[1] + (match[2] === 'kl1' ? '\u2193' : ''); } const count = this.attributeValue(skill); return count === 0 ? '2d20↓' : `${count}d20`; }
  private details(): Record<string, SheetCellValue>[] { const value = this.draft.fields['pericias_detalhes']; return Array.isArray(value) && value.every(row => row && typeof row === 'object' && !Array.isArray(row)) ? value as Record<string, SheetCellValue>[] : []; }
  detail(key: string, column: 'treino' | 'outros') { const row = this.details().find(item => item['chave'] === key); return typeof row?.[column] === 'number' ? row[column] as number : 0; }
  canEdit(skill: SystemField) { return this.mode === 'edit' && this.character.canEdit && !skill.formula && skill.editRole !== 'gm'; }
  canEditDetail() { return this.mode === 'edit' && this.character.canEdit && this.system.fields.some(field => field.key === 'pericias_detalhes'); }
  setBase(skill: SystemField, value: number) { if (!this.canEdit(skill)) return; this.draft.fields[skill.key] = Number(value) || 0; this.changed.emit(); }
  setDetail(key: string, column: 'treino' | 'outros', value: number) { if (!this.canEditDetail()) return; const rows = this.details().map(row => ({ ...row })); let row = rows.find(item => item['chave'] === key); if (!row) { row = { chave: key, treino: 0, outros: 0 }; rows.push(row); } row[column] = Number(value) || 0; this.draft.fields['pericias_detalhes'] = rows; this.changed.emit(); }
  rollValues(skill: SystemField) { return { ...this.draft.fields, [skill.key]: this.number(skill.key) + this.detail(skill.key,'treino') + this.detail(skill.key,'outros') }; }
}

@Component({ selector: 'app-paranormal-sheet', standalone: true, providers: [D20RuleRollContext], imports: [SheetWeaponListComponent, CommonModule, FormsModule, ParanormalAttributeWheelComponent, ParanormalResourceBarComponent, ParanormalSkillTableComponent, SystemFieldComponent, SheetCollectionFieldComponent, CharacterInventoryComponent, CharacterAbilitiesComponent, CharacterHistoryPanelComponent], template: `
  <article class="paranormal-sheet">
    <header class="paranormal-header">
      <div class="paranormal-portrait"><img *ngIf="draft.portraitUrl" [src]="draft.portraitUrl" alt=""><span *ngIf="!draft.portraitUrl">{{ draft.name.slice(0,1) || 'C' }}</span></div>
      <div class="paranormal-identity"><small>CRÔNICAS · ARQUIVO DO AGENTE</small><h1 *ngIf="mode !== 'edit'">{{ draft.name }}</h1><input *ngIf="mode === 'edit'" aria-label="Nome do agente" [ngModel]="draft.name" (ngModelChange)="draft.name=$event; changed.emit()" [ngModelOptions]="{standalone:true}" maxlength="120"><div class="paranormal-byline"><span *ngFor="let key of ['origem','classe','trilha']"><b>{{ label(key) }}</b><ng-container *ngIf="mode !== 'edit'">{{ textValue(key) || '—' }}</ng-container><input *ngIf="mode === 'edit'" [attr.aria-label]="label(key)" [ngModel]="textValue(key)" (ngModelChange)="setField(key,$event)" [ngModelOptions]="{standalone:true}"></span></div></div>
      <div class="paranormal-nex"><span>NEX</span><strong *ngIf="mode !== 'edit'">{{ number('nex') }}<small>%</small></strong><input *ngIf="mode === 'edit'" type="number" aria-label="NEX" [ngModel]="number('nex')" (ngModelChange)="setField('nex',+$event)" [ngModelOptions]="{standalone:true}" min="0" max="100"></div>
    </header>
    <div class="paranormal-controls"><nav aria-label="Seções da ficha"><button *ngFor="let item of tabs" type="button" [class.active]="active() === item.id" (click)="active.set(item.id)">{{ item.label }}</button><button *ngIf="extraFields().length" type="button" [class.active]="active() === 'extras'" (click)="active.set('extras')">Extras</button></nav><label *ngIf="mode !== 'print'">Rolagem <select [ngModel]="visibility()" (ngModelChange)="visibility.set($event)" [ngModelOptions]="{standalone:true}" aria-label="Visibilidade das rolagens"><option value="campaign">Mesa</option><option value="private">Privada</option></select></label></div>
    <div *ngIf="lastRoll() && mode !== 'print'" class="paranormal-last-roll" role="status">{{ lastRoll() }}</div>
    <div *ngIf="show('principal')" class="paranormal-primary">
      <div class="paranormal-left"><div class="paranormal-block-label">ATRIBUTOS</div><app-paranormal-attribute-wheel [system]="system" [draft]="draft" [character]="character" [mode]="mode" [visibility]="visibility()" (changed)="changed.emit()" (rolled)="lastRoll.set($event)" />
        <div class="paranormal-facts"><div><span>NEX</span><strong>{{ number('nex') }}%</strong></div><div><span>DESLOCAMENTO</span><strong>{{ effectiveSpeed() }} m</strong></div><div><span>INICIATIVA</span><strong>{{ signed(number('iniciativa')) }}</strong></div></div>
        <div class="paranormal-resource-stack"><app-paranormal-resource [draft]="draft" key="health" label="Vida" [canEdit]="character.canEdit" [mode]="mode" (changed)="changed.emit()" /><app-paranormal-resource [draft]="draft" key="sanity" label="Sanidade" [canEdit]="character.canEdit" [mode]="mode" (changed)="changed.emit()" /><app-paranormal-resource [draft]="draft" key="mana" label="Esforço" [canEdit]="character.canEdit" [mode]="mode" (changed)="changed.emit()" /></div>
        <div *ngIf="field('classe_regra')" class="paranormal-base"><label>CLASSE PARA RECURSOS <select *ngIf="canEditField('classe_regra'); else selectedRuleClass" aria-label="Classe para recursos" [ngModel]="textValue('classe_regra')" (ngModelChange)="setField('classe_regra',$event)" [ngModelOptions]="{standalone:true}"><option value="">Selecionar</option><option value="combatente">Combatente</option><option value="especialista">Especialista</option><option value="ocultista">Ocultista</option></select><ng-template #selectedRuleClass><strong>{{ ruleClassLabel() || '—' }}</strong></ng-template></label><div *ngIf="baseResources() as base" class="paranormal-base-values"><span>BASE v1.3</span><b>PV {{ base.pv }}</b><b>PE {{ base.pe }}</b><b>SAN {{ base.san }}</b><button *ngIf="mode === 'edit' && character.canEdit" type="button" (click)="applyBaseResources()">Aplicar valores iniciais</button></div><small *ngIf="baseResources()">Ajuste manualmente bônus de origem, poderes e outras exceções.</small></div>
        <div class="paranormal-defense"><div class="paranormal-block-label">DEFESAS</div><div class="defense-values"><div *ngFor="let key of ['defesa','bloqueio','esquiva']"><span>{{ label(key) }}</span><input *ngIf="canEditField(key); else defenseValue" type="number" [attr.aria-label]="label(key)" [ngModel]="number(key)" (ngModelChange)="setField(key,+$event)" [ngModelOptions]="{standalone:true}"><ng-template #defenseValue><strong>{{ field(key) ? key === 'defesa' ? effectiveDefense() : number(key) : '—' }}</strong></ng-template></div></div></div>
        <div class="paranormal-secondary"><strong>PROTEÇÃO</strong><span>{{ textValue('protecao') || '—' }}</span><strong>RESISTÊNCIAS</strong><span>{{ textValue('resistencias') || '—' }}</span></div>
      </div>
      <div class="paranormal-right"><div class="paranormal-block-label">PERÍCIAS <small>{{ skillCount() }} TESTES</small></div><app-paranormal-skill-table [system]="system" [draft]="draft" [character]="character" [mode]="mode" [visibility]="visibility()" (changed)="changed.emit()" (rolled)="lastRoll.set($event)" /></div>
    </div>
    <section *ngIf="show('combate')" class="paranormal-detail"><h2>Combate</h2><div class="paranormal-combat-grid"><div><div class="paranormal-block-label">RECURSOS E DEFESAS</div><div class="paranormal-resource-stack"><app-paranormal-resource [draft]="draft" key="health" label="Vida" [canEdit]="character.canEdit" [mode]="mode" (changed)="changed.emit()" /><app-paranormal-resource [draft]="draft" key="mana" label="Esforço" [canEdit]="character.canEdit" [mode]="mode" (changed)="changed.emit()" /></div><div class="paranormal-secondary"><app-system-field *ngFor="let key of ['defesa','bloqueio','esquiva','protecao','resistencias','equip_defesa','outros_defesa']" [field]="field(key)" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" [isMaster]="isMaster" (changed)="changed.emit()" /></div></div><div><div class="paranormal-block-label">ATAQUES</div><app-sheet-weapon-list [system]="system" [character]="character" [draft]="draft" [ready]="rulesReady" [mode]="mode" [isMaster]="isMaster" (working)="working.emit($event)" (applied)="restored.emit($event)" (changed)="changed.emit()" /><app-sheet-collection-field *ngIf="(mode === 'edit' || !field('ataques_regras') || $any(draft.fields['ataques'])?.length ? field('ataques') : null) as attack" [field]="attack" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" (changed)="changed.emit()" /><app-system-field [field]="field('condicoes')" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" [isMaster]="isMaster" (changed)="changed.emit()" /></div></div></section>
    <section *ngIf="show('inventario')" class="paranormal-detail"><h2>Inventário</h2><div class="paranormal-inline-fields"><app-system-field *ngFor="let key of ['carga_atual','carga_limite','proficiencias']" [field]="field(key)" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" [isMaster]="isMaster" (changed)="changed.emit()" /></div><app-character-inventory [character]="character" /></section>
    <section *ngIf="show('habilidades')" class="paranormal-detail"><h2>Habilidades</h2><app-character-abilities [character]="character" /></section>
    <section *ngIf="show('rituais')" class="paranormal-detail"><h2>Rituais</h2><app-sheet-collection-field *ngIf="field('rituais') as ritual" [field]="ritual" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" (changed)="changed.emit()" /></section>
    <section *ngIf="show('anotacoes')" class="paranormal-detail"><h2>Anotações</h2><label>Histórico e vínculos<textarea *ngIf="mode === 'edit'" [ngModel]="textValue('vinculos')" (ngModelChange)="setField('vinculos',$event)" [ngModelOptions]="{standalone:true}" maxlength="10000" rows="8"></textarea><p *ngIf="mode !== 'edit'">{{ textValue('vinculos') || 'Nenhum registro.' }}</p></label><app-character-history *ngIf="mode !== 'print'" [character]="character" (restored)="restored.emit($event)" /></section>
    <section *ngIf="extraFields().length && show('extras')" class="paranormal-detail"><h2>Campos da mesa</h2><div class="paranormal-inline-fields"><app-system-field *ngFor="let item of extraFields()" [field]="item" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" [isMaster]="isMaster" (changed)="changed.emit()" /></div></section>
  </article>
`, styles: [`
  :host{display:block;min-width:0}.paranormal-sheet{--ps-border:#66454b;--ps-muted:#b3a1a4;--ps-accent:#c67673;color:#eee4df;background:#171419;border:1px solid #76515a;min-width:0;font-family:Arial,sans-serif}
  .paranormal-header{display:grid;grid-template-columns:76px minmax(0,1fr) 98px;gap:15px;align-items:center;min-height:103px;padding:11px 18px;background:linear-gradient(105deg,#311b25,#1e191f 64%);border-bottom:2px solid #a35259}
  .paranormal-portrait{width:76px;height:76px;border:2px solid #9c6164;display:grid;place-items:center;background:#251e24;color:#e0aaa3;font:700 36px Georgia,serif}.paranormal-portrait img{width:100%;height:100%;object-fit:cover}
  .paranormal-identity{min-width:0}.paranormal-identity small{font-size:9px;letter-spacing:.18em;font-weight:800;color:#d7918e}.paranormal-identity h1{font:600 clamp(23px,2.5vw,34px) Georgia,serif;margin:2px 0 9px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.paranormal-identity>input{font:600 25px Georgia,serif;width:100%;margin:3px 0;background:#21191f;color:#fff;border:1px solid #88555b}
  .paranormal-byline{display:flex;flex-wrap:wrap;gap:6px 17px;font-size:11px;color:#e3d6d2}.paranormal-byline span{display:flex;gap:5px;align-items:center}.paranormal-byline b{font-size:9px;letter-spacing:.08em;color:#b99191;text-transform:uppercase}.paranormal-byline input{width:100px;min-width:0;margin:0;padding:2px;background:#21191f;color:#fff;border:1px solid #88555b}
  .paranormal-nex{align-self:stretch;display:grid;place-items:center;align-content:center;border-left:1px solid #805359;color:#dfaca2}.paranormal-nex span{font-size:10px;font-weight:800;letter-spacing:.18em}.paranormal-nex strong{font:700 33px Georgia,serif;color:#fff}.paranormal-nex small{font:14px Arial,sans-serif;color:#d9958f}.paranormal-nex input{width:65px;text-align:center;background:#21191f;color:#fff;border:1px solid #88555b}
  .paranormal-controls{display:flex;align-items:center;justify-content:space-between;gap:8px;background:#221b22;border-bottom:1px solid #604047}.paranormal-controls nav{display:flex;overflow-x:auto;min-width:0}.paranormal-controls button{border:0;border-bottom:3px solid transparent;background:transparent;color:#bca8a8;padding:10px 12px;white-space:nowrap;text-transform:uppercase;letter-spacing:.07em;font-size:10px;font-weight:800;cursor:pointer}.paranormal-controls button.active,.paranormal-controls button:hover{border-color:#c67673;color:#fff;background:#3a242d}.paranormal-controls label{flex:none;display:flex;align-items:center;gap:5px;padding-right:10px;color:#baa7a6;font-size:10px}.paranormal-controls select{width:76px;margin:0;padding:3px;background:#21191f;color:#fff;border:1px solid #805359;font-size:10px}
  .paranormal-last-roll{padding:6px 14px;background:#513039;color:#fff;font-size:12px}
  .paranormal-primary{display:grid;grid-template-columns:minmax(305px,39%) minmax(0,61%);min-height:690px}.paranormal-left{padding:12px 15px;border-right:1px solid #5c4249;min-width:0}.paranormal-right{padding:12px 13px;min-width:0}
  .paranormal-block-label{display:flex;justify-content:space-between;align-items:baseline;border-bottom:1px solid #815159;padding:0 2px 6px;margin-bottom:8px;font-size:10px;font-weight:800;letter-spacing:.18em;color:#d7a49d}.paranormal-block-label small{font-size:9px;color:#ab9397;letter-spacing:.05em}
  .paranormal-facts{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:0;border:1px solid #57444a;margin:4px 0 11px}.paranormal-facts div{padding:6px 5px;text-align:center;border-right:1px solid #57444a}.paranormal-facts div:last-child{border:0}.paranormal-facts span{display:block;font-size:8px;font-weight:800;color:#b2999b;letter-spacing:.07em}.paranormal-facts strong{display:block;font:700 17px Georgia,serif;margin-top:3px}
  .paranormal-base{margin-top:7px;padding:7px 9px;border:1px solid #60454a;background:#211b20}.paranormal-base label{display:flex;align-items:center;justify-content:space-between;gap:8px;font-size:9px;font-weight:800;letter-spacing:.05em;color:#c99f9c}.paranormal-base label strong{font-size:11px;color:#f4e5dd}.paranormal-base select{max-width:130px;padding:3px;background:#171419;color:#fff;border:1px solid #80545d}.paranormal-base-values{display:flex;align-items:center;flex-wrap:wrap;gap:5px 9px;margin-top:5px;font-size:10px}.paranormal-base-values span{color:#a89496;font-weight:800}.paranormal-base-values b{color:#f5ddd4;font-variant-numeric:tabular-nums}.paranormal-base-values button{margin-left:auto;padding:3px 7px;border:1px solid #9d6568;background:#4b252d;color:#fff;font-size:10px;cursor:pointer}.paranormal-base-values button:focus-visible,.paranormal-base select:focus-visible{outline:2px solid #e6a29c;outline-offset:2px}.paranormal-base>small{display:block;margin-top:5px;color:#b3a1a4;font-size:9px;line-height:1.3}
  .paranormal-resource-stack{display:grid;gap:5px}.paranormal-defense{margin-top:12px}.defense-values{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:5px}.defense-values>div{background:#262027;border:1px solid #6e4b52;text-align:center;padding:7px 4px}.defense-values span{display:block;font-size:9px;font-weight:800;letter-spacing:.09em;text-transform:uppercase;color:#bcaaa8}.defense-values strong{display:block;font:700 23px Georgia,serif;margin-top:3px}.defense-values input{width:55px;margin:4px auto 0;text-align:center;background:#1c171b;color:#fff;border:1px solid #80545d}
  .paranormal-secondary{display:grid;grid-template-columns:94px minmax(0,1fr);gap:4px 8px;margin-top:10px;font-size:11px}.paranormal-secondary strong{color:#c6a7a5;font-size:9px;letter-spacing:.07em}.paranormal-secondary span{white-space:pre-wrap;overflow-wrap:anywhere}
  .paranormal-detail{padding:16px 18px;display:grid;gap:14px;min-height:500px}.paranormal-detail h2{margin:0 0 3px;color:#eee2de;font:600 24px Georgia,serif;border-bottom:1px solid #80545d;padding-bottom:8px}.paranormal-combat-grid{display:grid;grid-template-columns:36% minmax(0,1fr);gap:18px}.paranormal-combat-grid>div{min-width:0}.paranormal-combat-grid .paranormal-secondary{display:grid;grid-template-columns:repeat(2,minmax(0,1fr))}.paranormal-inline-fields{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:8px}.paranormal-detail label{font-size:12px;color:#c8b4b0}.paranormal-detail textarea{display:block;width:100%;background:#211b20;color:#fff;border:1px solid #735158}.paranormal-detail p{white-space:pre-wrap}
  @media(max-width:1020px){.paranormal-primary{grid-template-columns:minmax(275px,41%) minmax(0,59%)}.paranormal-left{padding-inline:9px}.paranormal-right{padding-inline:8px}.paranormal-combat-grid{grid-template-columns:1fr}}
  @media(max-width:750px){.paranormal-primary{grid-template-columns:1fr}.paranormal-left{border-right:0;border-bottom:1px solid #5c4249;display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:7px 14px}.paranormal-left>.paranormal-block-label{grid-column:1/-1}.paranormal-left app-paranormal-attribute-wheel{grid-row:2/6}.paranormal-facts{margin-top:0}.paranormal-defense{margin-top:0}.paranormal-secondary{grid-column:1/-1}.paranormal-right{padding:12px}.paranormal-inline-fields{grid-template-columns:repeat(2,minmax(0,1fr))}}
  @media(max-width:540px){.paranormal-header{grid-template-columns:54px minmax(0,1fr) 58px;gap:8px;padding:8px}.paranormal-portrait{width:54px;height:64px}.paranormal-identity h1{font-size:21px;margin-bottom:3px}.paranormal-byline{gap:2px 7px;font-size:9px}.paranormal-byline b{font-size:8px}.paranormal-nex strong{font-size:23px}.paranormal-controls{display:block}.paranormal-controls nav{width:100%}.paranormal-controls label{justify-content:flex-end;padding:4px 9px}.paranormal-left{display:block}.paranormal-left app-paranormal-attribute-wheel{display:block}.paranormal-inline-fields{grid-template-columns:1fr}.paranormal-detail{padding:12px}}
  @media print{.paranormal-sheet{background:#fff;color:#111;border:0}.paranormal-header,.paranormal-controls{background:#fff;color:#111}.paranormal-controls{display:none}.paranormal-primary{min-height:0}.paranormal-detail{min-height:0;break-inside:avoid}.paranormal-resource,.paranormal-skills{background:#fff;color:#111}}
`]
})
export class ParanormalSheetComponent implements OnChanges {
  ngOnChanges() { this.ruleContext.refresh(); }
  private ruleContext = inject(D20RuleRollContext);
  constructor() { this.ruleContext.character = () => this.character; this.ruleContext.system = () => this.system; this.ruleContext.ready = () => this.rulesReady; this.ruleContext.applied = character => this.restored.emit(character); this.ruleContext.working = busy => this.working.emit(busy); }
  effectiveDefense() { return hasD20Conditions(this.system) ? d20Defense(this.draft.fields, true, false, this.draft.health) : this.number('defesa'); }
  effectiveSpeed() { return hasD20Conditions(this.system) ? d20Speed(this.draft) : this.number('deslocamento'); }
  @Input() rulesReady = false;
  @Output() working = new EventEmitter<boolean>();
  @Input({ required: true }) system!: RpgSystem;
  @Input({ required: true }) draft!: CharacterInput;
  @Input({ required: true }) character!: Character;
  @Input() mode: SheetMode = 'read';
  @Input() isMaster = false;
  @Output() changed = new EventEmitter<void>();
  @Output() restored = new EventEmitter<Character>();
  active = signal('principal');
  visibility = signal<Visibility>('campaign');
  lastRoll = signal('');
  tabs = [{id:'principal',label:'Principal'},{id:'combate',label:'Combate'},{id:'inventario',label:'Inventário'},{id:'habilidades',label:'Habilidades'},{id:'rituais',label:'Rituais'},{id:'anotacoes',label:'Anotações'}];
  show(id: string) { return this.mode === 'print' || this.active() === id; }
  field(key: string) { return this.system.fields.find(item => item.key === key); }
  label(key: string) { return this.field(key)?.label || ({defesa:'Defesa',bloqueio:'Bloqueio',esquiva:'Esquiva'} as Record<string,string>)[key] || key; }
  number(key: string) { const value = this.draft.fields[key]; return typeof value === 'number' ? value : 0; }
  textValue(key: string) { const value = this.draft.fields[key]; return typeof value === 'string' ? value : ''; }
  ruleClassLabel() { return ({ combatente: 'Combatente', especialista: 'Especialista', ocultista: 'Ocultista' } as Record<string, string>)[this.textValue('classe_regra')] || ''; }
  baseResources() { return this.system.rulesetVersion === '1.3' && this.field('classe_regra') ? paranormalV13BaseResources(this.textValue('classe_regra'), this.number('nex'), this.number('vigor'), this.number('presenca')) : null; }
  applyBaseResources() { const base = this.baseResources(); if (!base || this.mode !== 'edit' || !this.character.canEdit) return; Object.assign(this.draft, { health: base.pv, maxHealth: base.pv, mana: base.pe, maxMana: base.pe, sanity: base.san, maxSanity: base.san }); this.changed.emit(); }
  signed(value: number) { return value > 0 ? `+${value}` : `${value}`; }
  canEditField(key: string) { const field = this.field(key); return this.mode === 'edit' && this.character.canEdit && !!field && !field.formula && (field.editRole !== 'gm' || this.isMaster); }
  setField(key: string, value: string | number) { if (!this.canEditField(key)) return; this.draft.fields[key] = value; this.changed.emit(); }
  skillCount() { return this.system.fields.filter(field => field.section === 'Perícias' && field.key.startsWith('per_') && field.type === 'number').length; }
  extraFields() { const template = SHEET_TEMPLATES.find(item => item.id === 'ordem_paranormal'); if (!template) return []; const known = new Set(template.fields.map(field => field.key)); return this.system.fields.filter(field => !known.has(field.key)); }
}
