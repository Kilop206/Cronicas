import { CommonModule } from '@angular/common';
import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ApiService, Character, InventoryInput, InventoryItem, apiError } from './api';

const blank = (): InventoryInput => ({ name: '', category: 'item', quantity: 1, weight: 0, notes: '', version: 1 });

@Component({
  selector: 'app-character-inventory', standalone: true, imports: [CommonModule, FormsModule],
  template: `
    <section class="editor-panel inventory-panel">
      <div class="panel-heading"><span>PERTENCES</span><h2>Inventário</h2></div>
      <p class="inventory-summary">{{ items().length }} itens · Peso total {{ totalWeight().toLocaleString('pt-BR', { maximumFractionDigits: 2 }) }}</p>
      <p *ngIf="loading()" class="muted">Carregando inventário...</p>
      <p *ngIf="error()" class="notice error" role="alert">{{ error() }}</p>
      <div *ngIf="!loading()" class="inventory-list">
        <article *ngFor="let item of items()" class="inventory-row">
          <div><strong>{{ item.name }}</strong><small>{{ categoryLabel(item.category) }} · {{ item.quantity }} × {{ item.weight }} de peso</small><p *ngIf="item.notes">{{ item.notes }}</p></div>
          <div *ngIf="character.canEdit" class="inventory-actions">
            <button type="button" class="text-link" (click)="edit(item)" [disabled]="busy()">Editar</button>
            <button type="button" class="text-link danger" (click)="remove(item)" [disabled]="busy()">{{ removingId() === item.id ? 'Confirmar exclusão' : 'Excluir' }}</button>
          </div>
        </article>
        <p *ngIf="items().length === 0" class="muted">Nenhum item registrado.</p>
      </div>
      <form *ngIf="character.canEdit && !loading()" class="inventory-form" (ngSubmit)="save()" #form="ngForm">
        <div class="section-heading"><h3>{{ editingId() ? 'Editar item' : 'Adicionar item' }}</h3><button *ngIf="editingId()" type="button" class="text-link" (click)="cancel()">Cancelar</button></div>
        <div class="inventory-fields">
          <label class="inventory-name">Nome<input name="name" [(ngModel)]="draft.name" required maxlength="120" placeholder="Nome do item"></label>
          <label>Categoria<select name="category" [(ngModel)]="draft.category"><option value="item">Item</option><option value="equipment">Equipamento</option><option value="weapon">Arma</option><option value="consumable">Consumível</option><option value="other">Outro</option></select></label>
          <label>Quantidade<input name="quantity" [(ngModel)]="draft.quantity" type="number" required min="1" max="9999"></label>
          <label>Peso por unidade<input name="weight" [(ngModel)]="draft.weight" type="number" required min="0" max="9999.99" step="0.01"></label>
        </div>
        <label>Notas<textarea name="notes" [(ngModel)]="draft.notes" maxlength="1000" rows="2" placeholder="Propriedades, origem ou uso"></textarea></label>
        <button class="button button-small" [disabled]="form.invalid || busy()">{{ busy() ? 'Salvando...' : editingId() ? 'Salvar item' : 'Adicionar ao inventário' }}</button>
      </form>
    </section>`,
  styles: [`
    .inventory-panel { margin-top: 26px; }
    .inventory-summary { color: var(--muted); font-size: 13px; }
    .inventory-list { border-top: 1px solid var(--border); margin: 18px 0; }
    .inventory-row { display: flex; align-items: start; justify-content: space-between; gap: 18px; border-bottom: 1px solid var(--border); padding: 14px 0; }
    .inventory-row strong { display: block; font: 20px var(--font-editorial); }
    .inventory-row small { display: block; color: var(--muted); margin-top: 3px; }
    .inventory-row p { margin: 8px 0 0; white-space: pre-wrap; }
    .inventory-actions { display: flex; flex-wrap: wrap; gap: 12px; }
    .inventory-form { border-top: 1px solid var(--border); padding-top: 18px; }
    .inventory-fields { display: grid; grid-template-columns: minmax(180px, 2fr) repeat(3, minmax(95px, 1fr)); gap: 12px; }
    .inventory-form label { margin-bottom: 12px; }
    @media (max-width: 720px) { .inventory-fields { grid-template-columns: repeat(2, minmax(0, 1fr)); } .inventory-name { grid-column: 1 / -1; } }
    @media (max-width: 430px) { .inventory-row { display: block; } .inventory-actions { margin-top: 10px; } }
    @media print { .inventory-panel { color: #171717; border: 0; padding: 14px 0; background: white; } .inventory-summary, .inventory-row small { color: #444; } .inventory-actions, .inventory-form { display: none; } }
  `]
})
export class CharacterInventoryComponent implements OnInit {
  @Input({ required: true }) character!: Character;
  private api = inject(ApiService);
  items = signal<InventoryItem[]>([]); loading = signal(true); busy = signal(false); error = signal('');
  editingId = signal<string | null>(null); removingId = signal<string | null>(null); draft = blank();
  ngOnInit() { this.load(); }
  load() { this.api.inventory(this.character.id).pipe(finalize(() => this.loading.set(false))).subscribe({ next: items => this.items.set(items), error: e => this.error.set(apiError(e)) }); }
  totalWeight() { return this.items().reduce((sum, item) => sum + item.quantity * item.weight, 0); }
  categoryLabel(category: InventoryItem['category']) { return ({ item: 'Item', equipment: 'Equipamento', weapon: 'Arma', consumable: 'Consumível', other: 'Outro' })[category]; }
  edit(item: InventoryItem) { this.editingId.set(item.id); this.removingId.set(null); this.draft = { name: item.name, category: item.category, quantity: item.quantity, weight: item.weight, notes: item.notes, version: item.version }; }
  cancel() { this.editingId.set(null); this.draft = blank(); }
  save() {
    if (this.busy()) return;
    this.error.set(''); this.busy.set(true);
    const id = this.editingId();
    const request = id ? this.api.updateInventoryItem(this.character.id, id, this.draft) : this.api.createInventoryItem(this.character.id, this.draft);
    request.pipe(finalize(() => this.busy.set(false))).subscribe({ next: item => {
      this.items.update(items => [...items.filter(x => x.id !== item.id), item].sort((a, b) => a.category.localeCompare(b.category) || a.name.localeCompare(b.name)));
      this.cancel();
    }, error: e => this.error.set(apiError(e)) });
  }
  remove(item: InventoryItem) {
    if (this.removingId() !== item.id) { this.removingId.set(item.id); return; }
    this.error.set(''); this.busy.set(true);
    this.api.deleteInventoryItem(this.character.id, item.id, item.version).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => { this.items.update(items => items.filter(x => x.id !== item.id)); this.removingId.set(null); },
      error: e => { this.error.set(apiError(e)); this.removingId.set(null); }
    });
  }
}
