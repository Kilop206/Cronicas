import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Character, CharacterInput, SheetBlock, SheetLayout, SheetPage, SystemField } from './api';
import { CharacterInventoryComponent } from './character-inventory.component';
import { CharacterAbilitiesComponent } from './character-abilities.component';
import { CharacterHistoryPanelComponent } from './character-history.component';

type ResourceKey = 'health' | 'sanity' | 'mana';
type MaxResourceKey = 'maxHealth' | 'maxSanity' | 'maxMana';
const resources: Record<ResourceKey, { label: string; max: MaxResourceKey }> = {
  health: { label: 'Vida', max: 'maxHealth' }, sanity: { label: 'Sanidade', max: 'maxSanity' }, mana: { label: 'Mana', max: 'maxMana' }
};

@Component({ selector: 'app-sheet-layout', standalone: true, imports: [CommonModule, FormsModule, CharacterInventoryComponent, CharacterAbilitiesComponent, CharacterHistoryPanelComponent], template: `
  <div class="sheet-canvas" [style.--sheet-accent]="layout.accentColor">
    <nav *ngIf="mode !== 'print'" class="custom-sheet-tabs" aria-label="Páginas da ficha"><button *ngFor="let page of layout.pages" type="button" [class.active]="activePage().id === page.id" (click)="selectedPageId.set(page.id)">{{ page.title }}</button></nav>
    <section *ngFor="let page of visiblePages()" class="sheet-canvas-page"><h3>{{ page.title }}</h3><div class="sheet-canvas-grid" [class.free-canvas]="page.layoutMode === 'canvas'" [style.aspect-ratio]="page.layoutMode === 'canvas' ? page.aspectRatio || 0.75 : null" [style.background-image]="page.layoutMode === 'canvas' && page.backgroundUrl ? 'url(' + page.backgroundUrl + ')' : null">
      <div *ngFor="let block of page.blocks" class="sheet-canvas-block" [class.plain]="block.style === 'plain'" [class.accent]="block.style === 'accent'" [style.grid-column]="'span ' + block.span" [style.left.%]="page.layoutMode === 'canvas' ? block.x : null" [style.top.%]="page.layoutMode === 'canvas' ? block.y : null" [style.width.%]="page.layoutMode === 'canvas' ? block.width : null" [style.height.%]="page.layoutMode === 'canvas' ? block.height : null">
        <small>{{ block.title || blockLabel(block) }}</small>
        <ng-container *ngIf="block.kind === 'identity'">
          <label *ngIf="mode === 'edit'">Nome<input [ngModel]="draft.name" (ngModelChange)="draft.name = $event; changed.emit()" [ngModelOptions]="{standalone: true}" required minlength="2" maxlength="120"></label>
          <strong *ngIf="mode !== 'edit'">{{ draft.name }}</strong>
        </ng-container>
        <ng-container *ngIf="block.kind === 'portrait'">
          <div class="sheet-canvas-portrait"><img *ngIf="draft.portraitUrl" [src]="draft.portraitUrl" alt=""><span *ngIf="!draft.portraitUrl">Sem retrato</span></div>
          <ng-container *ngIf="mode === 'edit'"><label>URL HTTPS do retrato<input type="url" [ngModel]="draft.portraitUrl" (ngModelChange)="draft.portraitUrl = $event; changed.emit()" [ngModelOptions]="{standalone: true}" placeholder="https://..."></label><label>URL HTTPS do fundo<input type="url" [ngModel]="draft.backgroundUrl" (ngModelChange)="draft.backgroundUrl = $event; changed.emit()" [ngModelOptions]="{standalone: true}" placeholder="https://..."></label></ng-container>
        </ng-container>
        <ng-container *ngIf="block.kind === 'resource'">
          <strong>{{ resourceLabel(block.key) }} · {{ resourceValue(block.key) }} / {{ resourceValue(resourceMaxKey(block)) }}</strong>
          <div class="sheet-resource-track" role="meter" [attr.aria-label]="resourceLabel(block.key)" [attr.aria-valuenow]="resourceValue(block.key)" aria-valuemin="0" [attr.aria-valuemax]="resourceValue(resourceMaxKey(block))"><span [style.width.%]="resourcePercent(block)"></span></div>
          <div *ngIf="mode === 'edit'" class="sheet-resource-controls"><label>Atual<input type="number" [ngModel]="resourceValue(block.key)" (ngModelChange)="setResourceValue(block.key, $event)" [ngModelOptions]="{standalone: true}" min="0" max="999999"></label><label>Máximo<input type="number" [ngModel]="resourceValue(resourceMaxKey(block))" (ngModelChange)="setResourceValue(resourceMaxKey(block), $event)" [ngModelOptions]="{standalone: true}" min="1" max="999999"></label></div>
        </ng-container>
        <ng-container *ngIf="block.kind === 'field'"><ng-container *ngIf="fieldFor(block) as field">
          <label *ngIf="mode === 'edit' && (field.type === 'text' || field.type === 'number')">{{ field.label }}<input [type]="field.type === 'number' ? 'number' : 'text'" [ngModel]="fieldValue(field)" (ngModelChange)="setField(field, $event)" [ngModelOptions]="{standalone: true}" [min]="field.min" [max]="field.max" [attr.maxlength]="field.type === 'text' ? 300 : null"></label>
          <label *ngIf="mode === 'edit' && field.type === 'longtext'">{{ field.label }}<textarea [ngModel]="fieldValue(field)" (ngModelChange)="setField(field, $event)" [ngModelOptions]="{standalone: true}" rows="5" maxlength="10000"></textarea></label>
          <label *ngIf="mode === 'edit' && field.type === 'select'">{{ field.label }}<select [ngModel]="fieldValue(field)" (ngModelChange)="setField(field, $event)" [ngModelOptions]="{standalone: true}"><option value="">Selecione</option><option *ngFor="let option of field.options" [value]="option">{{ option }}</option></select></label>
          <label *ngIf="mode === 'edit' && field.type === 'boolean'" class="checkbox-field"><input type="checkbox" [ngModel]="fieldValue(field) === true" (ngModelChange)="setField(field, $event)" [ngModelOptions]="{standalone: true}"> {{ field.label }}</label>
          <strong *ngIf="mode !== 'edit'" class="sheet-value">{{ field.label }}: {{ displayField(field) }}</strong>
        </ng-container></ng-container>
        <p *ngIf="block.kind === 'note'">{{ block.text }}</p>
        <app-character-inventory *ngIf="block.kind === 'inventory'" [character]="character" />
        <app-character-abilities *ngIf="block.kind === 'abilities'" [character]="character" />
        <app-character-history *ngIf="block.kind === 'history' && mode !== 'print'" [character]="character" (restored)="restored.emit($event)" />
      </div>
    </div></section>
    <section *ngIf="unplacedFields().length" class="sheet-canvas-page"><h3>Campos não posicionados</h3><div class="sheet-canvas-grid"><div *ngFor="let field of unplacedFields()" class="sheet-canvas-block" [style.grid-column]="'span ' + (field.span || 6)">
      <label *ngIf="mode === 'edit' && (field.type === 'text' || field.type === 'number')">{{ field.label }}<input [type]="field.type === 'number' ? 'number' : 'text'" [ngModel]="fieldValue(field)" (ngModelChange)="setField(field, $event)" [ngModelOptions]="{standalone: true}" [min]="field.min" [max]="field.max" [attr.maxlength]="field.type === 'text' ? 300 : null"></label>
      <label *ngIf="mode === 'edit' && field.type === 'longtext'">{{ field.label }}<textarea [ngModel]="fieldValue(field)" (ngModelChange)="setField(field, $event)" [ngModelOptions]="{standalone: true}" rows="5" maxlength="10000"></textarea></label>
      <label *ngIf="mode === 'edit' && field.type === 'select'">{{ field.label }}<select [ngModel]="fieldValue(field)" (ngModelChange)="setField(field, $event)" [ngModelOptions]="{standalone: true}"><option value="">Selecione</option><option *ngFor="let option of field.options" [value]="option">{{ option }}</option></select></label>
      <label *ngIf="mode === 'edit' && field.type === 'boolean'" class="checkbox-field"><input type="checkbox" [ngModel]="fieldValue(field) === true" (ngModelChange)="setField(field, $event)" [ngModelOptions]="{standalone: true}"> {{ field.label }}</label>
      <strong *ngIf="mode !== 'edit'">{{ field.label }}: {{ displayField(field) }}</strong>
    </div></div></section>
  </div>`, styles: [`
    .custom-sheet-tabs { display: flex; gap: 5px; overflow-x: auto; border-bottom: 1px solid var(--border); margin-bottom: 18px; }
    .custom-sheet-tabs button { flex: 0 0 auto; border: 0; border-bottom: 2px solid transparent; background: transparent; color: var(--muted); padding: 12px 14px; }
    .custom-sheet-tabs button.active { border-color: var(--sheet-accent); color: var(--text); }
    .sheet-canvas-block label { display: block; min-width: 0; margin: 12px 0 0; }
    .sheet-resource-controls { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 9px; }
    .sheet-resource-track { height: 5px; margin: 13px 0 5px; background: var(--border); }
    .sheet-resource-track span { display: block; height: 100%; background: var(--sheet-accent); }
    .sheet-canvas-block .sheet-value { font: 16px var(--font-ui); font-weight: 600; white-space: pre-wrap; }
    .sheet-canvas-block app-character-inventory, .sheet-canvas-block app-character-abilities, .sheet-canvas-block app-character-history { display: block; }
  `] })
