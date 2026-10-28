import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Character, CharacterInput, RpgSystem, SystemField } from './api';
import { CharacterAbilitiesComponent } from './character-abilities.component';
import { CharacterHistoryPanelComponent } from './character-history.component';
import { CharacterInventoryComponent } from './character-inventory.component';
import { SheetRollComponent } from './sheet-roll.component';
import { SHEET_TEMPLATES } from './system-templates';
import { SheetCollectionFieldComponent } from './sheet-collection-field.component';

@Component({ selector: 'app-system-field', standalone: true, imports: [CommonModule, FormsModule, SheetRollComponent, SheetCollectionFieldComponent], template: `
  <div *ngIf="field" class="system-field" [class.system-field-score]="style === 'score'" [class.system-field-row]="style === 'row'">
    <span class="field-caption">{{ field.label }}</span>
    <ng-container *ngIf="canEdit(); else value">
      <input *ngIf="field.type === 'number' || field.type === 'text'" [attr.aria-label]="field.label" [type]="field.type === 'number' ? 'number' : 'text'" [ngModel]="fieldValue()" (ngModelChange)="set($event)" [ngModelOptions]="{standalone:true}" [min]="field.min" [max]="field.max" [attr.maxlength]="field.type === 'text' ? 300 : null">
      <textarea *ngIf="field.type === 'longtext'" [attr.aria-label]="field.label" [ngModel]="fieldValue()" (ngModelChange)="set($event)" [ngModelOptions]="{standalone:true}" maxlength="10000" rows="3"></textarea>
      <select *ngIf="field.type === 'select'" [attr.aria-label]="field.label" [ngModel]="fieldValue()" (ngModelChange)="set($event)" [ngModelOptions]="{standalone:true}"><option value="">Selecione</option><option *ngFor="let option of field.options" [value]="option">{{ option }}</option></select>
      <input *ngIf="field.type === 'boolean'" type="checkbox" [attr.aria-label]="field.label" [ngModel]="draft.fields[field.key] === true" (ngModelChange)="set($event)" [ngModelOptions]="{standalone:true}">
    </ng-container>
    <ng-template #value><strong *ngIf="field.type !== 'list' && field.type !== 'table'" class="field-value">{{ display() }}</strong></ng-template>
    <app-sheet-collection-field *ngIf="field.type === 'list' || field.type === 'table'" [field]="field" [draft]="draft" [mode]="fieldMode()" (changed)="changed.emit()" />
    <app-sheet-roll *ngIf="mode !== 'print' && field.rollExpression" [campaignId]="campaignId" [expression]="field.rollExpression" [values]="draft.fields" [label]="field.label" [compact]="style !== 'default'" [percentileTarget]="field.rollExpression === '1d100' ? percentileTarget() : null" />
  </div>`, styles: [`
    .system-field { min-width:0; display:grid; gap:5px; padding:10px 12px; border:1px solid var(--border); background:color-mix(in srgb,var(--surface) 88%,transparent); }
    .field-caption { color:var(--muted); font:600 11px var(--font-ui); letter-spacing:.08em; text-transform:uppercase; }
    .field-value { min-height:22px; font:600 16px var(--font-ui); white-space:pre-wrap; overflow-wrap:anywhere; }
    .system-field-score { text-align:center; align-content:center; min-height:104px; border-radius:50% 50% 10px 10px; }
    .system-field-score .field-value { font-size:29px; }
    .system-field-row { grid-template-columns:minmax(0,1fr) auto auto; align-items:center; }
    .system-field-row .field-value { text-align:right; }
    input,textarea,select { width:100%; min-width:0; margin:0; }
    input[type=checkbox] { width:auto; }
  `] })
