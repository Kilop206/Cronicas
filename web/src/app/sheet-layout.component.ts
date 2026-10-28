import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Character, CharacterInput, SheetBlock, SheetLayout, SheetPage, SystemField } from './api';
import { CharacterInventoryComponent } from './character-inventory.component';
import { CharacterAbilitiesComponent } from './character-abilities.component';
import { CharacterHistoryPanelComponent } from './character-history.component';
import { SheetRollComponent } from './sheet-roll.component';
import { SheetCollectionFieldComponent } from './sheet-collection-field.component';

type ResourceKey = 'health' | 'sanity' | 'mana';
type MaxResourceKey = 'maxHealth' | 'maxSanity' | 'maxMana';
const resources: Record<ResourceKey, { label: string; max: MaxResourceKey }> = {
  health: { label: 'Vida', max: 'maxHealth' }, sanity: { label: 'Sanidade', max: 'maxSanity' }, mana: { label: 'Mana', max: 'maxMana' }
};

@Component({ selector: 'app-sheet-layout', standalone: true, imports: [CommonModule, FormsModule, CharacterInventoryComponent, CharacterAbilitiesComponent, CharacterHistoryPanelComponent, SheetRollComponent, SheetCollectionFieldComponent], template: `
  <div class="sheet-canvas" [class.sheet-skin-archive]="layout.skin === 'archive'" [class.sheet-skin-investigation]="layout.skin === 'investigation'" [class.sheet-skin-fantasy]="layout.skin === 'fantasy'" [class.sheet-skin-mythic]="layout.skin === 'mythic'" [class.sheet-font-modern]="layout.fontStyle === 'modern'" [class.sheet-font-typewriter]="layout.fontStyle === 'typewriter'" [class.sheet-density-compact]="layout.density === 'compact'" [class.sheet-border-sharp]="layout.borderStyle === 'sharp'" [class.sheet-border-ornate]="layout.borderStyle === 'ornate'" [style.--sheet-accent]="layout.accentColor">
    <header class="sheet-masthead"><div><span>{{ layout.headerLabel || 'CRÔNICAS / FICHA DE PERSONAGEM' }}</span><strong>{{ draft.name || 'Personagem' }}</strong></div><small>{{ layout.pages.length }} {{ layout.pages.length === 1 ? 'PÁGINA' : 'PÁGINAS' }}</small></header>
    <div *ngIf="mode !== 'print' && activePage().id !== layout.pages[0].id" class="sheet-quickbar" aria-label="Resumo do personagem"><div *ngFor="let block of pinnedBlocks()" class="sheet-quick-stat">
      <span>{{ block.title || (block.kind === 'resource' ? resourceLabel(block.key) : fieldFor(block)?.label) }}</span>
      <strong *ngIf="block.kind === 'resource'">{{ resourceValue(block.key) }} / {{ resourceValue(resourceMaxKey(block)) }}</strong>
      <div *ngIf="block.kind === 'resource' && resourceCanWrite(block)" class="sheet-resource-steps"><button type="button" [attr.aria-label]="'Diminuir ' + (block.title || resourceLabel(block.key))" [disabled]="resourceValue(block.key) <= 0" (click)="adjustResource(block, -1)">−</button><button type="button" [attr.aria-label]="'Aumentar ' + (block.title || resourceLabel(block.key))" [disabled]="resourceValue(block.key) >= resourceValue(resourceMaxKey(block))" (click)="adjustResource(block, 1)">+</button></div>
      <strong *ngIf="block.kind === 'field' && fieldFor(block) as field">{{ displayField(field) }}</strong>
    </div></div>
    <nav *ngIf="mode !== 'print'" class="custom-sheet-tabs" aria-label="Páginas da ficha"><button *ngFor="let page of layout.pages" type="button" [class.active]="activePage().id === page.id" (click)="selectedPageId.set(page.id)">{{ page.title }}</button></nav>
    <section *ngFor="let page of visiblePages()" class="sheet-canvas-page"><div class="sheet-page-heading"><span>CAPÍTULO {{ pageNumber(page) }}</span><h3>{{ page.title }}</h3></div><div class="sheet-canvas-grid" [class.free-canvas]="page.layoutMode === 'canvas'" [style.aspect-ratio]="page.layoutMode === 'canvas' ? page.aspectRatio || 0.75 : null" [style.background-image]="page.layoutMode === 'canvas' && page.backgroundUrl ? 'url(' + page.backgroundUrl + ')' : null">
      <div *ngFor="let block of page.blocks" class="sheet-canvas-block" [class.plain]="block.style === 'plain'" [class.accent]="block.style === 'accent'" [class.score]="block.style === 'score'" [class.compact]="block.style === 'compact'" [class.kind-identity]="block.kind === 'identity'" [class.kind-portrait]="block.kind === 'portrait'" [class.kind-resource]="block.kind === 'resource'" [class.kind-tracker]="block.kind === 'tracker'" [class.kind-section]="block.kind === 'section'" [class.kind-field]="block.kind === 'field'" [class.kind-collection]="block.kind === 'inventory' || block.kind === 'abilities' || block.kind === 'history'" [class.is-numeric]="block.kind === 'field' && fieldFor(block)?.type === 'number'" [style.display]="blockVisible(block) ? null : 'none'" [style.grid-column]="'span ' + block.span" [style.left.%]="page.layoutMode === 'canvas' ? block.x : null" [style.top.%]="page.layoutMode === 'canvas' ? block.y : null" [style.width.%]="page.layoutMode === 'canvas' ? block.width : null" [style.height.%]="page.layoutMode === 'canvas' ? block.height : null">
        <small *ngIf="block.kind !== 'section'">{{ block.title || (block.kind === 'field' ? fieldFor(block)?.label : blockLabel(block)) }}</small>
        <ng-container *ngIf="block.kind === 'section'"><h4>{{ block.title }}</h4><p *ngIf="block.text">{{ block.text }}</p></ng-container>
        <ng-container *ngIf="block.kind === 'identity'">
          <label *ngIf="mode === 'edit'">Nome<input [ngModel]="draft.name" (ngModelChange)="draft.name = $event; changed.emit()" [ngModelOptions]="{standalone: true}" required minlength="2" maxlength="120"></label>
          <strong *ngIf="mode !== 'edit'">{{ draft.name }}</strong>
        </ng-container>
        <ng-container *ngIf="block.kind === 'portrait'">
          <div class="sheet-canvas-portrait"><img *ngIf="draft.portraitUrl" [src]="draft.portraitUrl" alt=""><span *ngIf="!draft.portraitUrl">Sem retrato</span></div>
          <ng-container *ngIf="mode === 'edit'"><label>URL HTTPS do retrato<input type="url" [ngModel]="draft.portraitUrl" (ngModelChange)="draft.portraitUrl = $event; changed.emit()" [ngModelOptions]="{standalone: true}" placeholder="https://..."></label><label>URL HTTPS do fundo<input type="url" [ngModel]="draft.backgroundUrl" (ngModelChange)="draft.backgroundUrl = $event; changed.emit()" [ngModelOptions]="{standalone: true}" placeholder="https://..."></label></ng-container>
        </ng-container>
        <ng-container *ngIf="block.kind === 'resource'">
          <strong>{{ block.title || resourceLabel(block.key) }} · {{ resourceValue(block.key) }} / {{ resourceValue(resourceMaxKey(block)) }}</strong>
          <div class="sheet-resource-track" role="meter" [attr.aria-label]="block.title || resourceLabel(block.key)" [attr.aria-valuenow]="resourceValue(block.key)" aria-valuemin="0" [attr.aria-valuemax]="resourceValue(resourceMaxKey(block))"><span [style.width.%]="resourcePercent(block)"></span></div>
          <div *ngIf="mode === 'read' && resourceCanWrite(block)" class="sheet-resource-steps"><button type="button" [attr.aria-label]="'Diminuir ' + (block.title || resourceLabel(block.key))" [disabled]="resourceValue(block.key) <= 0" (click)="adjustResource(block, -1)">−</button><button type="button" [attr.aria-label]="'Aumentar ' + (block.title || resourceLabel(block.key))" [disabled]="resourceValue(block.key) >= resourceValue(resourceMaxKey(block))" (click)="adjustResource(block, 1)">+</button></div>
          <div *ngIf="mode === 'edit'" class="sheet-resource-controls"><label>Atual<input type="number" [ngModel]="resourceValue(block.key)" (ngModelChange)="setResourceValue(block.key, $event)" [ngModelOptions]="{standalone: true}" min="0" max="999999"></label><label>Máximo<input type="number" [ngModel]="resourceValue(resourceMaxKey(block))" (ngModelChange)="setResourceValue(resourceMaxKey(block), $event)" [ngModelOptions]="{standalone: true}" min="1" max="999999"></label></div>
        </ng-container>
        <ng-container *ngIf="block.kind === 'tracker'"><strong>{{ block.title || resourceLabel(block.key) }} · {{ resourceValue(block.key) }} / {{ resourceValue(resourceMaxKey(block)) }}</strong>
          <div *ngIf="resourceValue(resourceMaxKey(block)) <= 20" class="sheet-tracker-dots" role="group" [attr.aria-label]="block.title || resourceLabel(block.key)"><button *ngFor="let number of trackerDots(block)" type="button" [class.filled]="number <= resourceValue(block.key)" [attr.aria-label]="'Definir ' + (block.title || resourceLabel(block.key)) + ' em ' + number" [attr.aria-pressed]="number <= resourceValue(block.key)" [disabled]="!resourceCanWrite(block) || mode === 'print'" (click)="setResourceValue(block.key, number)"></button></div>
          <div *ngIf="resourceCanWrite(block) && mode !== 'print'" class="sheet-resource-steps"><button type="button" [attr.aria-label]="'Diminuir ' + (block.title || resourceLabel(block.key))" [disabled]="resourceValue(block.key) <= 0" (click)="adjustResource(block,-1)">−</button><button type="button" [attr.aria-label]="'Aumentar ' + (block.title || resourceLabel(block.key))" [disabled]="resourceValue(block.key) >= resourceValue(resourceMaxKey(block))" (click)="adjustResource(block,1)">+</button></div>
        </ng-container>
        <ng-container *ngIf="block.kind === 'field'"><ng-container *ngIf="fieldFor(block) as field">
          <label *ngIf="fieldEditable(field) && (field.type === 'text' || field.type === 'number')">{{ field.label }}<input [type]="field.type === 'number' ? 'number' : 'text'" [ngModel]="fieldValue(field)" (ngModelChange)="setField(field, $event)" [ngModelOptions]="{standalone: true}" [min]="field.min" [max]="field.max" [attr.maxlength]="field.type === 'text' ? 300 : null" [readonly]="!!field.formula"></label>
          <label *ngIf="fieldEditable(field) && field.type === 'longtext'">{{ field.label }}<textarea [ngModel]="fieldValue(field)" (ngModelChange)="setField(field, $event)" [ngModelOptions]="{standalone: true}" rows="5" maxlength="10000"></textarea></label>
          <label *ngIf="fieldEditable(field) && field.type === 'select'">{{ field.label }}<select [ngModel]="fieldValue(field)" (ngModelChange)="setField(field, $event)" [ngModelOptions]="{standalone: true}"><option value="">Selecione</option><option *ngFor="let option of field.options" [value]="option">{{ option }}</option></select></label>
          <label *ngIf="fieldEditable(field) && field.type === 'boolean'" class="checkbox-field"><input type="checkbox" [ngModel]="fieldValue(field) === true" (ngModelChange)="setField(field, $event)" [ngModelOptions]="{standalone: true}"> {{ field.label }}</label>
          <strong *ngIf="!fieldEditable(field) && field.type !== 'list' && field.type !== 'table'" class="sheet-value"><span class="sheet-field-caption">{{ field.label }}</span>{{ displayField(field) }}</strong>
          <app-sheet-collection-field *ngIf="field.type === 'list' || field.type === 'table'" [field]="field" [draft]="draft" [campaignId]="character.campaignId" [mode]="fieldEditable(field) ? 'edit' : mode === 'print' ? 'print' : 'read'" (changed)="changed.emit()" />
          <app-sheet-roll *ngIf="mode !== 'print' && field.rollExpression" [campaignId]="character.campaignId" [expression]="field.rollExpression" [values]="draft.fields" [label]="field.label" [compact]="block.style === 'compact' || block.style === 'score'" />
        </ng-container></ng-container>
        <p *ngIf="block.kind === 'note'">{{ block.text }}</p>
        <app-character-inventory *ngIf="block.kind === 'inventory'" [character]="character" />
        <app-character-abilities *ngIf="block.kind === 'abilities'" [character]="character" />
        <app-character-history *ngIf="block.kind === 'history' && mode !== 'print'" [character]="character" (restored)="restored.emit($event)" />
      </div>
    </div></section>
    <section *ngIf="unplacedFields().length" class="sheet-canvas-page"><h3>Campos não posicionados</h3><div class="sheet-canvas-grid"><div *ngFor="let field of unplacedFields()" class="sheet-canvas-block" [style.grid-column]="'span ' + (field.span || 6)">
      <label *ngIf="fieldEditable(field) && (field.type === 'text' || field.type === 'number')">{{ field.label }}<input [type]="field.type === 'number' ? 'number' : 'text'" [ngModel]="fieldValue(field)" (ngModelChange)="setField(field, $event)" [ngModelOptions]="{standalone: true}" [min]="field.min" [max]="field.max" [attr.maxlength]="field.type === 'text' ? 300 : null" [readonly]="!!field.formula"></label>
      <label *ngIf="fieldEditable(field) && field.type === 'longtext'">{{ field.label }}<textarea [ngModel]="fieldValue(field)" (ngModelChange)="setField(field, $event)" [ngModelOptions]="{standalone: true}" rows="5" maxlength="10000"></textarea></label>
      <label *ngIf="fieldEditable(field) && field.type === 'select'">{{ field.label }}<select [ngModel]="fieldValue(field)" (ngModelChange)="setField(field, $event)" [ngModelOptions]="{standalone: true}"><option value="">Selecione</option><option *ngFor="let option of field.options" [value]="option">{{ option }}</option></select></label>
      <label *ngIf="fieldEditable(field) && field.type === 'boolean'" class="checkbox-field"><input type="checkbox" [ngModel]="fieldValue(field) === true" (ngModelChange)="setField(field, $event)" [ngModelOptions]="{standalone: true}"> {{ field.label }}</label>
      <strong *ngIf="!fieldEditable(field) && field.type !== 'list' && field.type !== 'table'">{{ field.label }}: {{ displayField(field) }}</strong>
      <app-sheet-collection-field *ngIf="field.type === 'list' || field.type === 'table'" [field]="field" [draft]="draft" [campaignId]="character.campaignId" [mode]="fieldEditable(field) ? 'edit' : mode === 'print' ? 'print' : 'read'" (changed)="changed.emit()" />
      <app-sheet-roll *ngIf="mode !== 'print' && field.rollExpression" [campaignId]="character.campaignId" [expression]="field.rollExpression" [values]="draft.fields" [label]="field.label" />
    </div></div></section>
  </div>`, styles: [`
    .custom-sheet-tabs { display: flex; gap: 4px; max-width: 100%; overflow-x: auto; border-bottom: 1px solid var(--border); margin: 0 0 18px; scrollbar-width: thin; }
    .custom-sheet-tabs button { flex: 0 0 auto; min-height: 44px; padding: 11px 15px; border: 0; border-bottom: 2px solid transparent; background: transparent; color: var(--muted); font-weight: 600; white-space: nowrap; }
    .custom-sheet-tabs button:hover, .custom-sheet-tabs button.active { border-color: var(--sheet-accent); color: var(--text); background: color-mix(in srgb, var(--sheet-accent) 9%, transparent); }
    .sheet-canvas-block label { display: block; min-width: 0; margin: 12px 0 0; }
    .sheet-resource-controls { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 9px; }
    .sheet-resource-track { height: 5px; margin: 13px 0 5px; background: var(--border); }
    .sheet-resource-track span { display: block; height: 100%; background: var(--sheet-accent); }
    .sheet-canvas-block .sheet-value { font: 16px var(--font-ui); font-weight: 600; white-space: pre-wrap; }
    .sheet-canvas-block app-character-inventory, .sheet-canvas-block app-character-abilities, .sheet-canvas-block app-character-history { display: block; }
    .kind-section { border:0; border-bottom:2px solid var(--sheet-accent); background:transparent!important; padding-bottom:7px; }
    .kind-section h4 { margin:0; font:500 23px var(--font-editorial); }
    .kind-section p { margin:5px 0 0; color:var(--muted); }
    .sheet-tracker-dots { display:flex; flex-wrap:wrap; gap:7px; margin:12px 0; }
    .sheet-tracker-dots button { width:23px; height:23px; border-radius:50%; border:2px solid var(--sheet-accent); background:transparent; padding:0; cursor:pointer; }
    .sheet-tracker-dots button.filled { background:var(--sheet-accent); }
    .sheet-tracker-dots button:disabled { cursor:default; }
    .sheet-font-modern .sheet-masthead strong,.sheet-font-modern .sheet-page-heading h3 { font-family:var(--font-ui); }
    .sheet-font-typewriter .sheet-masthead strong,.sheet-font-typewriter .sheet-page-heading h3,.sheet-font-typewriter .kind-section h4 { font-family:monospace; }
    .sheet-density-compact .sheet-canvas-grid { gap:8px; }
    .sheet-density-compact .sheet-canvas-block { padding:10px; }
    .sheet-border-sharp .sheet-canvas-block { border-radius:0; }
    .sheet-border-ornate .sheet-canvas-block:not(.kind-section) { outline:1px solid color-mix(in srgb,var(--sheet-accent) 40%,transparent); outline-offset:-5px; }
  `] })
