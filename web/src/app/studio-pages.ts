import { CommonModule, DOCUMENT } from '@angular/common';
import { Component, forwardRef, Input, OnDestroy, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize, Subscription, timer } from 'rxjs';
import { ApiService, apiError, Character, CharacterInput } from './api';

const emptyCharacter = (): CharacterInput => ({ name: '', portraitUrl: null, backgroundUrl: null, health: 10, maxHealth: 10, sanity: 10, maxSanity: 10, mana: 10, maxMana: 10 });

@Component({ standalone: true, imports: [CommonModule, FormsModule, RouterLink], template: `
  <main class="page narrow"><a [routerLink]="['/campaigns', campaignId]" class="back-link">← Voltar à campanha</a>
    <div class="page-heading"><div><span class="overline">ELENCO</span><h1>Personagens</h1><p>Retratos e recursos da sua mesa.</p></div></div>
    <div *ngIf="error()" class="notice error" role="alert">{{ error() }}</div>
    <div *ngIf="loading()" class="loading">Carregando personagens...</div>
    <div *ngIf="!loading()" class="character-list"><a *ngFor="let item of characters()" [routerLink]="['/characters', item.id]" class="character-list-item">
      <span class="character-thumb"><img *ngIf="item.portraitUrl" [src]="item.portraitUrl" alt=""></span><span><strong>{{ item.name }}</strong><small>Vida {{ item.health }}/{{ item.maxHealth }} · Sanidade {{ item.sanity }}/{{ item.maxSanity }} · Mana {{ item.mana }}/{{ item.maxMana }}</small></span><span aria-hidden="true">↗</span>
    </a><p *ngIf="characters().length === 0" class="muted">Ainda não há personagens nesta campanha.</p></div>
    <form *ngIf="role() && role() !== 'spectator'" class="editor-panel character-create" (ngSubmit)="create()" #form="ngForm"><div class="panel-heading"><span>NOVO PERSONAGEM</span><h2>Adicionar ao elenco</h2></div>
      <label>Nome<input name="name" [(ngModel)]="newName" required minlength="2" maxlength="120" placeholder="Nome do personagem"></label>
      <button class="button" type="submit" [disabled]="form.invalid || saving()">{{ saving() ? 'Criando...' : 'Criar personagem' }}</button>
    </form>
  </main>` })
export class CharactersPage {
  private api = inject(ApiService); private route = inject(ActivatedRoute);
  campaignId = this.route.snapshot.paramMap.get('id')!;
  characters = signal<Character[]>([]); role = signal<string | null>(null); loading = signal(true); saving = signal(false); error = signal(''); newName = '';
  constructor() { this.load(); }
  load() { this.loading.set(true); this.api.campaign(this.campaignId).subscribe({ next: campaign => this.role.set(campaign.role), error: error => this.error.set(apiError(error)) }); this.api.characters(this.campaignId).pipe(finalize(() => this.loading.set(false))).subscribe({ next: items => this.characters.set(items), error: error => this.error.set(apiError(error)) }); }
  create() { this.saving.set(true); this.error.set(''); this.api.createCharacter(this.campaignId, { ...emptyCharacter(), name: this.newName }).pipe(finalize(() => this.saving.set(false))).subscribe({ next: item => { this.characters.update(items => [...items, item].sort((a, b) => a.name.localeCompare(b.name))); this.newName = ''; }, error: error => this.error.set(apiError(error)) }); }
}

@Component({ standalone: true, imports: [CommonModule, FormsModule, RouterLink, forwardRef(() => OverlayCardComponent)], template: `
  <main class="page narrow" *ngIf="character() as item"><a [routerLink]="['/campaigns', item.campaignId, 'characters']" class="back-link">← Personagens</a>
    <div class="page-heading"><div><span class="overline">FICHA / CENA</span><h1>{{ item.name }}</h1><p>Informações exibidas na fonte do OBS.</p></div></div>
    <div *ngIf="error()" class="notice error" role="alert">{{ error() }}</div>
    <div class="character-editor"><form *ngIf="item.canEdit" class="editor-panel" (ngSubmit)="save()" #form="ngForm">
      <div class="panel-heading"><span>IDENTIDADE VISUAL</span><h2>Portrait e recursos</h2></div>
      <label>Nome<input name="name" [(ngModel)]="draft.name" required minlength="2" maxlength="120"></label>
      <label>URL HTTPS do portrait<input name="portraitUrl" [(ngModel)]="draft.portraitUrl" type="url" placeholder="https://..."></label>
      <label>URL HTTPS do fundo<input name="backgroundUrl" [(ngModel)]="draft.backgroundUrl" type="url" placeholder="https://..."></label>
      <div class="resource-fields"><div *ngFor="let resource of resources"><label>{{ resource.label }}<input type="number" [name]="resource.key" [(ngModel)]="draft[resource.key]" min="0" max="9999" required></label><label>Máximo<input type="number" [name]="resource.max" [(ngModel)]="draft[resource.max]" min="1" max="9999" required></label></div></div>
      <button class="button" [disabled]="form.invalid || saving()">{{ saving() ? 'Salvando...' : 'Salvar ficha' }}</button><p *ngIf="saved()" class="form-success" role="status">Ficha salva.</p>
    </form><div class="character-preview"><app-overlay-card [character]="draft" /><div *ngIf="item.ownerId === api.user()?.id" class="obs-actions"><h2>Fonte do OBS</h2><p>Gere uma URL de Browser Source. A chave anterior deixa de funcionar a cada nova geração.</p><button class="button button-small" type="button" (click)="createOverlayKey()" [disabled]="saving()">Gerar nova URL</button><p *ngIf="overlayUrl()" class="obs-url"><code>{{ overlayUrl() }}</code><button type="button" (click)="copyUrl()">Copiar</button></p><small *ngIf="overlayUrl()">Guarde esta URL agora; ela não pode ser recuperada depois.</small><h3>Câmera e ficha juntas</h3><p>Adicione esta cena como outra Browser Source. Ela exige entrar na sua conta pelo botão Interagir do OBS e mostra a câmera publicada por este personagem.</p><p class="obs-url"><code>{{ cameraUrl(item) }}</code><button type="button" (click)="copyCameraUrl(item)">Copiar</button></p></div></div></div>
  </main><main class="page" *ngIf="loading()"><div class="loading">Abrindo ficha...</div></main>` })
