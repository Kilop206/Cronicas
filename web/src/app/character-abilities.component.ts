import { CommonModule } from '@angular/common';
import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { AbilityInput, ApiService, Character, CharacterAbility, DiceRoll, apiError } from './api';

const blank = (): AbilityInput => ({ name: '', description: '', cost: '', effect: '', duration: '', limitations: '', recharge: '', rollExpression: null, version: 1 });

@Component({
  selector: 'app-character-abilities', standalone: true, imports: [CommonModule, FormsModule],
  template: `
    <section class="editor-panel abilities-panel">
      <div class="panel-heading"><span>APTIDÕES</span><h2>Habilidades</h2></div>
      <p *ngIf="loading()" class="muted">Carregando habilidades...</p>
      <p *ngIf="error()" class="notice error" role="alert">{{ error() }}</p>
      <div *ngIf="!loading()" class="ability-list">
        <article *ngFor="let ability of abilities()" class="ability-row">
          <div class="ability-head"><div><h3>{{ ability.name }}</h3><small *ngIf="ability.cost">Custo: {{ ability.cost }}</small></div>
            <div *ngIf="character.canEdit" class="ability-actions"><button type="button" class="text-link" (click)="edit(ability)" [disabled]="busy()">Editar</button><button type="button" class="text-link danger" (click)="remove(ability)" [disabled]="busy()">{{ removingId() === ability.id ? 'Confirmar exclusão' : 'Excluir' }}</button></div></div>
          <p *ngIf="ability.description">{{ ability.description }}</p><p *ngIf="ability.effect"><b>Efeito:</b> {{ ability.effect }}</p>
          <p *ngIf="ability.duration || ability.recharge" class="ability-meta"><span *ngIf="ability.duration">Duração: {{ ability.duration }}</span><span *ngIf="ability.recharge">Recarga: {{ ability.recharge }}</span></p>
          <p *ngIf="ability.limitations" class="ability-meta">Limitações: {{ ability.limitations }}</p>
          <div *ngIf="ability.rollExpression" class="ability-roll"><code>{{ ability.rollExpression }}</code><button *ngIf="character.canEdit" type="button" class="button button-small secondary" (click)="roll(ability)" [disabled]="rollBusy() === ability.id">{{ rollBusy() === ability.id ? 'Rolando...' : 'Rolar na campanha' }}</button><strong *ngIf="lastRoll()?.abilityId === ability.id" role="status">Resultado {{ lastRoll()?.result?.total }}</strong></div>
        </article>
        <p *ngIf="abilities().length === 0" class="muted">Nenhuma habilidade registrada.</p>
      </div>
      <form *ngIf="character.canEdit && !loading()" class="ability-form" (ngSubmit)="save()" #form="ngForm">
        <div class="section-heading"><h3>{{ editingId() ? 'Editar habilidade' : 'Adicionar habilidade' }}</h3><button *ngIf="editingId()" type="button" class="text-link" (click)="cancel()">Cancelar</button></div>
        <div class="ability-fields"><label>Nome<input name="name" [(ngModel)]="draft.name" required maxlength="120"></label><label>Custo<input name="cost" [(ngModel)]="draft.cost" maxlength="80" placeholder="Ex.: 2 mana"></label></div>
        <label>Descrição<textarea name="description" [(ngModel)]="draft.description" rows="2" maxlength="4000"></textarea></label>
        <label>Efeito<textarea name="effect" [(ngModel)]="draft.effect" rows="3" maxlength="4000"></textarea></label>
        <div class="ability-fields"><label>Duração<input name="duration" [(ngModel)]="draft.duration" maxlength="80"></label><label>Recarga<input name="recharge" [(ngModel)]="draft.recharge" maxlength="80"></label></div>
        <label>Limitações<textarea name="limitations" [(ngModel)]="draft.limitations" rows="2" maxlength="1000"></textarea></label>
        <label>Expressão de dados<input name="rollExpression" [(ngModel)]="draft.rollExpression" maxlength="40" placeholder="1d20+5"></label>
        <button class="button button-small" [disabled]="form.invalid || busy()">{{ busy() ? 'Salvando...' : editingId() ? 'Salvar habilidade' : 'Adicionar habilidade' }}</button>
      </form>
    </section>`,
  styles: [`
    .abilities-panel { margin-top: 26px; }
    .ability-list { border-top: 1px solid var(--border); margin: 18px 0; }
    .ability-row { border-bottom: 1px solid var(--border); padding: 18px 0; }
    .ability-head { display: flex; align-items: start; justify-content: space-between; gap: 18px; }
    .ability-head h3 { margin: 0; font: 22px var(--font-editorial); }
    .ability-head small, .ability-meta { color: var(--muted); }
    .ability-actions { display: flex; gap: 12px; }
    .ability-row p { white-space: pre-wrap; line-height: 1.5; margin: 9px 0; }
    .ability-meta span + span { margin-left: 18px; }
    .ability-roll { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; margin-top: 12px; }
    .ability-roll code { color: var(--gold-bright); }
    .ability-roll strong { font-size: 13px; }
    .ability-form { border-top: 1px solid var(--border); padding-top: 18px; }
    .ability-fields { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
    .ability-form label { margin-bottom: 12px; }
    @media (max-width: 600px) { .ability-fields { grid-template-columns: 1fr; } .ability-head { display: block; } .ability-actions { margin-top: 10px; } }
    @media print { .abilities-panel { color: #171717; border: 0; padding: 14px 0; background: white; } .ability-head small, .ability-meta { color: #444; } .ability-actions, .ability-form, .ability-roll button { display: none; } }
  `]
})
export class CharacterAbilitiesComponent implements OnInit {
  @Input({ required: true }) character!: Character;
  private api = inject(ApiService);
  abilities = signal<CharacterAbility[]>([]); loading = signal(true); busy = signal(false); error = signal('');
  editingId = signal<string | null>(null); removingId = signal<string | null>(null); rollBusy = signal<string | null>(null);
  lastRoll = signal<{ abilityId: string; result: DiceRoll } | null>(null); draft = blank();
  ngOnInit() { this.api.abilities(this.character.id).pipe(finalize(() => this.loading.set(false))).subscribe({ next: items => this.abilities.set(items), error: e => this.error.set(apiError(e)) }); }
  edit(ability: CharacterAbility) { this.editingId.set(ability.id); this.removingId.set(null); this.draft = { name: ability.name, description: ability.description, cost: ability.cost, effect: ability.effect, duration: ability.duration, limitations: ability.limitations, recharge: ability.recharge, rollExpression: ability.rollExpression, version: ability.version }; }
  cancel() { this.editingId.set(null); this.draft = blank(); }
  save() {
    if (this.busy()) return;
    this.error.set(''); this.busy.set(true);
    const id = this.editingId();
    const request = id ? this.api.updateAbility(this.character.id, id, this.draft) : this.api.createAbility(this.character.id, this.draft);
    request.pipe(finalize(() => this.busy.set(false))).subscribe({ next: ability => {
      this.abilities.update(items => [...items.filter(x => x.id !== ability.id), ability].sort((a, b) => a.name.localeCompare(b.name)));
      this.cancel();
    }, error: e => this.error.set(apiError(e)) });
  }
  remove(ability: CharacterAbility) {
    if (this.removingId() !== ability.id) { this.removingId.set(ability.id); return; }
    this.error.set(''); this.busy.set(true);
    this.api.deleteAbility(this.character.id, ability.id, ability.version).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => { this.abilities.update(items => items.filter(x => x.id !== ability.id)); this.removingId.set(null); },
      error: e => { this.error.set(apiError(e)); this.removingId.set(null); }
    });
  }
  roll(ability: CharacterAbility) {
    if (!ability.rollExpression) return;
    this.error.set(''); this.rollBusy.set(ability.id);
    this.api.roll(this.character.campaignId, ability.rollExpression, 'campaign', crypto.randomUUID())
      .pipe(finalize(() => this.rollBusy.set(null))).subscribe({
        next: result => this.lastRoll.set({ abilityId: ability.id, result }), error: e => this.error.set(apiError(e))
      });
  }
}
