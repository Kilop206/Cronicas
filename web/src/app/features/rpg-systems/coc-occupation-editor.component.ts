import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CthulhuOccupationDefinition, CthulhuOccupationSlot, SystemField } from '../../api';

@Component({selector:'app-coc-occupation-editor', standalone:true, imports:[CommonModule,FormsModule], template:`
  <details class="occupation-editor"><summary>Criação 7e · bases e ocupações</summary>
    <p>As fórmulas usam chaves numéricas da ficha e operações permitidas, como educacao * 4 e floordiv(destreza, 2). Cada ocupação exige oito escolhas distintas; uma escolha pode aceitar várias perícias. Alterações durante uma distribuição aberta exigem reiniciá-la ou restaurar a revisão anterior.</p>
    <div class="settings"><label>Limite de perícia na criação (vazio: sem limite adicional)<input type="number" aria-label="Limite de perícia na criação" [(ngModel)]="rules.skillCap" [ngModelOptions]="{standalone:true}" min="1" max="9999" [disabled]="readonly"></label><label><input type="checkbox" [(ngModel)]="rules.personalCredit" [ngModelOptions]="{standalone:true}" [disabled]="readonly"> Permitir interesses pessoais em Crédito (configuração da mesa)</label></div>
    <details><summary>Bases das perícias · {{ rules.skills.length }}</summary><div *ngFor="let skill of rules.skills; let i=index" class="base-row"><label>Perícia<select [attr.aria-label]="'Perícia base ' + (i+1)" [(ngModel)]="skill.key" [ngModelOptions]="{standalone:true}" [disabled]="readonly"><option *ngFor="let field of eligible()" [value]="field.key">{{ field.label }}</option></select></label><label>Fórmula base<input [attr.aria-label]="'Fórmula base ' + skill.key" [(ngModel)]="skill.formula" [ngModelOptions]="{standalone:true}" maxlength="200" [disabled]="readonly"></label><button type="button" *ngIf="!readonly" (click)="rules.skills.splice(i,1)">Remover</button></div><button type="button" *ngIf="!readonly" [disabled]="rules.skills.length >= 60" (click)="addSkill()">Adicionar base de perícia</button></details>
    <details *ngFor="let profile of rules.occupations; let i=index" class="profile"><summary>{{ profile.name || 'Nova ocupação' }} · {{ profile.points }} · Crédito {{ profile.creditMin }}–{{ profile.creditMax }}</summary>
      <div class="settings"><label>Identificador estável<input [attr.aria-label]="'Identificador da ocupação ' + (i+1)" [(ngModel)]="profile.id" [ngModelOptions]="{standalone:true}" maxlength="40" [disabled]="readonly"></label><label>Nome<input [attr.aria-label]="'Nome da ocupação ' + (i+1)" [(ngModel)]="profile.name" [ngModelOptions]="{standalone:true}" maxlength="80" [disabled]="readonly"></label><label>Fórmula dos pontos<input [attr.aria-label]="'Pontos da ocupação ' + (i+1)" [(ngModel)]="profile.points" [ngModelOptions]="{standalone:true}" maxlength="200" [disabled]="readonly"></label><label>Crédito mínimo<input type="number" [attr.aria-label]="'Crédito mínimo ' + (i+1)" [(ngModel)]="profile.creditMin" [ngModelOptions]="{standalone:true}" min="0" [max]="profile.creditMax" [disabled]="readonly"></label><label>Crédito máximo<input type="number" [attr.aria-label]="'Crédito máximo ' + (i+1)" [(ngModel)]="profile.creditMax" [ngModelOptions]="{standalone:true}" [min]="profile.creditMin" max="99" [disabled]="readonly"></label></div>
      <details *ngFor="let slot of profile.slots; let n=index" class="slot"><summary>Escolha {{ n+1 }} · {{ slot.label }}</summary><label>Rótulo<input [attr.aria-label]="'Rótulo ' + profile.id + ' escolha ' + (n+1)" [(ngModel)]="slot.label" [ngModelOptions]="{standalone:true}" maxlength="80" [disabled]="readonly"></label><label><input type="checkbox" [ngModel]="!slot.skills" (ngModelChange)="slot.skills = $event ? null : []" [ngModelOptions]="{standalone:true}" [disabled]="readonly"> Qualquer perícia configurada</label><fieldset *ngIf="slot.skills"><legend>Perícias permitidas (uma por escolha)</legend><label *ngFor="let skill of rules.skills" class="skill-option"><input type="checkbox" [ngModel]="slot.skills.includes(skill.key)" (ngModelChange)="toggle(slot,skill.key,$event)" [ngModelOptions]="{standalone:true}" [disabled]="readonly">{{ label(skill.key) }}</label></fieldset></details>
      <button *ngIf="!readonly" type="button" (click)="rules.occupations.splice(i,1)">Remover ocupação</button>
    </details>
    <button *ngIf="!readonly" type="button" [disabled]="rules.occupations.length >= 30" (click)="addOccupation()">Adicionar ocupação</button>
  </details>
`, styles:[`
 :host{display:block;min-width:0}.occupation-editor{margin:12px 0;padding:10px;border:1px solid var(--border)}summary{cursor:pointer;font-size:12px;font-weight:700}p{font-size:12px;line-height:1.5;color:var(--muted)}details details{margin-top:9px;padding:8px;border:1px solid var(--border)}label{display:block;margin:7px 0;font-size:11px}input:not([type=checkbox]),select{display:block;width:100%;min-width:0;max-width:100%;margin-top:4px;padding:6px;color:var(--text);background:var(--surface);border:1px solid var(--border);font-size:12px}input[type=checkbox]{width:auto;margin-right:5px}.settings{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:0 9px}.base-row{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr) auto;gap:7px;align-items:end}.base-row label{min-width:0}.base-row button{margin-bottom:7px}button{margin-top:6px;padding:5px 8px;border:1px solid var(--border);background:var(--surface);color:var(--text);font-size:11px;cursor:pointer}.skill-option{display:inline-flex;align-items:center;margin:5px 10px 5px 0}.skill-option input{flex-shrink:0}fieldset{min-width:0;border:1px solid var(--border)}legend{font-size:11px}button:focus-visible,input:focus-visible,select:focus-visible,summary:focus-visible{outline:2px solid var(--text);outline-offset:2px}@media(max-width:600px){.settings,.base-row{grid-template-columns:1fr}.base-row{border-bottom:1px solid var(--border);padding-bottom:6px}}
`]})
export class CocOccupationEditorComponent {
  @Input({required:true}) rules!: CthulhuOccupationDefinition;
  @Input({required:true}) fields!: SystemField[];
  @Input() readonly = false;
  eligible() { return this.fields.filter(f => f.type === 'number' && !f.formula && f.key.startsWith('per_') && f.key !== 'per_mitos'); }
  label(key: string) { return this.fields.find(field => field.key === key)?.label || key; }
  addSkill() { const field = this.eligible().find(field => !this.rules.skills.some(skill => skill.key === field.key)); if (field) this.rules.skills.push({key:field.key,formula:'0'}); }
  toggle(slot: CthulhuOccupationSlot, key: string, on: boolean) { slot.skills = on ? [...(slot.skills || []),key] : slot.skills?.filter(item => item !== key) || []; }
  addOccupation() { let index=1; while(this.rules.occupations.some(p => p.id === `ocupacao_${index}`)) index++; this.rules.occupations.push({id:`ocupacao_${index}`,name:'Nova ocupação',points:'educacao * 4',creditMin:0,creditMax:99,slots:Array.from({length:8},(_,n) => ({id:`escolha_${n+1}`,label:`Escolha ${n+1}`,skills:null}))}); }
}

