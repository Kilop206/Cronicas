import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService, Npc, NpcInput, Page, apiError } from './api';

const blankNpc = (): NpcInput => ({
  name: '', portraitUrl: null, appearance: '', personality: '', motivation: '', relationships: '',
  location: '', secrets: '', status: 'active', visibility: 'gm', health: 10, maxHealth: 10, version: 1
});

@Component({ standalone: true, imports: [CommonModule, FormsModule, RouterLink], template: `
  <main class="page"><a [routerLink]="['/campaigns', campaignId]" class="back-link">← Voltar à campanha</a>
    <div class="page-heading"><div><span class="overline">ELENCO / MESTRE</span><h1>NPCs</h1><p>Rostos, vínculos e segredos que movem a história.</p></div><button *ngIf="master()" type="button" class="button button-small" (click)="newNpc()">Novo NPC</button></div>
    <p *ngIf="error()" class="notice error" role="alert">{{ error() }}</p>
    <div class="narrative-layout"><section class="narrative-list"><label class="search-field"><span aria-hidden="true">⌕</span><input type="search" [(ngModel)]="search" (ngModelChange)="searchChanged()" placeholder="Buscar NPCs" aria-label="Buscar NPCs"></label>
      <p *ngIf="loading()" class="loading">Abrindo o elenco...</p>
      <p *ngIf="!loading() && !page()?.items?.length" class="empty-state small">{{ search ? 'Nenhum NPC encontrado.' : master() ? 'Crie o primeiro NPC desta campanha.' : 'Nenhum NPC foi apresentado à campanha.' }}</p>
      <button *ngFor="let npc of page()?.items" type="button" class="narrative-row" [class.selected]="selected()?.id === npc.id" (click)="select(npc.id)"><span class="overline">{{ statusLabel(npc.status) }} · {{ npc.visibility === 'gm' ? 'MESTRES' : 'CAMPANHA' }}</span><strong>{{ npc.name }}</strong><small>{{ npc.location || 'Localização desconhecida' }}</small></button>
      <div class="pagination" *ngIf="(page()?.total || 0) > 20"><button type="button" [disabled]="pageNumber === 1" (click)="changePage(-1)">Anterior</button><span>Página {{ pageNumber }}</span><button type="button" [disabled]="pageNumber * 20 >= (page()?.total || 0)" (click)="changePage(1)">Próxima</button></div>
    </section><section *ngIf="selected() as npc" class="editor-panel narrative-detail" [style.display]="editing() ? 'none' : null"><div class="panel-heading"><span>{{ statusLabel(npc.status) }}</span><h2>{{ npc.name }}</h2></div>
      <div class="npc-portrait" *ngIf="npc.portraitUrl"><img [src]="npc.portraitUrl" [alt]="'Retrato de ' + npc.name"></div>
      <p class="narrative-meta">{{ npc.visibility === 'gm' ? 'Apenas mestres' : 'Visível à campanha' }} · Vida {{ npc.health }}/{{ npc.maxHealth }}</p>
      <dl class="npc-details"><ng-container *ngFor="let field of details(npc)"><dt>{{ field.label }}</dt><dd>{{ field.value || '—' }}</dd></ng-container></dl>
      <div *ngIf="master()" class="npc-secret"><span class="overline">SOMENTE MESTRES</span><p>{{ npc.secrets || 'Nenhum segredo registrado.' }}</p></div>
      <button *ngIf="master()" type="button" class="button button-small secondary" (click)="edit(npc)">Editar NPC</button>
    </section>
    <form *ngIf="editing()" class="editor-panel narrative-detail" (ngSubmit)="save()" #form="ngForm"><div class="panel-heading"><span>SOMENTE MESTRES</span><h2>{{ editingId ? 'Editar NPC' : 'Novo NPC' }}</h2></div>
      <label>Nome<input name="name" [(ngModel)]="draft.name" required minlength="2" maxlength="120"></label>
      <label>URL HTTPS do retrato<input name="portraitUrl" [(ngModel)]="draft.portraitUrl" type="url" maxlength="2048" placeholder="https://..."></label>
      <label>Aparência<textarea name="appearance" [(ngModel)]="draft.appearance" rows="3" maxlength="2000"></textarea></label>
      <label>Personalidade<textarea name="personality" [(ngModel)]="draft.personality" rows="3" maxlength="2000"></textarea></label>
      <label>Motivação<textarea name="motivation" [(ngModel)]="draft.motivation" rows="3" maxlength="2000"></textarea></label>
      <label>Relações<textarea name="relationships" [(ngModel)]="draft.relationships" rows="3" maxlength="2000"></textarea></label>
      <label>Localização<input name="location" [(ngModel)]="draft.location" maxlength="160"></label>
      <div class="resource-fields"><label>Vida<input name="health" type="number" [(ngModel)]="draft.health" required min="0" [max]="draft.maxHealth"></label><label>Vida máxima<input name="maxHealth" type="number" [(ngModel)]="draft.maxHealth" required min="1" max="9999"></label></div>
      <label>Status<select name="status" [(ngModel)]="draft.status"><option value="active">Ativo</option><option value="missing">Desaparecido</option><option value="deceased">Falecido</option><option value="unknown">Desconhecido</option></select></label>
      <label>Visibilidade<select name="visibility" [(ngModel)]="draft.visibility"><option value="gm">Somente mestres</option><option value="campaign">Campanha</option></select></label>
      <label>Segredos do mestre<textarea name="secrets" [(ngModel)]="draft.secrets" rows="5" maxlength="10000"></textarea></label>
      <div class="form-actions"><button type="button" class="text-link" (click)="editing.set(false)">Cancelar</button><button *ngIf="editingId" type="button" class="text-link danger" [disabled]="busy()" (click)="remove()">Excluir</button><button class="button" [disabled]="form.invalid || busy()">{{ busy() ? 'Salvando...' : 'Salvar NPC' }}</button></div>
    </form><div *ngIf="!selected() && !editing()" class="narrative-placeholder"><p>Selecione um NPC para consultar sua história.</p></div></div>
  </main>`, styles: [`
    .npc-portrait { width: min(100%, 260px); aspect-ratio: 4 / 3; margin-bottom: 18px; overflow: hidden; background: var(--surface); }
    .npc-portrait img { width: 100%; height: 100%; object-fit: cover; }
    .npc-details { display: grid; grid-template-columns: minmax(100px, 145px) 1fr; gap: 12px 18px; margin: 24px 0; }
    .npc-details dt { color: var(--muted); font-size: 12px; }
    .npc-details dd { margin: 0; white-space: pre-wrap; overflow-wrap: anywhere; }
    .npc-secret { border-top: 1px solid var(--border); padding: 20px 0; white-space: pre-wrap; }
    @media (max-width: 620px) { .npc-details { grid-template-columns: 1fr; gap: 4px; } .npc-details dd { margin-bottom: 12px; } }
  `] })