export class SystemFieldComponent {
  @Input() field?: SystemField;
  @Input({ required: true }) draft!: CharacterInput;
  @Input() campaignId = '';
  @Input() mode: 'read' | 'edit' | 'print' = 'read';
  @Input() isMaster = false;
  @Input() style: 'score' | 'row' | 'default' = 'default';
  @Output() changed = new EventEmitter<void>();
  fieldValue() { return this.draft.fields[this.field?.key || ''] ?? ''; }
  canEdit() { return this.mode === 'edit' && !this.field?.formula && (this.field?.editRole !== 'gm' || this.isMaster); }
  fieldMode(): 'read' | 'edit' | 'print' { return this.mode === 'print' ? 'print' : this.canEdit() ? 'edit' : 'read'; }
  display() { const value = this.draft.fields[this.field?.key || '']; return value === undefined || value === '' ? '—' : this.field?.type === 'boolean' ? value ? 'Sim' : 'Não' : String(value); }
  percentileTarget() { const value = this.draft.fields[this.field?.key || '']; return typeof value === 'number' ? value : null; }
  set(value: string | number | boolean) { if (!this.field || !this.canEdit()) return; if (value === '' || value === null) delete this.draft.fields[this.field.key]; else this.draft.fields[this.field.key] = this.field.type === 'number' ? Number(value) : value; this.changed.emit(); }
}

@Component({ selector: 'app-system-resource', standalone: true, imports: [CommonModule, FormsModule], template: `
  <div class="system-resource"><div class="system-resource-head"><span>{{ label }}</span><strong>{{ draft[key] }} <small>/ {{ draft[maxKey()] }}</small></strong></div>
    <div class="system-resource-bar" role="meter" [attr.aria-label]="label" [attr.aria-valuenow]="draft[key]" aria-valuemin="0" [attr.aria-valuemax]="draft[maxKey()]"><i [style.width.%]="percent()"></i></div>
    <div *ngIf="mode !== 'print' && canEdit" class="system-resource-controls"><button type="button" [attr.aria-label]="'Diminuir ' + label" [disabled]="draft[key] <= 0" (click)="adjust(-1)">−</button><button type="button" [attr.aria-label]="'Aumentar ' + label" [disabled]="draft[key] >= draft[maxKey()]" (click)="adjust(1)">+</button><label *ngIf="mode === 'edit'">Máximo<input type="number" [attr.aria-label]="'Máximo de ' + label" [ngModel]="draft[maxKey()]" (ngModelChange)="setMax($event)" [ngModelOptions]="{standalone:true}" min="1" max="9999"></label></div>
  </div>`, styles: [`
    .system-resource { border:1px solid var(--border); padding:12px; background:color-mix(in srgb,var(--surface) 90%,transparent); }
    .system-resource-head { display:flex; justify-content:space-between; align-items:baseline; gap:10px; font:600 12px var(--font-ui); text-transform:uppercase; letter-spacing:.08em; }
    .system-resource-head strong { font-size:21px; letter-spacing:0; }
    .system-resource-head small { font-size:12px; color:var(--muted); }
    .system-resource-bar { height:5px; margin:9px 0; background:var(--border); }
    .system-resource-bar i { display:block; height:100%; background:var(--system-accent,var(--gold)); }
    .system-resource-controls { display:flex; align-items:center; gap:6px; }
    button { min-width:35px; min-height:35px; color:var(--text); background:transparent; border:1px solid var(--border); cursor:pointer; }
    button:disabled { opacity:.4; cursor:default; }
    label { display:flex; align-items:center; gap:6px; margin-left:auto; font-size:11px; }
    input { width:62px; margin:0; }
  `] })
