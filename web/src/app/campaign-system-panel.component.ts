import { CommonModule } from '@angular/common';
import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService, RpgSystem, apiError } from './api';

@Component({ selector: 'app-campaign-system-panel', standalone: true, imports: [CommonModule, FormsModule, RouterLink], template: `
  <section class="editor-panel campaign-system-panel"><div class="panel-heading"><span>REGRAS DA CAMPANHA</span><h2>Sistema de fichas</h2></div>
    <p class="room-note">O sistema define os campos adicionais dos personagens. Fichas existentes preservam seus dados ao trocar o modelo.</p>
    <label *ngIf="role === 'owner'">Sistema<select [(ngModel)]="selectedSystemId" name="system"><option *ngFor="let item of systems()" [value]="item.id">{{ item.name }}</option></select></label>
    <p *ngIf="role !== 'owner'">{{ current()?.name || 'Carregando sistema...' }}</p>
    <div class="form-actions"><button *ngIf="role === 'owner'" type="button" class="button button-small" [disabled]="busy() || !selectedSystemId || selectedSystemId === systemId" (click)="save()">{{ busy() ? 'Salvando...' : 'Aplicar sistema' }}</button><a routerLink="/systems" class="text-link">Biblioteca de sistemas</a></div>
    <p *ngIf="error()" class="form-error" role="alert">{{ error() }}</p><p *ngIf="saved()" class="form-success" role="status">Sistema da campanha atualizado.</p>
  </section>` })
export class CampaignSystemPanelComponent implements OnInit {
  @Input({ required: true }) campaignId = ''; @Input() systemId: string | null = null; @Input() role = 'player';
  private api = inject(ApiService); systems = signal<RpgSystem[]>([]); current = signal<RpgSystem | null>(null); selectedSystemId = ''; busy = signal(false); saved = signal(false); error = signal('');
  ngOnInit() {
    this.selectedSystemId = this.systemId || 'a9e29682-c194-4aae-9c0e-7bbb3f6a72f1';
    this.api.system(this.selectedSystemId).subscribe({ next: item => this.current.set(item), error: e => this.error.set(apiError(e)) });
    if (this.role === 'owner') this.api.systems().subscribe({ next: items => this.systems.set(items), error: e => this.error.set(apiError(e)) });
  }
  save() { this.busy.set(true); this.error.set(''); this.saved.set(false); this.api.setCampaignSystem(this.campaignId, this.selectedSystemId).subscribe({ next: () => { this.systemId = this.selectedSystemId; this.current.set(this.systems().find(x => x.id === this.systemId) || null); this.saved.set(true); this.busy.set(false); }, error: e => { this.error.set(apiError(e)); this.busy.set(false); } }); }
}