export class NpcsPage implements OnInit, OnDestroy {
  private api = inject(ApiService); private route = inject(ActivatedRoute);
  campaignId = this.route.snapshot.paramMap.get('id')!;
  page = signal<Page<Npc> | null>(null); selected = signal<Npc | null>(null); master = signal(false);
  loading = signal(true); busy = signal(false); editing = signal(false); error = signal('');
  pageNumber = 1; search = ''; draft = blankNpc(); editingId: string | null = null;
  private searchTimer?: ReturnType<typeof setTimeout>;
  ngOnInit() {
    this.api.campaign(this.campaignId).subscribe({ next: campaign => this.master.set(['owner', 'gm', 'co-gm'].includes(campaign.role)), error: error => this.error.set(apiError(error)) });
    this.load();
    const npcId = this.route.snapshot.queryParamMap.get('npc');
    if (npcId) this.select(npcId);
  }
  ngOnDestroy() { clearTimeout(this.searchTimer); }
  load() { this.loading.set(true); this.api.npcs(this.campaignId, this.pageNumber, this.search).pipe(finalize(() => this.loading.set(false))).subscribe({ next: page => this.page.set(page), error: error => this.error.set(apiError(error)) }); }
  searchChanged() { clearTimeout(this.searchTimer); this.searchTimer = setTimeout(() => { this.pageNumber = 1; this.load(); }, 250); }
  changePage(step: number) { this.pageNumber += step; this.load(); }
  select(id: string) { this.error.set(''); this.editing.set(false); this.api.npc(id).subscribe({ next: npc => this.selected.set(npc), error: error => this.error.set(apiError(error)) }); }
  newNpc() { this.editingId = null; this.selected.set(null); this.draft = blankNpc(); this.editing.set(true); this.error.set(''); }
  edit(npc: Npc) { this.editingId = npc.id; this.draft = { name: npc.name, portraitUrl: npc.portraitUrl, appearance: npc.appearance, personality: npc.personality, motivation: npc.motivation, relationships: npc.relationships, location: npc.location, secrets: npc.secrets ?? '', status: npc.status, visibility: npc.visibility, health: npc.health, maxHealth: npc.maxHealth, version: npc.version }; this.editing.set(true); this.error.set(''); }
  save() {
    if (!this.master()) return;
    this.busy.set(true); this.error.set('');
    const request = this.editingId ? this.api.updateNpc(this.editingId, this.draft) : this.api.createNpc(this.campaignId, this.draft);
    request.pipe(finalize(() => this.busy.set(false))).subscribe({ next: npc => { this.editing.set(false); this.selected.set(npc); this.load(); }, error: error => this.error.set(apiError(error)) });
  }
  remove() {
    if (!this.editingId || !confirm(`Excluir ${this.draft.name}?`)) return;
    this.busy.set(true); this.error.set('');
    this.api.deleteNpc(this.editingId, this.draft.version).pipe(finalize(() => this.busy.set(false))).subscribe({ next: () => { this.editing.set(false); this.selected.set(null); this.load(); }, error: error => this.error.set(apiError(error)) });
  }
  statusLabel(status: Npc['status']) { return status === 'missing' ? 'DESAPARECIDO' : status === 'deceased' ? 'FALECIDO' : status === 'unknown' ? 'DESCONHECIDO' : 'ATIVO'; }
  details(npc: Npc) { return [
    { label: 'Aparência', value: npc.appearance }, { label: 'Personalidade', value: npc.personality },
    { label: 'Motivação', value: npc.motivation }, { label: 'Relações', value: npc.relationships },
    { label: 'Localização', value: npc.location }
  ]; }
}