export class SystemResourceComponent {
  @Input({ required: true }) draft!: CharacterInput;
  @Input({ required: true }) key!: 'health' | 'sanity' | 'mana';
  @Input() label = 'Recurso'; @Input() canEdit = false; @Input() mode: 'read' | 'edit' | 'print' = 'read';
  @Output() changed = new EventEmitter<void>();
  maxKey(): 'maxHealth' | 'maxSanity' | 'maxMana' { return this.key === 'health' ? 'maxHealth' : this.key === 'sanity' ? 'maxSanity' : 'maxMana'; }
  percent() { return Math.max(0, Math.min(100, this.draft[this.key] / this.draft[this.maxKey()] * 100)); }
  adjust(step: number) { this.draft[this.key] = Math.max(0, Math.min(this.draft[this.maxKey()], this.draft[this.key] + step)); this.changed.emit(); }
  setMax(value: number) { const max = Math.max(1, Math.min(9999, Number(value) || 1)); this.draft[this.maxKey()] = max; this.draft[this.key] = Math.min(this.draft[this.key], max); this.changed.emit(); }
}

@Component({ selector: 'app-specialized-sheet', standalone: true, imports: [CommonModule, FormsModule, SystemFieldComponent, SystemResourceComponent, CharacterInventoryComponent, CharacterAbilitiesComponent, CharacterHistoryPanelComponent], template: `
  <article class="system-sheet" [class.order-sheet]="system.templateId === 'ordem_paranormal'" [class.dnd-sheet]="system.templateId === 'dnd_5e'" [class.t20-sheet]="system.templateId === 'tormenta20'" [class.coc-sheet]="system.templateId === 'call_of_cthulhu_7e'">
    <header class="system-sheet-head"><div class="system-sheet-portrait"><img *ngIf="draft.portraitUrl" [src]="draft.portraitUrl" alt=""><span *ngIf="!draft.portraitUrl">{{ draft.name.slice(0,1) || 'C' }}</span></div>
      <div class="system-sheet-title"><small>{{ heading() }}</small><h2>{{ draft.name || 'Personagem' }}</h2><p>{{ system.name }}<span *ngIf="system.rulesetVersion"> · {{ system.rulesetVersion }}</span></p></div>
      <div *ngIf="mode === 'edit'" class="system-sheet-identity"><label>Nome<input [ngModel]="draft.name" (ngModelChange)="draft.name=$event; changed.emit()" [ngModelOptions]="{standalone:true}" maxlength="120"></label><label>URL do retrato<input type="url" [ngModel]="draft.portraitUrl" (ngModelChange)="draft.portraitUrl=$event; changed.emit()" [ngModelOptions]="{standalone:true}"></label></div>
    </header>
    <nav *ngIf="mode !== 'print'" class="system-sheet-nav" aria-label="Seções do sistema"><button *ngFor="let tab of tabs()" type="button" [class.active]="active() === tab.id" (click)="active.set(tab.id)">{{ tab.label }}</button></nav>

    <ng-container [ngSwitch]="system.templateId">
      <ng-container *ngSwitchCase="'ordem_paranormal'">
        <section *ngIf="show('perfil')" class="system-panel order-dossier"><div class="system-section-title"><small>ARQUIVO / 01</small><h3>Identidade do agente</h3></div><div class="system-fields-grid"><app-system-field [isMaster]="isMaster" *ngFor="let key of ['origem','classe','trilha','nex','aparencia','afinidade','vinculos']" [field]="field(key)" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" (changed)="changed.emit()" /></div><div class="system-resources"><app-system-resource [draft]="draft" key="health" label="PV" [canEdit]="character.canEdit" [mode]="mode" (changed)="changed.emit()" /><app-system-resource [draft]="draft" key="mana" label="PE" [canEdit]="character.canEdit" [mode]="mode" (changed)="changed.emit()" /><app-system-resource [draft]="draft" key="sanity" label="SAN" [canEdit]="character.canEdit" [mode]="mode" (changed)="changed.emit()" /></div><div class="system-fields-grid"><app-system-field [isMaster]="isMaster" *ngFor="let key of ['defesa','deslocamento']" [field]="field(key)" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" (changed)="changed.emit()" /></div></section>
        <section *ngIf="show('investigacao')" class="system-panel order-evidence"><div class="system-section-title"><small>ARQUIVO / 02</small><h3>Aptidões e investigação</h3></div><div class="order-attributes"><app-system-field [isMaster]="isMaster" *ngFor="let key of ['agilidade','forca','intelecto','presenca','vigor']" [field]="field(key)" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" style="score" (changed)="changed.emit()" /></div><h4>Perícias</h4><div class="system-two-col"><app-system-field [isMaster]="isMaster" *ngFor="let item of prefix('per_')" [field]="item" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" style="row" (changed)="changed.emit()" /></div></section>
        <section *ngIf="show('arsenal')" class="system-panel"><div class="system-section-title"><small>ARQUIVO / 03</small><h3>Combate e paranormal</h3></div><div class="system-fields-grid"><app-system-field [isMaster]="isMaster" *ngFor="let key of ['equip_defesa','outros_defesa','iniciativa']" [field]="field(key)" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" (changed)="changed.emit()" /></div><app-system-field [isMaster]="isMaster" [field]="field('condicoes')" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" (changed)="changed.emit()" /><app-character-inventory [character]="character" /><app-character-abilities [character]="character" /></section>
        <section *ngIf="show('historia')" class="system-panel"><div class="system-section-title"><small>ARQUIVO / 04</small><h3>Registros</h3></div><app-character-history *ngIf="mode !== 'print'" [character]="character" (restored)="restored.emit($event)" /></section>
      </ng-container>

      <ng-container *ngSwitchCase="'dnd_5e'">
        <section *ngIf="show('heroi')" class="system-panel dnd-hero"><div class="system-section-title"><small>CHAPTER I</small><h3>Personagem</h3></div><div class="system-fields-grid"><app-system-field [isMaster]="isMaster" *ngFor="let key of ['especie','classe','subclasse','antecedente','alinhamento','nivel','experiencia']" [field]="field(key)" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" (changed)="changed.emit()" /></div><div class="system-resources"><app-system-resource [draft]="draft" key="health" label="Hit Points" [canEdit]="character.canEdit" [mode]="mode" (changed)="changed.emit()" /><app-system-field [isMaster]="isMaster" [field]="field('classe_armadura')" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" style="score" (changed)="changed.emit()" /><app-system-field [isMaster]="isMaster" [field]="field('iniciativa')" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" style="score" (changed)="changed.emit()" /></div><div class="system-fields-grid"><app-system-field [isMaster]="isMaster" *ngFor="let key of ['deslocamento','pontos_temporarios','proficiencia','dados_vida','salvaguardas_sucesso','salvaguardas_falha']" [field]="field(key)" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" (changed)="changed.emit()" /></div><app-system-field [isMaster]="isMaster" [field]="field('condicoes')" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" (changed)="changed.emit()" /></section>
        <section *ngIf="show('aptidoes')" class="system-panel dnd-abilities"><div class="system-section-title"><small>CHAPTER II</small><h3>Atributos e testes</h3></div><div class="dnd-ability-grid"><div *ngFor="let key of ['forca','destreza','constituicao','inteligencia','sabedoria','carisma']" class="dnd-ability"><app-system-field [isMaster]="isMaster" [field]="field(key)" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" style="score" (changed)="changed.emit()" /><app-system-field [isMaster]="isMaster" [field]="field('mod_'+key)" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" style="row" (changed)="changed.emit()" /></div></div><h4>Saving Throws</h4><div class="system-two-col"><app-system-field [isMaster]="isMaster" *ngFor="let item of prefix('res_')" [field]="item" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" style="row" (changed)="changed.emit()" /></div><h4>Treino em resistências</h4><div class="system-two-col"><app-system-field [isMaster]="isMaster" *ngFor="let item of prefix('treino_res_')" [field]="item" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" style="row" (changed)="changed.emit()" /></div></section>
        <section *ngIf="show('pericias')" class="system-panel"><div class="system-section-title"><small>CHAPTER III</small><h3>Perícias e proficiência</h3></div><div class="system-two-col"><div *ngFor="let item of prefix('per_')" class="dnd-skill"><app-system-field [isMaster]="isMaster" [field]="item" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" style="row" (changed)="changed.emit()" /><app-system-field [isMaster]="isMaster" [field]="field('treino_'+item.key)" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" style="row" (changed)="changed.emit()" /></div></div></section>
        <section *ngIf="show('grimorio')" class="system-panel dnd-grimoire"><div class="system-section-title"><small>CHAPTER IV</small><h3>Magias e características</h3></div><div class="system-fields-grid"><app-system-field [isMaster]="isMaster" *ngFor="let key of ['atributo_conjuracao','cd_magias','ataque_magico']" [field]="field(key)" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" (changed)="changed.emit()" /></div><h4>Spell Slots</h4><div class="system-fields-grid"><app-system-field [isMaster]="isMaster" *ngFor="let item of prefix('espacos_magia_')" [field]="item" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" style="row" (changed)="changed.emit()" /></div><app-system-field [isMaster]="isMaster" [field]="field('magias_preparadas')" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" (changed)="changed.emit()" /><app-character-abilities [character]="character" /></section>
        <section *ngIf="show('equipamento')" class="system-panel"><div class="system-section-title"><small>CHAPTER V</small><h3>Equipamento e jornada</h3></div><app-system-field [isMaster]="isMaster" [field]="field('moedas')" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" (changed)="changed.emit()" /><app-character-inventory [character]="character" /><app-character-history *ngIf="mode !== 'print'" [character]="character" (restored)="restored.emit($event)" /></section>
      </ng-container>

      <ng-container *ngSwitchCase="'tormenta20'">
        <section *ngIf="show('heroi')" class="system-panel t20-hero"><div class="system-section-title"><small>LENDA / I</small><h3>Herói de Arton</h3></div><div class="system-fields-grid"><app-system-field [isMaster]="isMaster" *ngFor="let key of ['raca','origem','classe','divindade','nivel','experiencia']" [field]="field(key)" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" (changed)="changed.emit()" /></div><div class="system-resources"><app-system-resource [draft]="draft" key="health" label="PV" [canEdit]="character.canEdit" [mode]="mode" (changed)="changed.emit()" /><app-system-resource [draft]="draft" key="mana" label="PM" [canEdit]="character.canEdit" [mode]="mode" (changed)="changed.emit()" /><app-system-field [isMaster]="isMaster" [field]="field('defesa')" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" style="score" (changed)="changed.emit()" /></div><div class="t20-stats"><app-system-field [isMaster]="isMaster" *ngFor="let key of ['forca','destreza','constituicao','inteligencia','sabedoria','carisma']" [field]="field(key)" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" style="score" (changed)="changed.emit()" /></div></section>
        <section *ngIf="show('acao')" class="system-panel"><div class="system-section-title"><small>LENDA / II</small><h3>Combate e perícias</h3></div><app-system-field [isMaster]="isMaster" [field]="field('deslocamento')" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" (changed)="changed.emit()" /><app-system-field [isMaster]="isMaster" [field]="field('ataques')" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" (changed)="changed.emit()" /><app-system-field [isMaster]="isMaster" [field]="field('condicoes')" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" (changed)="changed.emit()" /><div class="system-two-col"><app-system-field [isMaster]="isMaster" *ngFor="let item of prefix('per_')" [field]="item" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" style="row" (changed)="changed.emit()" /></div></section>
        <section *ngIf="show('poderes')" class="system-panel"><div class="system-section-title"><small>LENDA / III</small><h3>Poderes e magias</h3></div><app-character-abilities [character]="character" /></section>
        <section *ngIf="show('mochila')" class="system-panel"><div class="system-section-title"><small>LENDA / IV</small><h3>Mochila e crônica</h3></div><app-character-inventory [character]="character" /><app-character-history *ngIf="mode !== 'print'" [character]="character" (restored)="restored.emit($event)" /></section>
      </ng-container>

      <ng-container *ngSwitchCase="'call_of_cthulhu_7e'">
        <section *ngIf="show('investigador')" class="system-panel coc-dossier"><div class="system-section-title"><small>CASE FILE / 001</small><h3>Investigador</h3></div><div class="system-fields-grid"><app-system-field [isMaster]="isMaster" *ngFor="let key of ['ocupacao','local','epoca','idade','credito','sorte','contatos','desenvolvimento']" [field]="field(key)" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" (changed)="changed.emit()" /></div><div class="system-resources"><app-system-resource [draft]="draft" key="health" label="HP" [canEdit]="character.canEdit" [mode]="mode" (changed)="changed.emit()" /><app-system-resource [draft]="draft" key="sanity" label="SAN" [canEdit]="character.canEdit" [mode]="mode" (changed)="changed.emit()" /><app-system-resource [draft]="draft" key="mana" label="Magic Points" [canEdit]="character.canEdit" [mode]="mode" (changed)="changed.emit()" /></div></section>
        <section *ngIf="show('caracteristicas')" class="system-panel coc-evidence"><div class="system-section-title"><small>CASE FILE / 002</small><h3>Características percentuais</h3></div><div class="coc-characteristics"><div *ngFor="let key of ['forca','constituicao','tamanho','destreza','aparencia','educacao','inteligencia','poder']" class="coc-characteristic"><app-system-field [isMaster]="isMaster" [field]="field(key)" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" style="score" (changed)="changed.emit()" /><div><app-system-field [isMaster]="isMaster" [field]="field(key+'_metade')" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" style="row" (changed)="changed.emit()" /><app-system-field [isMaster]="isMaster" [field]="field(key+'_quinto')" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" style="row" (changed)="changed.emit()" /></div></div></div></section>
        <section *ngIf="show('pericias')" class="system-panel"><div class="system-section-title"><small>CASE FILE / 003</small><h3>Perícias e pistas</h3></div><div class="system-two-col"><app-system-field [isMaster]="isMaster" *ngFor="let item of prefix('per_')" [field]="item" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" style="row" (changed)="changed.emit()" /></div></section>
        <section *ngIf="show('arquivo')" class="system-panel"><div class="system-section-title"><small>CASE FILE / 004</small><h3>Arquivo pessoal</h3></div><app-system-field [isMaster]="isMaster" [field]="field('condicoes')" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" (changed)="changed.emit()" /><app-character-inventory [character]="character" /><app-character-abilities [character]="character" /><app-character-history *ngIf="mode !== 'print'" [character]="character" (restored)="restored.emit($event)" /></section>
      </ng-container>
    </ng-container>
    <section *ngIf="extraFields().length && show('extras')" class="system-panel"><div class="system-section-title"><small>CRÔNICAS / CAMPANHA</small><h3>Campos da mesa</h3></div><div class="system-two-col"><app-system-field [isMaster]="isMaster" *ngFor="let item of extraFields()" [field]="item" [draft]="draft" [campaignId]="character.campaignId" [mode]="mode" (changed)="changed.emit()" /></div></section>
  </article>`, styles: [`
    .system-sheet { --system-accent:#a89b83; --system-tint:rgba(168,155,131,.08); color:var(--text); border:1px solid var(--border); background:linear-gradient(150deg,var(--system-tint),transparent 33%),var(--surface); overflow:hidden; }
    .system-sheet-head { display:grid; grid-template-columns:80px minmax(0,1fr); gap:18px; padding:22px 26px; border-bottom:2px solid var(--system-accent); align-items:center; }
    .system-sheet-portrait { width:80px; height:92px; display:grid; place-items:center; border:2px solid var(--system-accent); background:var(--system-tint); color:var(--system-accent); font:700 39px Georgia,serif; }
    .system-sheet-portrait img { width:100%; height:100%; object-fit:cover; }
    .system-sheet-title small,.system-section-title small { color:var(--system-accent); font:700 10px var(--font-ui); letter-spacing:.19em; }
    .system-sheet-title h2 { font:500 clamp(27px,4vw,43px) Georgia,serif; margin:4px 0; }
    .system-sheet-title p { color:var(--muted); margin:0; }
    .system-sheet-identity { grid-column:1/-1; display:grid; grid-template-columns:1fr 1fr; gap:12px; }
    .system-sheet-nav { display:flex; gap:0; overflow-x:auto; border-bottom:1px solid var(--border); scrollbar-width:thin; }
    .system-sheet-nav button { flex:0 0 auto; min-height:46px; border:0; border-bottom:3px solid transparent; padding:10px 17px; background:transparent; color:var(--muted); font:700 12px var(--font-ui); cursor:pointer; }
    .system-sheet-nav button.active,.system-sheet-nav button:hover { border-color:var(--system-accent); color:var(--text); background:var(--system-tint); }
    .system-panel { padding:clamp(17px,3vw,30px); display:grid; gap:18px; }
    .system-section-title { display:flex; align-items:baseline; justify-content:space-between; gap:12px; border-bottom:1px solid var(--border); padding-bottom:10px; }
    .system-section-title h3 { font:500 clamp(22px,3vw,30px) Georgia,serif; margin:0; }
    .system-panel h4 { margin:8px 0 0; font:600 17px Georgia,serif; }
    .system-fields-grid,.system-resources,.system-two-col,.order-attributes,.dnd-ability-grid,.t20-stats,.coc-characteristics { display:grid; gap:10px; }
    .system-fields-grid { grid-template-columns:repeat(3,minmax(0,1fr)); }
    .system-resources { grid-template-columns:repeat(3,minmax(0,1fr)); }
    .system-two-col { grid-template-columns:repeat(2,minmax(0,1fr)); }
    .order-attributes { grid-template-columns:repeat(5,minmax(0,1fr)); }
    .dnd-ability-grid,.t20-stats { grid-template-columns:repeat(3,minmax(0,1fr)); }
    .dnd-ability { display:grid; gap:4px; border:1px solid var(--system-accent); padding:7px; background:var(--system-tint); }
    .dnd-skill { display:grid; gap:3px; border-left:2px solid var(--system-accent); padding-left:5px; }
    .coc-characteristics { grid-template-columns:repeat(2,minmax(0,1fr)); }
    .coc-characteristic { display:grid; grid-template-columns:1fr 1.4fr; gap:4px; padding:6px; border-bottom:1px dashed var(--system-accent); }
    .coc-characteristic>div { display:grid; gap:4px; }
    .order-sheet { --system-accent:#c45a60; --system-tint:rgba(130,28,34,.14); background:linear-gradient(125deg,rgba(90,12,19,.19),transparent 45%),var(--surface); }
    .order-sheet .system-sheet-head { text-transform:uppercase; }
    .order-sheet .system-sheet-title h2 { font-family:var(--font-ui); font-weight:750; letter-spacing:.03em; }
    .order-dossier { background:repeating-linear-gradient(0deg,transparent 0 39px,rgba(196,90,96,.055) 40px); }
    .order-evidence .system-two-col { border-top:2px solid var(--system-accent); padding-top:12px; }
    .dnd-sheet { --system-accent:#c99769; --system-tint:rgba(185,133,76,.12); }
    .dnd-sheet .system-sheet-head { background:radial-gradient(circle at right top,rgba(201,151,105,.16),transparent 50%); }
    .dnd-hero .system-resources { border-block:1px solid var(--system-accent); padding-block:12px; }
    .dnd-grimoire { border-left:5px double var(--system-accent); }
    .t20-sheet { --system-accent:#e49a63; --system-tint:rgba(181,78,38,.12); }
    .t20-sheet .system-sheet-head { background:linear-gradient(110deg,rgba(112,40,27,.23),transparent 65%); }
    .t20-hero .t20-stats { padding:16px; border:1px solid var(--system-accent); background:var(--system-tint); }
    .coc-sheet { --system-accent:#91a698; --system-tint:rgba(112,134,109,.11); }
    .coc-sheet .system-sheet-head { border-bottom-style:dashed; background:repeating-linear-gradient(0deg,transparent 0 28px,rgba(112,134,109,.05) 29px); }
    .coc-sheet .system-sheet-title h2 { font-family:Georgia,serif; font-style:italic; }
    .coc-dossier { border-left:6px solid var(--system-accent); }
    @media(max-width:750px) { .system-fields-grid,.system-resources,.system-two-col,.dnd-ability-grid,.t20-stats,.coc-characteristics { grid-template-columns:repeat(2,minmax(0,1fr)); } .order-attributes { grid-template-columns:repeat(3,minmax(0,1fr)); } }
    @media(max-width:520px) { .system-sheet-head { grid-template-columns:58px minmax(0,1fr); gap:11px; padding:15px; } .system-sheet-portrait { width:58px; height:70px; } .system-sheet-identity,.system-fields-grid,.system-resources,.system-two-col,.coc-characteristics { grid-template-columns:1fr; } .order-attributes,.dnd-ability-grid,.t20-stats { grid-template-columns:repeat(2,minmax(0,1fr)); } .system-section-title { display:block; } }
    @media print { .system-sheet { border:0; background:white; color:#111; } .system-panel { break-inside:avoid; } .system-sheet-nav { display:none; } }
  `] })
