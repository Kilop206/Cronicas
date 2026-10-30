import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CharacterInput, SheetCellValue, SystemColumn, SystemField } from './api';
import { SheetRollComponent } from './sheet-roll.component';

type SheetRow = Record<string, SheetCellValue>;

@Component({ selector: 'app-sheet-collection-field', standalone: true, imports: [CommonModule, FormsModule, SheetRollComponent], template: `
  <div class="sheet-collection-field" [attr.aria-label]="field.label">
    <ul *ngIf="field.type === 'list'" class="sheet-custom-list"><li *ngFor="let item of items(); let index=index">
      <input *ngIf="mode === 'edit'; else readItem" [attr.aria-label]="field.label + ' item ' + (index + 1)" [ngModel]="item" (ngModelChange)="setItem(index,$event)" [ngModelOptions]="{standalone:true}" maxlength="300">
      <ng-template #readItem><span>{{ item }}</span></ng-template>
      <button *ngIf="mode === 'edit'" type="button" [attr.aria-label]="'Remover item ' + (index + 1)" (click)="removeItem(index)">×</button>
    </li></ul>
    <div *ngIf="field.type === 'table'" class="sheet-custom-table"><table><thead><tr><th *ngFor="let column of field.columns">{{ column.label }}</th><th *ngIf="mode === 'edit'"><span class="sr-only">Ações</span></th></tr></thead>
      <tbody><tr *ngFor="let row of rows(); let index=index"><td *ngFor="let column of field.columns">
        <ng-container *ngIf="mode === 'edit'; else readCell"><input [type]="column.type === 'boolean' ? 'checkbox' : column.type === 'number' ? 'number' : 'text'" [attr.aria-label]="column.label + ' linha ' + (index + 1)" [ngModel]="cell(row,column)" (ngModelChange)="setCell(index,column,$event)" [ngModelOptions]="{standalone:true}" [attr.maxlength]="column.type === 'text' ? 300 : null"></ng-container>
        <ng-template #readCell>{{ column.type === 'boolean' ? cell(row,column) === true ? 'Sim' : 'Não' : cell(row,column) }}</ng-template>
        <app-sheet-roll *ngIf="mode !== 'print' && column.type === 'dice' && cell(row,column)" [campaignId]="campaignId" [expression]="String(cell(row,column))" [values]="draft.fields" [label]="field.label + ' ' + (index + 1)" [compact]="true" />
      </td><td *ngIf="mode === 'edit'"><button type="button" [attr.aria-label]="'Remover linha ' + (index + 1)" (click)="removeRow(index)">×</button></td></tr></tbody></table></div>
    <p *ngIf="mode !== 'edit' && !(field.type === 'list' ? items().length : rows().length)" class="muted">Nenhum registro.</p>
    <button *ngIf="mode === 'edit'" type="button" class="button button-small secondary" [disabled]="(field.type === 'list' ? items().length : rows().length) >= rowLimit()" (click)="add()">{{ field.type === 'list' ? 'Adicionar item' : 'Adicionar linha' }}</button>
  </div>`, styles: [`
    .sheet-collection-field { display:grid; gap:11px; min-width:0; }
    .sheet-custom-list { margin:0; padding-left:20px; display:grid; gap:6px; }
    .sheet-custom-list li { display:flex; gap:7px; align-items:center; }
    .sheet-custom-list input { flex:1; min-width:0; margin:0; }
    .sheet-custom-list button,.sheet-custom-table button { min-width:32px; min-height:32px; background:transparent; color:var(--text); border:1px solid var(--border); cursor:pointer; }
    .sheet-custom-table { max-width:100%; overflow-x:auto; }
    table { width:100%; border-collapse:collapse; font:13px var(--font-ui); }
    th,td { padding:7px; border:1px solid var(--border); text-align:left; min-width:90px; }
    th { color:var(--muted); font-size:11px; text-transform:uppercase; }
    td input { width:100%; min-width:70px; margin:0; }
    .sr-only { position:absolute; width:1px; height:1px; padding:0; margin:-1px; overflow:hidden; clip:rect(0,0,0,0); white-space:nowrap; border:0; }
  `] })
export class SheetCollectionFieldComponent {
  @Input({ required: true }) field!: SystemField;
  @Input({ required: true }) draft!: CharacterInput;
  @Input() campaignId = '';
  @Input() mode: 'read' | 'edit' | 'print' = 'read';
  @Output() changed = new EventEmitter<void>();
  items(): string[] { const value = this.draft.fields[this.field.key]; return Array.isArray(value) && value.every(item => typeof item === 'string') ? value as string[] : []; }
  rowLimit() { return this.field.key === 'estado_regras' ? 100 : 30; }
  rows(): SheetRow[] { const value = this.draft.fields[this.field.key]; return Array.isArray(value) && value.every(item => item && typeof item === 'object' && !Array.isArray(item)) ? value as SheetRow[] : []; }
  cell(row: SheetRow, column: SystemColumn): SheetCellValue { return row[column.key] ?? (column.type === 'boolean' ? false : column.type === 'number' ? 0 : ''); }
  String(value: SheetCellValue) { return String(value); }
  private set(value: string[] | SheetRow[]) { this.draft.fields[this.field.key] = value; this.changed.emit(); }
  setItem(index: number, value: string) { const items = [...this.items()]; items[index] = value; this.set(items); }
  removeItem(index: number) { this.set(this.items().filter((_, position) => position !== index)); }
  setCell(index: number, column: SystemColumn, value: SheetCellValue) { const rows = this.rows().map(row => ({ ...row })); rows[index][column.key] = column.type === 'number' ? Number(value) : value; this.set(rows); }
  removeRow(index: number) { this.set(this.rows().filter((_, position) => position !== index)); }
  add() { if (this.field.type === 'list' && this.items().length < 30) this.set([...this.items(), '']);
    if (this.field.type === 'table' && this.rows().length < this.rowLimit()) this.set([...this.rows(), Object.fromEntries((this.field.columns || []).map(column => [column.key, this.field.key === 'ataques_regras' ? ({ margem: 20, multiplicador: 2, municao: -1, falha: 100, dano: '1', dano_extra: this.field.columns?.some(item => item.key === 'empala') ? '' : '0' } as Record<string, SheetCellValue>)[column.key] ?? this.cell({}, column) : this.cell({}, column)]))]); }
}
