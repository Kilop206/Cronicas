import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription, finalize } from 'rxjs';
import { ApiService, Campaign, NarrativeEntry, NarrativeInput, NarrativeKind, Page, apiError } from './api';

const emptyInput = (): NarrativeInput => ({ title: '', body: '', category: 'general', visibility: 'private', eventAt: null, revision: 1 });

@Component({ standalone: true, imports: [CommonModule, FormsModule, RouterLink], template: `
  <main class="page narrative-page">
    <a [routerLink]="['/campaigns', campaignId]" class="back-link">← Voltar à campanha</a>
    <div class="page-heading"><div><span class="overline">ARQUIVO DA CAMPANHA</span><h1>{{ heading() }}</h1><p>{{ subtitle() }}</p></div>
      <button *ngIf="canCreate()" type="button" class="button" (click)="newEntry()">+ Novo registro</button></div>
    <div *ngIf="error()" class="notice error" role="alert">{{ error() }}</div>
    <div class="narrative-layout">
      <section aria-label="Registros" class="narrative-list">
        <label class="search-field"><span aria-hidden="true">⌕</span><input type="search" [(ngModel)]="search" (ngModelChange)="onSearch()" placeholder="Buscar no arquivo" aria-label="Buscar registros"></label>
        <div *ngIf="loading()" class="loading">Abrindo registros...</div>
        <div *ngIf="!loading() && !page()?.items?.length" class="empty-state small"><h2>Nenhum registro encontrado</h2><p>{{ search ? 'Tente outro termo.' : 'Comece um capítulo deste arquivo.' }}</p></div>
        <button *ngFor="let item of page()?.items" type="button" class="narrative-row" [class.selected]="selected()?.id === item.id" (click)="select(item)">
          <span class="overline">{{ item.category }} · {{ visibilityLabel(item.visibility) }}</span><strong>{{ item.title }}</strong>
          <small>{{ (item.eventAt || item.updatedAt) | date:'dd/MM/yyyy' }} · revisão {{ item.revision }}</small>
        </button>
        <div class="pagination" *ngIf="(page()?.total || 0) > 20"><button [disabled]="currentPage === 1" (click)="changePage(-1)">Anterior</button><span>Página {{ currentPage }}</span><button [disabled]="currentPage * 20 >= (page()?.total || 0)" (click)="changePage(1)">Próxima</button></div>
      </section>
      <section class="editor-panel narrative-detail" *ngIf="selected() as item">
        <div class="panel-heading"><span>{{ item.kind.toUpperCase() }} / {{ item.category.toUpperCase() }}</span><h2>{{ item.title }}</h2></div>
        <div class="narrative-meta"><span>{{ visibilityLabel(item.visibility) }}</span><span *ngIf="item.eventAt">{{ item.eventAt | date:'dd/MM/yyyy, HH:mm' }}</span></div>
        <div class="narrative-body">{{ item.body || 'Este registro ainda não tem conteúdo.' }}</div>
        <div class="form-actions"><button *ngIf="canEdit(item)" type="button" class="text-link" (click)="edit(item)">Editar</button>
          <button *ngIf="isMaster() && item.visibility !== 'campaign'" type="button" class="button button-small" [disabled]="busy()" (click)="reveal(item)">Revelar à campanha</button></div>
      </section>
      <form *ngIf="editing()" class="editor-panel narrative-detail" (ngSubmit)="save()" #form="ngForm">
        <div class="panel-heading"><span>{{ editingId ? 'EDITAR REGISTRO' : 'NOVO REGISTRO' }}</span><h2>{{ heading() }}</h2></div>
        <label>Título<input name="title" [(ngModel)]="draft.title" required minlength="2" maxlength="160" placeholder="Título do registro"></label>
        <label>Categoria<input name="category" [(ngModel)]="draft.category" required maxlength="40" pattern="[a-zA-Z0-9_-]+" placeholder="general"></label>
        <label>Data do acontecimento<input name="eventAt" type="datetime-local" [(ngModel)]="dateInput"></label>
        <label>Visibilidade<select name="visibility" [(ngModel)]="draft.visibility"><option value="private">Privado</option><option *ngIf="isMaster()" value="gm">Apenas mestres</option><option value="campaign">Toda a campanha</option></select></label>
        <label>Conteúdo<textarea name="body" [(ngModel)]="draft.body" rows="14" maxlength="40000" placeholder="Escreva a história, a pista ou o resumo aqui..."></textarea></label>
        <p class="form-error" *ngIf="error()" role="alert">{{ error() }}</p>
        <div class="form-actions"><button type="button" class="text-link" (click)="cancel()">Cancelar</button><button *ngIf="editingId" type="button" class="text-link danger" [disabled]="busy()" (click)="remove()">Excluir</button><button class="button" [disabled]="form.invalid || busy()">{{ busy() ? 'Salvando...' : 'Salvar registro' }}</button></div>
      </form>
      <div *ngIf="!selected() && !editing()" class="narrative-placeholder"><span class="overline">PÁGINAS DO ARQUIVO</span><p>Selecione um registro para ler ou crie uma nova página.</p></div>
    </div>
  </main>` })
