import { CommonModule } from '@angular/common';
import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService, World, apiError } from './api';

@Component({ selector: 'app-campaign-world-panel', standalone: true, imports: [CommonModule, FormsModule, RouterLink], template: `
  <section class="editor-panel campaign-world-panel"><div class="panel-heading"><span>CENÁRIO</span><h2>Universo compartilhado</h2></div>
    <p class="room-note">Campanhas ligadas ao mesmo universo compartilham a enciclopédia do mundo.</p>
    <label *ngIf="role === 'owner'">Universo<select [(ngModel)]="selectedWorldId" name="world"><option value="">Nenhum universo</option><option *ngFor="let item of worlds()" [value]="item.id">{{ item.name }}</option></select></label>
    <p *ngIf="role !== 'owner'">{{ current()?.name || 'Nenhum universo associado' }}</p>
    <div class="form-actions"><button *ngIf="role === 'owner'" type="button" class="button button-small" [disabled]="busy() || selectedWorldId === (worldId || '')" (click)="save()">{{ busy() ? 'Salvando...' : 'Associar universo' }}</button><a [routerLink]="worldId ? ['/worlds', worldId] : ['/worlds']" class="text-link">{{ worldId ? 'Abrir universo' : 'Criar universo' }}</a></div>
    <p *ngIf="error()" class="form-error" role="alert">{{ error() }}</p>
  </section>` })
export class CampaignWorldPanelComponent implements OnInit {
  @Input({ required: true }) campaignId = ''; @Input() worldId: string | null = null; @Input() role = 'player';
  private api = inject(ApiService); worlds = signal<World[]>([]); current = signal<World | null>(null); selectedWorldId = ''; busy = signal(false); error = signal('');
  ngOnInit() { this.selectedWorldId = this.worldId || ''; if (this.worldId) this.api.world(this.worldId).subscribe({ next: item => this.current.set(item), error: e => this.error.set(apiError(e)) }); if (this.role === 'owner') this.api.worlds().subscribe({ next: items => this.worlds.set(items.filter(x => x.ownerId === this.api.user()?.id)), error: e => this.error.set(apiError(e)) }); }
  save() { this.busy.set(true); this.error.set(''); this.api.setCampaignWorld(this.campaignId, this.selectedWorldId || null).subscribe({ next: () => { this.worldId = this.selectedWorldId || null; this.current.set(this.worlds().find(x => x.id === this.worldId) || null); this.busy.set(false); }, error: e => { this.busy.set(false); this.error.set(apiError(e)); } }); }
}
