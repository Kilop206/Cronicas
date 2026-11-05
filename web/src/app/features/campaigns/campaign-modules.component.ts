import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({ selector: 'app-campaign-modules', standalone: true, imports: [CommonModule, RouterLink], template: `
  <div class="campaign-sections"><details *ngFor="let group of groups; let first = first" [open]="first"><summary><span class="overline">{{ group.kicker }}</span><strong>{{ group.title }}</strong><small>{{ group.description }}</small></summary>
    <div class="section-links"><a *ngFor="let item of group.items" [routerLink]="['/campaigns', campaignId, item.path]"><span>{{ item.name }}</span><small>{{ item.description }}</small><span aria-hidden="true">↗</span></a></div>
  </details></div>`, styles: [`
    .campaign-sections { border-top: 1px solid var(--border); }
    details { border-bottom: 1px solid var(--border); }
    summary { display: grid; grid-template-columns: 92px minmax(120px, 200px) 1fr; align-items: center; gap: 18px; padding: 19px 14px; cursor: pointer; list-style: none; }
    summary::-webkit-details-marker { display: none; }
    summary strong { font: 24px var(--font-editorial); }
    summary small { color: var(--muted); font-size: 12px; }
    .section-links { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 210px), 1fr)); gap: 1px; border-top: 1px solid var(--border); background: var(--border); }
    .section-links a { display: grid; grid-template-columns: 1fr auto; gap: 4px 12px; padding: 18px; background: var(--surface); }
    .section-links a:hover { background: #292d31; }
    .section-links a span:first-child { font-weight: 600; }
    .section-links a small { grid-column: 1; color: var(--muted); font-size: 11px; }
    .section-links a span:last-child { grid-column: 2; grid-row: 1 / span 2; align-self: center; color: var(--gold); }
    @media (max-width: 620px) { summary { grid-template-columns: 1fr; gap: 5px; } }
  `] })
export class CampaignModulesComponent {
  @Input({ required: true }) campaignId = '';
  groups = [
    { kicker: 'AO VIVO', title: 'Mesa', description: 'Transmissão, cenas e turnos.', items: [
      { path: 'room', name: 'Sala de transmissão', description: 'Câmeras, telas, chat e dados' },
      { path: 'encounters', name: 'Encontros', description: 'Iniciativa e recursos' }
    ] },
    { kicker: 'ELENCO', title: 'Personagens', description: 'Fichas e pessoas da história.', items: [
      { path: 'characters', name: 'Personagens', description: 'Fichas dos jogadores' },
      { path: 'npcs', name: 'NPCs', description: 'Elenco do mestre' }
    ] },
    { kicker: 'ARQUIVO', title: 'História', description: 'Memória e referências da campanha.', items: [
      { path: 'sessions', name: 'Sessões', description: 'Registros e resumos' },
      { path: 'wiki', name: 'Mundo', description: 'Lugares, pessoas e facções' },
      { path: 'documents', name: 'Documentos', description: 'Cartas, pistas e arquivos' }
    ] }
  ];
}