export class SheetLayoutComponent {
  @Input({ required: true }) layout!: SheetLayout;
  @Input() fields: SystemField[] = [];
  @Input({ required: true }) draft!: CharacterInput;
  @Input({ required: true }) character!: Character;
  @Input() mode: 'edit' | 'read' | 'print' = 'read';
  @Input() isMaster = false;
  @Output() changed = new EventEmitter<void>();
  @Output() restored = new EventEmitter<Character>();
  selectedPageId = signal('');
  activePage(): SheetPage { return this.layout.pages.find(page => page.id === this.selectedPageId()) || this.layout.pages[0]; }
  pageNumber(page: SheetPage) { return String(this.layout.pages.indexOf(page) + 1).padStart(2, '0'); }
  visiblePages() { return this.mode === 'print' ? this.layout.pages : [this.activePage()]; }
  pinnedBlocks() { return this.layout.pages[0].blocks.filter(block => this.blockVisible(block) && (block.kind === 'resource' || block.kind === 'field' && block.style === 'accent')).slice(0, 6); }
  blockVisible(block: SheetBlock) { return !block.visibleWhenKey || this.draft.fields[block.visibleWhenKey] === (block.visibleWhenValue ?? true); }
  trackerDots(block: SheetBlock) { return Array.from({ length: Math.max(0, Math.min(20, this.resourceValue(this.resourceMaxKey(block)))) }, (_, index) => index + 1); }
  blockLabel(block: SheetBlock) { return block.kind === 'field' ? 'Campo' : block.kind === 'resource' ? 'Recurso' : block.kind === 'tracker' ? 'Marcador' : block.kind === 'section' ? 'Seção' : block.kind === 'note' ? 'Nota' : block.kind === 'identity' ? 'Identidade' : block.kind === 'portrait' ? 'Retrato' : block.kind === 'inventory' ? 'Inventário' : block.kind === 'abilities' ? 'Habilidades' : 'Histórico'; }
  resourceMaxKey(block: SheetBlock): string | null { return block.maxKey || (block.key === 'health' || block.key === 'sanity' || block.key === 'mana' ? resources[block.key].max : null); }
  resourceLabel(key: string | null) { return this.fields.find(field => field.key === key)?.label || ({ health: 'Vida', sanity: 'Sanidade', mana: 'Mana', maxHealth: 'Vida máxima', maxSanity: 'Sanidade máxima', maxMana: 'Mana máxima' } as Record<string, string>)[key || ''] || 'Recurso'; }
  resourceValue(key: string | null): number {
    if (key === 'health' || key === 'sanity' || key === 'mana' || key === 'maxHealth' || key === 'maxSanity' || key === 'maxMana') return this.draft[key];
    return Number(this.draft.fields[key || ''] ?? 0);
  }
  resourcePercent(block: SheetBlock) { const maximum = this.resourceValue(this.resourceMaxKey(block)); return Math.max(0, Math.min(100, maximum > 0 ? this.resourceValue(block.key) / maximum * 100 : 0)); }
  adjustResource(block: SheetBlock, delta: number) { const max = this.resourceValue(this.resourceMaxKey(block)); this.setResourceValue(block.key, Math.max(0, Math.min(max, this.resourceValue(block.key) + delta))); }
  setResourceValue(key: string | null, value: number) {
    if (!key) return;
    if (key === 'health' || key === 'sanity' || key === 'mana' || key === 'maxHealth' || key === 'maxSanity' || key === 'maxMana') { this.draft[key] = Number(value); this.changed.emit(); return; }
    const field = this.fields.find(item => item.key === key && item.type === 'number');
    if (field) this.setField(field, value);
  }
  fieldFor(block: SheetBlock) { return this.fields.find(field => field.key === block.key); }
  fieldCanWrite(field: SystemField) { return this.character.canEdit && !field.formula && (field.editRole !== 'gm' || this.isMaster); }
  resourceCanWrite(block: SheetBlock) { if (!this.character.canEdit) return false; const field = this.fields.find(item => item.key === block.key); return field ? this.fieldCanWrite(field) : true; }
  fieldEditable(field: SystemField) { return this.mode === 'edit' && this.fieldCanWrite(field); }
  fieldValue(field: SystemField) { return this.draft.fields[field.key] ?? ''; }
  displayField(field: SystemField) { const value = this.fieldValue(field); return value === '' ? '—' : field.type === 'boolean' ? value ? 'Sim' : 'Não' : String(value); }
  setField(field: SystemField, value: string | number | boolean) { if (!this.fieldCanWrite(field)) return; if (value === '' || value === null) delete this.draft.fields[field.key]; else this.draft.fields[field.key] = field.type === 'number' ? Number(value) : value; this.changed.emit(); }
  unplacedFields() { const placed = new Set(this.layout.pages.flatMap(page => page.blocks.flatMap(block => block.kind === 'field' ? [block.key] : block.kind === 'resource' || block.kind === 'tracker' ? [block.key, block.maxKey] : []))); return this.fields.filter(field => !placed.has(field.key)); }
}
