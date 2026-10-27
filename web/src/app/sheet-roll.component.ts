import { CommonModule } from '@angular/common';
import { Component, Input, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { ApiService, apiError } from './api';
import { SheetValues, fieldRoll } from './sheet-rules';

@Component({ selector: 'app-sheet-roll', standalone: true, imports: [CommonModule], template: `
  <div class="sheet-roll" [class.compact]="compact"><button type="button" class="button button-small secondary" (click)="roll()" [disabled]="busy()" [attr.aria-label]="'Rolar ' + label">{{ busy() ? 'Rolando...' : compact ? 'Rolar' : 'Rolar ' + label }}</button>
    <span *ngIf="total() !== null" role="status">Resultado {{ total() }}</span><span *ngIf="error()" class="form-error" role="alert">{{ error() }}</span></div>`,
  styles: [`.sheet-roll { display: flex; align-items: center; flex-wrap: wrap; gap: 8px; margin-top: 8px; font-size: 13px; } .sheet-roll.compact { margin-top: 0; } .sheet-roll.compact button { min-height: 30px; padding: 5px 9px; } @media print { .sheet-roll { display: none; } }`]
})
export class SheetRollComponent {
  @Input({ required: true }) campaignId!: string;
  @Input({ required: true }) expression!: string;
  @Input({ required: true }) values!: SheetValues;
  @Input() label = 'dados';
  @Input() compact = false;
  private api = inject(ApiService);
  busy = signal(false); total = signal<number | null>(null); error = signal('');
  roll() {
    const expression = fieldRoll(this.expression, this.values);
    if (!expression) { this.error.set('Expressão ou modificador inválido.'); return; }
    this.busy.set(true); this.error.set('');
    this.api.roll(this.campaignId, expression, 'campaign', crypto.randomUUID()).pipe(finalize(() => this.busy.set(false)))
      .subscribe({ next: result => this.total.set(result.total), error: err => this.error.set(apiError(err)) });
  }
}