export class NarrativePage implements OnInit, OnDestroy {
  private api = inject(ApiService); private route = inject(ActivatedRoute); private subscription?: Subscription; private searchTimer?: ReturnType<typeof setTimeout>;
  campaignId = ''; kind: NarrativeKind = 'sessions'; campaign = signal<Campaign | null>(null); page = signal<Page<NarrativeEntry> | null>(null);
  selected = signal<NarrativeEntry | null>(null); editing = signal(false); loading = signal(true); busy = signal(false); error = signal('');
  editingId: string | null = null; draft = emptyInput(); dateInput = ''; search = ''; currentPage = 1;
  ngOnInit() {
    this.subscription = this.route.paramMap.subscribe(params => {
      this.campaignId = params.get('id') || '';
      const section = params.get('section') || this.route.snapshot.data['section'];
      this.kind = section === 'documents' || section === 'wiki' ? section : 'sessions';
      this.selected.set(null); this.editing.set(false); this.currentPage = 1; this.search = '';
      this.api.campaign(this.campaignId).subscribe({ next: c => this.campaign.set(c), error: e => this.error.set(apiError(e)) });
      this.load();
      const entryId = this.route.snapshot.queryParamMap.get('entry');
      if (entryId) this.api.entry(entryId).subscribe({ next: entry => { if (entry.campaignId === this.campaignId && entry.kind === this.kind) this.select(entry); }, error: e => this.error.set(apiError(e)) });
    });
  }
  ngOnDestroy() { this.subscription?.unsubscribe(); clearTimeout(this.searchTimer); }
  heading() { return this.kind === 'sessions' ? 'Sessões' : this.kind === 'documents' ? 'Documentos e pistas' : 'Enciclopédia'; }
  subtitle() { return this.kind === 'sessions' ? 'Capítulos, resumos e acontecimentos.' : this.kind === 'documents' ? 'Cartas, pistas e arquivos revelados.' : 'Pessoas, lugares e organizações do mundo.'; }
  isMaster() { return ['owner', 'gm', 'co-gm'].includes(this.campaign()?.role || ''); }
  canCreate() { return !!this.campaign() && this.campaign()?.role !== 'spectator' && (this.kind !== 'sessions' || this.isMaster()); }
  canEdit(entry: NarrativeEntry) { return this.isMaster() || entry.authorId === this.api.user()?.id && this.kind !== 'sessions' && this.campaign()?.role !== 'spectator'; }
  visibilityLabel(value: string) { return value === 'campaign' ? 'Campanha' : value === 'gm' ? 'Mestres' : 'Privado'; }
  load() { this.loading.set(true); this.error.set(''); this.api.entries(this.campaignId, this.kind, this.currentPage, this.search).pipe(finalize(() => this.loading.set(false))).subscribe({ next: p => this.page.set(p), error: e => this.error.set(apiError(e)) }); }
  onSearch() { clearTimeout(this.searchTimer); this.searchTimer = setTimeout(() => { this.currentPage = 1; this.selected.set(null); this.load(); }, 250); }
  changePage(step: number) { this.currentPage += step; this.selected.set(null); this.load(); }
  select(item: NarrativeEntry) { this.selected.set(item); this.editing.set(false); this.error.set(''); }
  newEntry() { this.editingId = null; this.draft = emptyInput(); this.dateInput = ''; this.selected.set(null); this.editing.set(true); this.error.set(''); }
  edit(item: NarrativeEntry) { this.editingId = item.id; this.draft = { title: item.title, body: item.body, category: item.category, visibility: item.visibility, eventAt: item.eventAt, revision: item.revision }; this.dateInput = item.eventAt?.slice(0, 16) || ''; this.selected.set(null); this.editing.set(true); this.error.set(''); }
  cancel() { this.editing.set(false); this.error.set(''); }
  save() {
    this.error.set(''); this.busy.set(true);
    this.draft.eventAt = this.dateInput ? new Date(this.dateInput).toISOString() : null;
    const request = this.editingId ? this.api.updateEntry(this.editingId, this.draft) : this.api.createEntry(this.campaignId, this.kind, this.draft);
    request.pipe(finalize(() => this.busy.set(false))).subscribe({ next: entry => { this.editing.set(false); this.selected.set(entry); this.load(); }, error: e => this.error.set(apiError(e)) });
  }
  remove() {
    if (!this.editingId || !confirm('Excluir este registro permanentemente?')) return;
    this.busy.set(true); this.api.deleteEntry(this.editingId).pipe(finalize(() => this.busy.set(false))).subscribe({ next: () => { this.editing.set(false); this.selected.set(null); this.load(); }, error: e => this.error.set(apiError(e)) });
  }
  reveal(item: NarrativeEntry) { this.busy.set(true); this.api.revealEntry(item.id).pipe(finalize(() => this.busy.set(false))).subscribe({ next: entry => { this.selected.set(entry); this.load(); }, error: e => this.error.set(apiError(e)) }); }
}
