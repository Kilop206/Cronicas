import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { ApiService, Character, CharacterRevision, apiError } from './api';

@Component({ selector: 'app-character-history', standalone: true, imports: [CommonModule], template: `
  <section class="editor-panel history-panel" *ngIf="character.canEdit"><div class="panel-heading"><span>ARQUIVO DA FICHA</span><h2>Histórico de alterações</h2></div>
    <p class="room-note">Cada salvamento guarda a versão anterior. Restaurar também mantém uma cópia da versão atual.</p>
    <p *ngIf="error()" class="form-error" role="alert">{{ error() }}</p>
    <div *ngIf="!revisions().length" class="room-note">Ainda não há versões anteriores.</div>
    <div *ngFor="let item of revisions()" class="revision-row"><div><strong>Versão {{ item.version }}</strong><small>{{ item.createdAt | date:'dd/MM/yyyy HH:mm' }} · {{ item.editedBy }}</small></div>
      <button type="button" class="text-link" [disabled]="busy()" (click)="restore(item)">Restaurar</button></div>
  </section>` })
export class CharacterHistoryPanelComponent implements OnChanges {
  @Input({ required: true }) character!: Character; @Output() restored = new EventEmitter<Character>();
  private api = inject(ApiService); revisions = signal<CharacterRevision[]>([]); busy = signal(false); error = signal('');
  ngOnChanges() { if (this.character?.canEdit) this.api.characterRevisions(this.character.id).subscribe({ next: items => this.revisions.set(items), error: e => this.error.set(apiError(e)) }); }
  restore(item: CharacterRevision) {
    if (!confirm(`Restaurar a versão ${item.version} desta ficha?`)) return;
    this.busy.set(true); this.error.set('');
    this.api.restoreCharacterRevision(this.character.id, item.id, this.character.version).subscribe({
      next: character => { this.busy.set(false); this.restored.emit(character); },
      error: e => { this.busy.set(false); this.error.set(apiError(e)); }
    });
  }
}