// Check imported JSON before passing it to a template. The server validates field references and formulas.
export function isOccupationDefinition(value: unknown): value is CthulhuOccupationDefinition {
  if (!value || typeof value !== 'object') return false;
  const rules = value as CthulhuOccupationDefinition;
  const text = (v: unknown, max: number) => typeof v === 'string' && v.trim().length > 0 && v.length <= max;
  return Array.isArray(rules.skills) && rules.skills.length > 0 && rules.skills.length <= 60
    && rules.skills.every(skill => skill && text(skill.key,40) && text(skill.formula,200))
    && (rules.skillCap == null || Number.isInteger(rules.skillCap) && rules.skillCap >= 1 && rules.skillCap <= 9999)
    && (rules.personalCredit == null || typeof rules.personalCredit === 'boolean')
    && Array.isArray(rules.occupations) && rules.occupations.length > 0 && rules.occupations.length <= 30
    && rules.occupations.every(profile => profile && text(profile.id,40) && text(profile.name,80) && text(profile.points,200)
      && Number.isInteger(profile.creditMin) && Number.isInteger(profile.creditMax) && profile.creditMin >= 0 && profile.creditMax <= 99 && profile.creditMin <= profile.creditMax
      && Array.isArray(profile.slots) && profile.slots.length === 8 && profile.slots.every(slot => slot && text(slot.id,40) && text(slot.label,80)
        && (slot.skills == null || Array.isArray(slot.skills) && slot.skills.length > 0 && slot.skills.length <= 60 && slot.skills.every(key => text(key,40)))));
}