export class CharacterEditorPage {
  api = inject(ApiService); private route = inject(ActivatedRoute); private document = inject(DOCUMENT);
  character = signal<Character | null>(null); draft: CharacterInput = emptyCharacter(); loading = signal(true); saving = signal(false); saved = signal(false); error = signal(''); overlayUrl = signal('');
  resources: { label: string; key: 'health' | 'sanity' | 'mana'; max: 'maxHealth' | 'maxSanity' | 'maxMana' }[] = [
    { label: 'Vida', key: 'health', max: 'maxHealth' }, { label: 'Sanidade', key: 'sanity', max: 'maxSanity' }, { label: 'Mana', key: 'mana', max: 'maxMana' }
  ];
  constructor() { this.api.character(this.route.snapshot.paramMap.get('id')!).pipe(finalize(() => this.loading.set(false))).subscribe({ next: item => { this.character.set(item); this.draft = { ...item }; }, error: error => this.error.set(apiError(error)) }); }
  save() { const current = this.character(); if (!current) return; this.error.set(''); this.saved.set(false); this.saving.set(true); this.api.updateCharacter(current.id, this.draft).pipe(finalize(() => this.saving.set(false))).subscribe({ next: item => { this.character.set(item); this.saved.set(true); }, error: error => this.error.set(apiError(error)) }); }
  createOverlayKey() { const current = this.character(); if (!current) return; this.error.set(''); this.saving.set(true); this.api.rotateOverlayKey(current.id).pipe(finalize(() => this.saving.set(false))).subscribe({ next: result => this.overlayUrl.set(`${this.document.location?.origin}/obs/${result.key}`), error: error => this.error.set(apiError(error)) }); }
  copyUrl() { void this.document.defaultView?.navigator.clipboard.writeText(this.overlayUrl()); }
  cameraUrl(item: Character) { return `${this.document.location?.origin}/obs-camera/${item.campaignId}/${item.id}`; }
  copyCameraUrl(item: Character) { void this.document.defaultView?.navigator.clipboard.writeText(this.cameraUrl(item)); }
}

@Component({ selector: 'app-overlay-card', standalone: true, imports: [CommonModule], template: `
  <div class="obs-card" [style.background-image]="character.backgroundUrl ? 'url(' + character.backgroundUrl + ')' : null">
    <div class="obs-portrait"><img *ngIf="character.portraitUrl" [src]="character.portraitUrl" alt=""><span *ngIf="!character.portraitUrl">{{ character.name.slice(0, 1) || 'C' }}</span></div>
    <div class="obs-info"><h2>{{ character.name || 'Personagem' }}</h2><div class="obs-resources">
      <div><span>VIDA</span><b>{{ character.health }} / {{ character.maxHealth }}</b><i [style.width.%]="percent(character.health, character.maxHealth)"></i></div>
      <div><span>SANIDADE</span><b>{{ character.sanity }} / {{ character.maxSanity }}</b><i [style.width.%]="percent(character.sanity, character.maxSanity)"></i></div>
      <div><span>MANA</span><b>{{ character.mana }} / {{ character.maxMana }}</b><i [style.width.%]="percent(character.mana, character.maxMana)"></i></div>
    </div></div>
  </div>` })
export class OverlayCardComponent { @Input() character: CharacterInput = emptyCharacter(); percent(value: number, max: number) { return Math.max(0, Math.min(100, max ? value / max * 100 : 0)); } }

@Component({ standalone: true, imports: [CommonModule, OverlayCardComponent], template: `<div class="obs-page"><app-overlay-card *ngIf="character() as item" [character]="item" /><p *ngIf="error()" role="alert">Fonte indisponível. Gere uma nova URL na ficha.</p></div>` })
export class ObsOverlayPage implements OnDestroy {
  private api = inject(ApiService); private route = inject(ActivatedRoute);
  character = signal<Character | null>(null); error = signal(false);
  private polling: Subscription;
  constructor() { const key = this.route.snapshot.paramMap.get('key')!; this.polling = timer(0, 3000).subscribe(() => this.api.overlay(key).subscribe({ next: item => { this.character.set(item); this.error.set(false); }, error: () => { this.character.set(null); this.error.set(true); } })); }
  ngOnDestroy() { this.polling.unsubscribe(); }
}

