import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription, finalize } from 'rxjs';
import { ApiService, SearchResults, apiError } from './api';

@Component({ standalone: true, imports: [CommonModule, FormsModule, RouterLink], template: `
  <main class="page search-page"><div class="page-heading"><div><span class="overline">ARQUIVO INTEIRO</span><h1>Pesquisa</h1><p>Encontre campanhas, personagens, mundos e registros que você pode consultar.</p></div></div>
    <form class="search-main" (ngSubmit)="submit()"><label class="search-field"><span aria-hidden="true">⌕</span><input type="search" name="query" [(ngModel)]="query" minlength="2" maxlength="120" placeholder="O que você procura?" aria-label="Pesquisar no arquivo" autofocus></label><button class="button" [disabled]="query.trim().length < 2">Pesquisar</button></form>
    <div *ngIf="error()" class="notice error" role="alert">{{ error() }}</div><div *ngIf="loading()" class="loading">Buscando no arquivo...</div>
    <p *ngIf="!loading() && !results()" class="room-note">Digite ao menos dois caracteres para buscar.</p>
    <ng-container *ngIf="results() as found"><p class="search-count">{{ count(found) }} resultado(s) para “{{ searched }}” · até 10 por categoria</p>
      <div *ngIf="count(found) === 0" class="empty-state small"><h2>Nada encontrado</h2><p>Tente outro nome ou trecho do conteúdo.</p></div>
      <section *ngIf="found.campaigns.length" class="search-group"><h2>Campanhas</h2><a *ngFor="let item of found.campaigns" [routerLink]="['/campaigns', item.id]" class="search-result"><strong>{{ item.name }}</strong><small>{{ item.status }} · Atualizada {{ item.updatedAt | date:'dd/MM/yyyy' }}</small><span aria-hidden="true">↗</span></a></section>
      <section *ngIf="found.characters.length" class="search-group"><h2>Personagens</h2><a *ngFor="let item of found.characters" [routerLink]="['/characters', item.id]" class="search-result"><strong>{{ item.name }}</strong><small>{{ item.campaignName }}</small><span aria-hidden="true">↗</span></a></section>
      <section *ngIf="found.npcs.length" class="search-group"><h2>NPCs</h2><a *ngFor="let item of found.npcs" [routerLink]="['/campaigns', item.campaignId, 'npcs']" [queryParams]="{ npc: item.id }" class="search-result"><strong>{{ item.name }}</strong><small>{{ item.campaignName }}</small><span aria-hidden="true">↗</span></a></section>
      <section *ngIf="found.worlds.length" class="search-group"><h2>Universos</h2><a *ngFor="let item of found.worlds" [routerLink]="['/worlds', item.id]" class="search-result"><strong>{{ item.name }}</strong><small>Universo</small><span aria-hidden="true">↗</span></a></section>
      <section *ngIf="found.articles.length" class="search-group"><h2>Enciclopédia</h2><a *ngFor="let item of found.articles" [routerLink]="['/worlds', item.worldId]" [queryParams]="{ article: item.id }" class="search-result"><strong>{{ item.title }}</strong><small>{{ item.worldName }} · {{ item.category }}</small><span aria-hidden="true">↗</span></a></section>
      <section *ngIf="found.entries.length" class="search-group"><h2>Registros de campanha</h2><a *ngFor="let item of found.entries" [routerLink]="['/campaigns', item.campaignId, item.kind]" [queryParams]="{ entry: item.id }" class="search-result"><strong>{{ item.title }}</strong><small>{{ item.campaignName }} · {{ label(item.kind) }}</small><span aria-hidden="true">↗</span></a></section>
    </ng-container>
  </main>` })
export class SearchPage implements OnInit, OnDestroy {
  private api = inject(ApiService); private route = inject(ActivatedRoute); private router = inject(Router); private subscription?: Subscription;
  query = ''; searched = ''; results = signal<SearchResults | null>(null); loading = signal(false); error = signal('');
  ngOnInit() { this.subscription = this.route.queryParamMap.subscribe(params => { this.query = params.get('q') || ''; if (this.query.trim().length >= 2) this.load(); else this.results.set(null); }); }
  ngOnDestroy() { this.subscription?.unsubscribe(); }
  submit() { const q = this.query.trim(); if (q.length < 2) return; void this.router.navigate(['/search'], { queryParams: { q } }); if (q === this.searched) this.load(); }
  load() { this.loading.set(true); this.error.set(''); this.searched = this.query.trim(); this.api.search(this.searched).pipe(finalize(() => this.loading.set(false))).subscribe({ next: result => this.results.set(result), error: e => this.error.set(apiError(e)) }); }
  count(results: SearchResults) { return results.campaigns.length + results.characters.length + results.npcs.length + results.worlds.length + results.articles.length + results.entries.length; }
  label(kind: string) { return kind === 'sessions' ? 'Sessão' : kind === 'documents' ? 'Documento' : 'Wiki'; }
}
