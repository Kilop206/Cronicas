import { CommonModule } from '@angular/common';
import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription, finalize } from 'rxjs';
import { ApiService, Page, World, WorldArticle, WorldArticleInput, WorldDetail, apiError } from './api';

@Component({ standalone: true, imports: [CommonModule, FormsModule, RouterLink], template: `
  <main class="page"><div class="page-heading"><div><span class="overline">ATLAS PESSOAL</span><h1>Universos</h1><p>Mundos que abrigam suas campanhas e histórias.</p></div></div>
    <div *ngIf="error()" class="notice error" role="alert">{{ error() }}</div>
    <div class="system-grid"><a *ngFor="let world of worlds()" [routerLink]="['/worlds', world.id]" class="system-card"><span class="overline">{{ world.ownerId === api.user()?.id ? 'SEU UNIVERSO' : 'COMPARTILHADO' }}</span><h2>{{ world.name }}</h2><p>{{ world.description || 'Um mundo aguardando suas histórias.' }}</p><small>Abrir universo →</small></a></div>
    <form class="editor-panel world-create" (ngSubmit)="create()" #form="ngForm"><div class="panel-heading"><span>NOVO UNIVERSO</span><h2>Comece um mundo</h2></div>
      <label>Nome<input name="name" [(ngModel)]="name" required minlength="3" maxlength="120" placeholder="Ex.: Arquipélago das Marés"></label>
      <label>Descrição<textarea name="description" [(ngModel)]="description" maxlength="4000" rows="3"></textarea></label>
      <button class="button" [disabled]="form.invalid || busy()">{{ busy() ? 'Criando...' : 'Criar universo' }}</button></form>
  </main>` })
export class WorldsPage implements OnInit {
  api = inject(ApiService); private router = inject(Router); worlds = signal<World[]>([]); error = signal(''); busy = signal(false); name = ''; description = '';
  ngOnInit() { this.api.worlds().subscribe({ next: items => this.worlds.set(items), error: e => this.error.set(apiError(e)) }); }
  create() { this.busy.set(true); this.error.set(''); this.api.createWorld(this.name, this.description).pipe(finalize(() => this.busy.set(false))).subscribe({ next: world => void this.router.navigate(['/worlds', world.id]), error: e => this.error.set(apiError(e)) }); }
}

const emptyArticle = (): WorldArticleInput => ({ title: '', body: '', category: 'general', visibility: 'private', revision: 1 });

@Component({ standalone: true, imports: [CommonModule, FormsModule, RouterLink], template: `
  <main class="page" *ngIf="world() as item"><a routerLink="/worlds" class="back-link">← Todos os universos</a>
    <div class="page-heading"><div><span class="overline">ATLAS / {{ item.id.slice(0, 8).toUpperCase() }}</span><h1>{{ item.name }}</h1><p>{{ item.description || 'Este universo ainda não tem descrição.' }}</p></div></div>
    <div *ngIf="error()" class="notice error" role="alert">{{ error() }}</div>
    <div class="world-campaigns"><span class="overline">CAMPANHAS RELACIONADAS</span><div *ngIf="!item.campaigns.length" class="room-note">Associe uma campanha no workspace dela para compartilhar este universo.</div>
      <a *ngFor="let campaign of item.campaigns" [routerLink]="['/campaigns', campaign.campaignId]" class="campaign-row"><span class="row-main"><strong>{{ campaign.name }}</strong><small>{{ campaign.role }}</small></span><span>↗</span></a></div>
    <div class="section-heading"><div><span class="overline">ENCICLOPÉDIA</span><h2>Artigos do universo</h2></div><button *ngIf="canWrite()" type="button" class="button button-small" (click)="newArticle()">+ Novo artigo</button></div>
    <div class="narrative-layout"><section class="narrative-list"><label class="search-field"><span aria-hidden="true">⌕</span><input type="search" [(ngModel)]="search" (ngModelChange)="searchChanged()" placeholder="Buscar artigos" aria-label="Buscar artigos"></label>
      <div *ngIf="!articles()?.items?.length" class="empty-state small"><h3>Nenhum artigo encontrado</h3><p>Crie um lugar, pessoa ou acontecimento para iniciar a enciclopédia.</p></div>
      <button *ngFor="let article of articles()?.items" type="button" class="narrative-row" (click)="select(article)"><span class="overline">{{ article.category }} · {{ article.visibility }}</span><strong>{{ article.title }}</strong><small>Atualizado {{ article.updatedAt | date:'dd/MM/yyyy' }}</small></button>
      <div class="pagination" *ngIf="(articles()?.total || 0) > 20"><button [disabled]="pageNumber === 1" (click)="changePage(-1)">Anterior</button><span>Página {{ pageNumber }}</span><button [disabled]="pageNumber * 20 >= (articles()?.total || 0)" (click)="changePage(1)">Próxima</button></div></section>
      <section *ngIf="selected() as article" class="editor-panel narrative-detail"><div class="panel-heading"><span>{{ article.category.toUpperCase() }}</span><h2>{{ article.title }}</h2></div><div class="narrative-meta">{{ article.visibility === 'campaign' ? 'Compartilhado' : article.visibility === 'gm' ? 'Mestres' : 'Privado' }}</div><div class="narrative-body">{{ article.body }}</div><button *ngIf="canEdit(article)" type="button" class="text-link" (click)="edit(article)">Editar artigo</button></section>
      <form *ngIf="editing()" class="editor-panel narrative-detail" (ngSubmit)="saveArticle()" #form="ngForm"><div class="panel-heading"><span>ENCICLOPÉDIA</span><h2>{{ editingId ? 'Editar artigo' : 'Novo artigo' }}</h2></div>
        <label>Título<input name="title" [(ngModel)]="draft.title" required minlength="2" maxlength="160"></label>
        <label>Categoria<input name="category" [(ngModel)]="draft.category" required maxlength="40" pattern="[a-z][a-z0-9_-]*" placeholder="location"></label>
        <label>Visibilidade<select name="visibility" [(ngModel)]="draft.visibility"><option value="private">Privado</option><option *ngIf="canMaster()" value="gm">Mestres</option><option value="campaign">Campanhas relacionadas</option></select></label>
        <label>Conteúdo<textarea name="body" [(ngModel)]="draft.body" rows="13" maxlength="40000"></textarea></label>
        <div class="form-actions"><button type="button" class="text-link" (click)="editing.set(false)">Cancelar</button><button *ngIf="editingId" type="button" class="text-link danger" [disabled]="busy()" (click)="deleteArticle()">Excluir</button><button class="button" [disabled]="form.invalid || busy()">{{ busy() ? 'Salvando...' : 'Salvar artigo' }}</button></div></form>
      <div *ngIf="!selected() && !editing()" class="narrative-placeholder"><p>Selecione um artigo para ler.</p></div></div>
    <form *ngIf="item.ownerId === api.user()?.id" class="editor-panel world-settings" (ngSubmit)="saveWorld()" #worldForm="ngForm"><div class="panel-heading"><span>CONFIGURAÇÕES DO UNIVERSO</span><h2>Identidade</h2></div>
      <label>Nome<input name="worldName" [(ngModel)]="name" required minlength="3" maxlength="120"></label><label>Descrição<textarea name="worldDescription" [(ngModel)]="description" maxlength="4000" rows="3"></textarea></label>
      <div class="form-actions"><button type="button" class="text-link danger" (click)="deleteWorld()">Excluir universo</button><button class="button" [disabled]="worldForm.invalid || busy()">Salvar universo</button></div></form>
  </main><main class="page" *ngIf="!world()"><div class="loading">Abrindo universo...</div><div *ngIf="error()" class="notice error">{{ error() }}</div></main>` })