export class SheetLayoutComponent {
  @Input({ required: true }) layout!: SheetLayout;
  @Input() fields: SystemField[] = [];
  @Input({ required: true }) draft!: CharacterInput;
  @Input({ required: true }) character!: Character;
  @Input() mode: 'edit' | 'read' | 'print' = 'read';
  @Output() changed = new EventEmitter<void>();
  @Output() restored = new EventEmitter<Character>();
  selectedPageId = signal('');
  activePage(): SheetPage { return this.layout.pages.find(page => page.id === this.selectedPageId()) || this.layout.pages[0]; }
  visiblePages() { return this.mode === 'print' ? this.layout.pages : [this.activePage()]; }
  blockLabel(block: SheetBlock) { return block.kind === 'field' ? 'Campo' : block.kind === 'resource' ? 'Recurso' : block.kind === 'note' ? 'Nota' : block.kind === 'identity' ? 'Identidade' : block.kind === 'portrait' ? 'Retrato' : block.kind === 'inventory' ? 'Inventário' : block.kind === 'abilities' ? 'Habilidades' : 'Histórico'; }
  resourceMaxKey(block: SheetBlock): string | null { return block.maxKey || (block.key === 'health' || block.key === 'sanity' || block.key === 'mana' ? resources[block.key].max : null); }
  resourceLabel(key: string | null) { return this.fields.find(field => field.key === key)?.label || ({ health: 'Vida', sanity: 'Sanidade', mana: 'Mana', maxHealth: 'Vida máxima', maxSanity: 'Sanidade máxima', maxMana: 'Mana máxima' } as Record<string, string>)[key || ''] || 'Recurso'; }
  resourceValue(key: string | null): number {
    if (key === 'health' || key === 'sanity' || key === 'mana' || key === 'maxHealth' || key === 'maxSanity' || key === 'maxMana') return this.draft[key];
    return Number(this.draft.fields[key || ''] ?? 0);
  }
  resourcePercent(block: SheetBlock) { const maximum = this.resourceValue(this.resourceMaxKey(block)); return Math.max(0, Math.min(100, maximum > 0 ? this.resourceValue(block.key) / maximum * 100 : 0)); }
  setResourceValue(key: string | null, value: number) {
    if (!key) return;
    if (key === 'health' || key === 'sanity' || key === 'mana' || key === 'maxHealth' || key === 'maxSanity' || key === 'maxMana') { this.draft[key] = Number(value); this.changed.emit(); return; }
    const field = this.fields.find(item => item.key === key && item.type === 'number');
    if (field) this.setField(field, value);
  }
  fieldFor(block: SheetBlock) { return this.fields.find(field => field.key === block.key); }
  fieldValue(field: SystemField) { return this.draft.fields[field.key] ?? ''; }
  displayField(field: SystemField) { const value = this.fieldValue(field); return value === '' ? '—' : field.type === 'boolean' ? value ? 'Sim' : 'Não' : String(value); }
  setField(field: SystemField, value: string | number | boolean) { if (value === '' || value === null) delete this.draft.fields[field.key]; else this.draft.fields[field.key] = field.type === 'number' ? Number(value) : value; this.changed.emit(); }
  unplacedFields() { const placed = new Set(this.layout.pages.flatMap(page => page.blocks.filter(block => block.kind === 'field').map(block => block.key))); return this.fields.filter(field => !placed.has(field.key)); }
}
