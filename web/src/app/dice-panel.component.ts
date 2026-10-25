import { CommonModule } from '@angular/common';
import { Component, Input, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription, interval } from 'rxjs';
import { ApiService, DiceRoll, apiError } from './api';

@Component({ selector: 'app-dice-panel', standalone: true, imports: [CommonModule, FormsModule], template: `
  <section class="dice-panel" aria-label="Rolagens de dados">
    <div class="panel-heading"><span>MESA / DADOS</span><h2>Rolagens</h2></div>
    <form class="dice-form" (ngSubmit)="submit()" #form="ngForm">
      <label>Expressão<input name="expression" [(ngModel)]="expression" required maxlength="40" placeholder="2d20kh1+5" autocomplete="off"></label>
      <label>Visibilidade<select name="visibility" [(ngModel)]="visibility"><option value="campaign">Campanha</option><option value="private">Só para mim</option><option *ngIf="master()" value="gm">Mestres</option></select></label>
      <button class="button" [disabled]="form.invalid || busy() || spectator()">{{ busy() ? 'Rolando...' : 'Rolar dados' }}</button>
    </form>
    <p class="room-note">Use d4, d6, d8, d10, d12, d20 ou d100. Exemplos: 1d20+5, 4d8, 2d20kh1, 2d20kl1.</p>
    <p *ngIf="error()" class="form-error" role="alert">{{ error() }}</p>
    <div *ngIf="!rolls().length" class="empty-state small"><h3>O silêncio antes da rolagem</h3><p>Os resultados aparecerão aqui e ficarão guardados na campanha.</p></div>
    <ol class="roll-history"><li *ngFor="let roll of rolls()"><div><strong>{{ roll.userName }}</strong><span>{{ roll.createdAt | date:'dd/MM HH:mm' }} · {{ visibilityLabel(roll.visibility) }}</span></div>
      <div class="roll-result"><code>{{ roll.expression }}</code><strong>{{ roll.total }}</strong></div>
      <small><span *ngFor="let die of roll.rolls; let index = index" [class.dropped]="!roll.keptIndexes.includes(index)">{{ die }}<span *ngIf="index < roll.rolls.length - 1">, </span></span><span *ngIf="roll.modifier"> {{ roll.modifier > 0 ? '+' : '' }}{{ roll.modifier }}</span></small>
    </li></ol>
  </section>` })
export class DicePanelComponent implements OnInit, OnDestroy {
  @Input({ required: true }) campaignId = '';
  private api = inject(ApiService); private poll?: Subscription;
  expression = '1d20'; visibility: DiceRoll['visibility'] = 'campaign'; rolls = signal<DiceRoll[]>([]); error = signal(''); busy = signal(false); master = signal(false); spectator = signal(false);
  ngOnInit() {
    this.api.campaign(this.campaignId).subscribe({ next: c => { this.master.set(['owner', 'gm', 'co-gm'].includes(c.role)); this.spectator.set(c.role === 'spectator'); }, error: e => this.error.set(apiError(e)) });
    this.refresh(); this.poll = interval(3000).subscribe(() => this.refresh(true));
  }
  ngOnDestroy() { this.poll?.unsubscribe(); }
  refresh(silent = false) { this.api.rolls(this.campaignId).subscribe({ next: rolls => this.rolls.set(rolls.slice().reverse()), error: e => { if (!silent) this.error.set(apiError(e)); } }); }
  submit() {
    this.error.set(''); this.busy.set(true);
    this.api.roll(this.campaignId, this.expression.trim(), this.visibility, crypto.randomUUID()).subscribe({
      next: () => { this.busy.set(false); this.refresh(); }, error: e => { this.busy.set(false); this.error.set(apiError(e)); }
    });
  }
  visibilityLabel(value: DiceRoll['visibility']) { return value === 'campaign' ? 'Pública' : value === 'gm' ? 'Mestres' : 'Privada'; }
}