export class WorldDetailPage implements OnInit, OnDestroy {
  api = inject(ApiService); private route = inject(ActivatedRoute); private router = inject(Router); private searchTimer?: ReturnType<typeof setTimeout>; private subscription?: Subscription;
  id = this.route.snapshot.paramMap.get('id')!; world = signal<WorldDetail | null>(null); articles = signal<Page<WorldArticle> | null>(null); selected = signal<WorldArticle | null>(null);
  editing = signal(false); editingId: string | null = null; draft = emptyArticle(); error = signal(''); busy = signal(false); name = ''; description = ''; search = ''; pageNumber = 1;
  ngOnInit() { this.subscription = this.route.paramMap.subscribe(params => { this.id = params.get('id')!; this.loadWorld(); this.loadArticles(); }); }
  ngOnDestroy() { this.subscription?.unsubscribe(); clearTimeout(this.searchTimer); }
  loadWorld() { this.api.world(this.id).subscribe({ next: value => { this.world.set(value); this.name = value.name; this.description = value.description; }, error: e => this.error.set(apiError(e)) }); }
  loadArticles() { this.api.worldArticles(this.id, this.pageNumber, this.search).subscribe({ next: value => this.articles.set(value), error: e => this.error.set(apiError(e)) }); }
  canMaster() { const item = this.world(); return item?.ownerId === this.api.user()?.id || !!item?.campaigns.some(c => ['owner', 'gm', 'co-gm'].includes(c.role)); }
  canWrite() { const item = this.world(); return this.canMaster() || !!item?.campaigns.some(c => c.role !== 'spectator'); }
  canEdit(article: WorldArticle) { return this.canMaster() || this.canWrite() && article.authorId === this.api.user()?.id; }
  searchChanged() { clearTimeout(this.searchTimer); this.searchTimer = setTimeout(() => { this.pageNumber = 1; this.loadArticles(); }, 250); }
  changePage(step: number) { this.pageNumber += step; this.loadArticles(); }
  select(article: WorldArticle) { this.selected.set(article); this.editing.set(false); }
  newArticle() { this.selected.set(null); this.editingId = null; this.draft = emptyArticle(); this.editing.set(true); this.error.set(''); }
  edit(article: WorldArticle) { this.selected.set(null); this.editingId = article.id; this.draft = { title: article.title, body: article.body, category: article.category, visibility: article.visibility, revision: article.revision }; this.editing.set(true); this.error.set(''); }
  saveArticle() { this.busy.set(true); this.error.set(''); const request = this.editingId ? this.api.updateWorldArticle(this.editingId, this.draft) : this.api.createWorldArticle(this.id, this.draft); request.pipe(finalize(() => this.busy.set(false))).subscribe({ next: value => { this.editing.set(false); this.selected.set(value); this.loadArticles(); }, error: e => this.error.set(apiError(e)) }); }
  deleteArticle() { if (!this.editingId || !confirm('Excluir este artigo?')) return; this.busy.set(true); this.api.deleteWorldArticle(this.editingId).pipe(finalize(() => this.busy.set(false))).subscribe({ next: () => { this.editing.set(false); this.loadArticles(); }, error: e => this.error.set(apiError(e)) }); }
  saveWorld() { this.busy.set(true); this.error.set(''); this.api.updateWorld(this.id, this.name, this.description).pipe(finalize(() => this.busy.set(false))).subscribe({ next: () => this.loadWorld(), error: e => this.error.set(apiError(e)) }); }
  deleteWorld() { if (!confirm('Excluir este universo e seus artigos?')) return; this.busy.set(true); this.api.deleteWorld(this.id).pipe(finalize(() => this.busy.set(false))).subscribe({ next: () => void this.router.navigateByUrl('/worlds'), error: e => this.error.set(apiError(e)) }); }
}
