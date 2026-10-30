import { CommonModule } from '@angular/common';
import { Component, Input, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { ApiService, apiError } from './api';
import { SheetValues, fieldRoll } from './sheet-rules';

@Component({ selector: 'app-sheet-roll', standalone: true, imports: [CommonModule], template: `
  <div class="sheet-roll" [class.compact]="compact"><button type="button" class="button button-small secondary" (click)="roll()" [disabled]="busy()" [attr.aria-label]="'Rolar ' + label">{{ busy() ? 'Rolando...' : compact ? 'Rolar' : 'Rolar ' + label }}</button>
    <select [value]="visibility()" (change)="visibility.set($any($event.target).value)" [attr.aria-label]="'Visibilidade da rolagem de ' + label"><option value="campaign">Mesa</option><option value="private">Privada</option></select>
    <select *ngIf="percentileTarget !== null" [value]="bonusDice()" (change)="bonusDice.set(+$any($event.target).value)" [attr.aria-label]="'Dados de bônus ou penalidade de ' + label"><option [value]="0">Normal</option><option [value]="1">1 bônus</option><option [value]="2">2 bônus</option><option [value]="-1">1 penalidade</option><option [value]="-2">2 penalidades</option></select>
    <span *ngIf="total() !== null" role="status">{{ resultLabel() }}</span><span *ngIf="error()" class="form-error" role="alert">{{ error() }}</span></div>`,
  styles: [`.sheet-roll { display: flex; align-items: center; flex-wrap: wrap; gap: 8px; margin-top: 8px; font-size: 13px; } .sheet-roll.compact { margin-top: 0; } .sheet-roll.compact button { min-height: 30px; padding: 5px 9px; } .sheet-roll select { width:auto; min-height:30px; margin:0; padding:3px 7px; border:1px solid var(--border,#777); background:var(--surface-raised,#fff); color:var(--text,#111); font-size:11px; } .sheet-roll select:focus-visible{outline:2px solid var(--text,#111);outline-offset:2px} @media print { .sheet-roll { display: none; } }`]
})
export class SheetRollComponent {
  @Input({ required: true }) campaignId!: string;
  @Input({ required: true }) expression!: string;
  @Input({ required: true }) values!: SheetValues;
  @Input() label = 'dados';
  @Input() compact = false;
  @Input() percentileTarget: number | null = null;
  private api = inject(ApiService);
  busy = signal(false); total = signal<number | null>(null); error = signal(''); detail = signal(''); visibility = signal<'campaign' | 'private'>('campaign'); bonusDice = signal(0);
  resultLabel() {
    const total = this.total(); if (total === null) return '';
    const target = this.percentileTarget;
    const percentile = target !== null && Number.isFinite(target) && target >= 0 && target <= 100;
    const degree = percentile ? total <= Math.floor(target / 5) ? 'sucesso extremo' : total <= Math.floor(target / 2) ? 'sucesso difícil' : total <= target ? 'sucesso regular' : 'falha' : '';
    return `${this.detail()} = ${total}${percentile ? ` · ${degree} (alvo ${target}%)` : ''}`;
  }
  roll() {
    const expression = fieldRoll(this.expression, this.values);
    if (!expression) { this.error.set('Expressão ou modificador inválido.'); return; }
    this.busy.set(true); this.error.set('');
    const percentile = this.percentileTarget !== null && expression === '1d100';
    const request = percentile
      ? this.api.percentileRoll(this.campaignId, this.percentileTarget!, this.bonusDice(), this.visibility(), crypto.randomUUID())
      : this.api.roll(this.campaignId, expression, this.visibility(), crypto.randomUUID());
    request.pipe(finalize(() => this.busy.set(false)))
      .subscribe({ next: result => { this.total.set(result.total); this.detail.set(`${result.expression} [${result.rolls.join(', ')}]`); }, error: err => this.error.set(apiError(err)) });
  }
}