export class SpecializedSheetComponent {
  @Input({ required: true }) system!: RpgSystem;
  @Input({ required: true }) draft!: CharacterInput;
  @Input({ required: true }) character!: Character;
  @Input() mode: 'read' | 'edit' | 'print' = 'read';
  @Input() isMaster = false;
  @Output() changed = new EventEmitter<void>();
  @Output() restored = new EventEmitter<Character>();
  active = signal('');
  extraFields() { const template = SHEET_TEMPLATES.find(item => item.id === this.system.templateId && item.version === this.system.templateVersion); if (!template) return []; const known = new Set(template.fields.map(field => field.key)); return this.system.fields.filter(field => !known.has(field.key)); }
  field(key: string) { return this.system.fields.find(item => item.key === key); }
  prefix(prefix: string) { return this.system.fields.filter(item => item.key.startsWith(prefix)); }
  tabs() {
    const extra = this.extraFields().length ? [{id:'extras',label:'Campos da mesa'}] : [];
    switch (this.system.templateId) {
      case 'ordem_paranormal': return [{id:'perfil',label:'Dossiê'}, {id:'investigacao',label:'Investigação'}, {id:'arsenal',label:'Arsenal & paranormal'}, {id:'historia',label:'Registros'}, ...extra];
      case 'dnd_5e': return [{id:'heroi',label:'Personagem'}, {id:'aptidoes',label:'Atributos'}, {id:'pericias',label:'Perícias'}, {id:'grimorio',label:'Magias'}, {id:'equipamento',label:'Equipamento'}, ...extra];
      case 'tormenta20': return [{id:'heroi',label:'Herói'}, {id:'acao',label:'Ação'}, {id:'poderes',label:'Poderes'}, {id:'mochila',label:'Mochila'}, ...extra];
      default: return [{id:'investigador',label:'Investigador'}, {id:'caracteristicas',label:'Características'}, {id:'pericias',label:'Perícias'}, {id:'arquivo',label:'Arquivo'}, ...extra];
    }
  }
  show(id: string) { return this.mode === 'print' || (this.active() || this.tabs()[0].id) === id; }
  heading() { return this.system.templateId === 'ordem_paranormal' ? 'CRÔNICAS / DOCUMENTO CLASSIFICADO' : this.system.templateId === 'dnd_5e' ? 'CRÔNICAS / CHARACTER RECORD' : this.system.templateId === 'tormenta20' ? 'CRÔNICAS / CRÔNICA HEROICA' : 'CRÔNICAS / ARQUIVO DE INVESTIGAÇÃO'; }
}
