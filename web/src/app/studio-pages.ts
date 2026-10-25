import { CommonModule, DOCUMENT } from '@angular/common';
import { Component, forwardRef, Input, OnDestroy, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize, Subscription, timer } from 'rxjs';
import { ApiService, apiError, Character, CharacterInput, CharacterSecret, RpgSystem, SystemField } from './api';
import { CharacterHistoryPanelComponent } from './character-history.component';
import { CharacterInventoryComponent } from './character-inventory.component';

const emptyCharacter = (): CharacterInput => ({ name: '', portraitUrl: null, backgroundUrl: null, health: 10, maxHealth: 10, sanity: 10, maxSanity: 10, mana: 10, maxMana: 10, fields: {}, version: 1 });

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

@Component({ standalone: true, imports: [CommonModule, FormsModule, RouterLink, forwardRef(() => OverlayCardComponent), CharacterHistoryPanelComponent, CharacterInventoryComponent], template: `
  <main class="page narrow" *ngIf="character() as item"><a [routerLink]="['/campaigns', item.campaignId, 'characters']" class="back-link">← Personagens</a>
    <div class="page-heading"><div><span class="overline">FICHA / CENA</span><h1>{{ item.name }}</h1><p>Ficha da campanha e composição para OBS.</p></div></div>
    <button type="button" class="text-link character-favorite" [attr.aria-pressed]="favorite()" [disabled]="favoriteBusy()" (click)="toggleFavorite()">{{ favorite() ? 'Remover dos favoritos' : 'Adicionar aos favoritos' }}</button>
    <div *ngIf="error()" class="notice error" role="alert">{{ error() }}</div>
    <div class="character-editor"><form *ngIf="item.canEdit" class="editor-panel" (ngSubmit)="save()" #form="ngForm">
      <div class="panel-heading"><span>IDENTIDADE VISUAL</span><h2>Portrait e recursos</h2></div>
      <label>Nome<input name="name" [(ngModel)]="draft.name" (ngModelChange)="scheduleSave()" required minlength="2" maxlength="120"></label>
      <label>URL HTTPS do portrait<input name="portraitUrl" [(ngModel)]="draft.portraitUrl" (ngModelChange)="scheduleSave()" type="url" placeholder="https://..."></label>
      <label>URL HTTPS do fundo<input name="backgroundUrl" [(ngModel)]="draft.backgroundUrl" (ngModelChange)="scheduleSave()" type="url" placeholder="https://..."></label>
      <div class="resource-fields"><div *ngFor="let resource of resources"><label>{{ resource.label }}<input type="number" [name]="resource.key" [(ngModel)]="draft[resource.key]" (ngModelChange)="scheduleSave()" min="0" max="9999" required></label><label>Máximo<input type="number" [name]="resource.max" [(ngModel)]="draft[resource.max]" (ngModelChange)="scheduleSave()" min="1" max="9999" required></label></div></div>
      <div *ngIf="system() as sheet" class="dynamic-fields"><div class="panel-heading"><span>SISTEMA / {{ sheet.name }}</span><h2>Campos da ficha</h2></div>
        <div *ngFor="let field of sheet.fields" class="dynamic-field">
          <label *ngIf="field.type === 'text' || field.type === 'number'">{{ field.label }} <small>{{ field.section }}</small><input [name]="field.key" [type]="field.type === 'number' ? 'number' : 'text'" [min]="field.min" [max]="field.max" [ngModel]="fieldValue(field.key)" (ngModelChange)="setField(field, $event)"></label>
          <label *ngIf="field.type === 'longtext'">{{ field.label }} <small>{{ field.section }}</small><textarea [name]="field.key" [ngModel]="fieldValue(field.key)" (ngModelChange)="setField(field, $event)" rows="5" maxlength="10000"></textarea></label>
          <label *ngIf="field.type === 'select'">{{ field.label }} <small>{{ field.section }}</small><select [name]="field.key" [ngModel]="fieldValue(field.key)" (ngModelChange)="setField(field, $event)"><option value="">Selecione</option><option *ngFor="let option of field.options" [value]="option">{{ option }}</option></select></label>
          <label *ngIf="field.type === 'boolean'" class="checkbox-field"><input type="checkbox" [name]="field.key" [ngModel]="fieldValue(field.key)" (ngModelChange)="setField(field, $event)"> {{ field.label }}</label>
        </div>
      </div>
      <button class="button" [disabled]="form.invalid || saving()">{{ saving() ? 'Salvando...' : 'Salvar ficha' }}</button><p *ngIf="saved()" class="form-success" role="status">Ficha salva automaticamente.</p>
    </form><div *ngIf="!item.canEdit" class="editor-panel"><div class="panel-heading"><span>FICHA</span><h2>{{ system()?.name || 'Personagem' }}</h2></div><div *ngFor="let field of system()?.fields" class="sheet-read-row"><span>{{ field.label }}</span><strong>{{ fieldValue(field.key) || '—' }}</strong></div></div><div class="character-preview"><app-overlay-card [character]="draft" /><div class="character-actions"><button type="button" class="button button-small print-action" (click)="printSheet()">Imprimir / salvar PDF</button><button *ngIf="item.canEdit" type="button" class="button button-small secondary" [disabled]="!canDuplicate()" (click)="duplicate()">Duplicar ficha</button></div><div *ngIf="item.ownerId === api.user()?.id" class="obs-actions"><h2>Fonte do OBS</h2><p>Gere uma URL de Browser Source. A chave anterior deixa de funcionar a cada nova geração.</p><button class="button button-small" type="button" (click)="createOverlayKey()" [disabled]="saving()">Gerar nova URL</button><p *ngIf="overlayUrl()" class="obs-url"><code>{{ overlayUrl() }}</code><button type="button" (click)="copyUrl()">Copiar</button></p><small *ngIf="overlayUrl()">Guarde esta URL agora; ela não pode ser recuperada depois.</small><h3>Câmera e ficha juntas</h3><p>Adicione esta cena como outra Browser Source. Ela exige entrar na sua conta pelo botão Interagir do OBS e mostra a câmera publicada por este personagem.</p><p class="obs-url"><code>{{ cameraUrl(item) }}</code><button type="button" (click)="copyCameraUrl(item)">Copiar</button></p></div></div></div>
    <section class="print-sheet"><h2>{{ draft.name }}</h2><p>Vida {{ draft.health }}/{{ draft.maxHealth }} · Sanidade {{ draft.sanity }}/{{ draft.maxSanity }} · Mana {{ draft.mana }}/{{ draft.maxMana }}</p><div *ngFor="let field of system()?.fields" class="sheet-read-row"><span>{{ field.label }}</span><strong>{{ fieldValue(field.key) === '' ? '—' : fieldValue(field.key) }}</strong></div></section>
    <app-character-inventory [character]="item" />
    <app-character-history [character]="item" (restored)="onRestored($event)" />
    <section *ngIf="master()" class="editor-panel character-secret"><div class="panel-heading"><span>SOMENTE MESTRES</span><h2>Anotações secretas</h2></div><p class="room-note">Estas notas ficam em uma área separada da ficha pública. Jogadores não recebem o conteúdo pela API.</p><label>Segredos e planejamento<textarea [(ngModel)]="secretDraft" rows="7" maxlength="10000" placeholder="Informações ainda não reveladas..."></textarea></label><div class="form-actions"><button type="button" class="button" [disabled]="secretBusy() || !secret()" (click)="saveSecret()">{{ secretBusy() ? 'Salvando...' : 'Salvar notas secretas' }}</button><span *ngIf="secretSaved()" class="form-success" role="status">Notas salvas.</span></div></section>
  </main><main class="page" *ngIf="loading()"><div class="loading">Abrindo ficha...</div></main>` })
export class CharacterEditorPage implements OnDestroy {
  api = inject(ApiService); private route = inject(ActivatedRoute); private router = inject(Router); private document = inject(DOCUMENT);
  character = signal<Character | null>(null); system = signal<RpgSystem | null>(null); draft: CharacterInput = emptyCharacter(); loading = signal(true); saving = signal(false); saved = signal(false); error = signal(''); overlayUrl = signal('');
  master = signal(false); secret = signal<CharacterSecret | null>(null); secretDraft = ''; secretBusy = signal(false); secretSaved = signal(false);
  favorite = signal(false); favoriteBusy = signal(false);
  private saveTimer?: ReturnType<typeof setTimeout>; private editRevision = 0; private routeSubscription: Subscription;
  resources: { label: string; key: 'health' | 'sanity' | 'mana'; max: 'maxHealth' | 'maxSanity' | 'maxMana' }[] = [
    { label: 'Vida', key: 'health', max: 'maxHealth' }, { label: 'Sanidade', key: 'sanity', max: 'maxSanity' }, { label: 'Mana', key: 'mana', max: 'maxMana' }
  ];
  constructor() { this.routeSubscription = this.route.paramMap.subscribe(params => this.loadCharacter(params.get('id')!)); }
  private loadCharacter(id: string) { clearTimeout(this.saveTimer); this.loading.set(true); this.character.set(null); this.favorite.set(false); this.master.set(false); this.secret.set(null); this.secretDraft = ''; this.overlayUrl.set(''); this.error.set(''); this.api.character(id).pipe(finalize(() => this.loading.set(false))).subscribe({ next: item => { this.character.set(item); this.draft = { ...item, fields: { ...item.fields } }; this.api.characterFavorite(id).subscribe({ next: value => this.favorite.set(value.favorite), error: e => this.error.set(apiError(e)) }); this.api.campaign(item.campaignId).subscribe({ next: campaign => { const master = ['owner', 'gm', 'co-gm'].includes(campaign.role); this.master.set(master); if (master) this.api.characterSecret(id).subscribe({ next: secret => { this.secret.set(secret); this.secretDraft = secret.notes; }, error: e => this.error.set(apiError(e)) }); this.api.system(campaign.systemId || 'a9e29682-c194-4aae-9c0e-7bbb3f6a72f1').subscribe({ next: system => this.system.set(system), error: e => this.error.set(apiError(e)) }); }, error: e => this.error.set(apiError(e)) }); }, error: error => this.error.set(apiError(error)) }); }
  fieldValue(key: string) { return this.draft.fields[key] ?? ''; }
  setField(field: SystemField, value: string | number | boolean) { if (value === '' || value === null) delete this.draft.fields[field.key]; else this.draft.fields[field.key] = field.type === 'number' ? Number(value) : value; this.scheduleSave(); }
  scheduleSave() { this.editRevision++; this.saved.set(false); clearTimeout(this.saveTimer); this.saveTimer = setTimeout(() => { if (this.validDraft()) this.save(); }, 900); }
  private validDraft() { return this.draft.name.trim().length >= 2 && this.resources.every(r => this.draft[r.key] >= 0 && this.draft[r.key] <= this.draft[r.max] && this.draft[r.max] >= 1); }
  save() {
    const current = this.character(); if (!current || this.saving() || !this.validDraft()) return;
    clearTimeout(this.saveTimer); this.error.set(''); this.saved.set(false); this.saving.set(true);
    const revision = this.editRevision; const payload = { ...this.draft, fields: { ...this.draft.fields }, version: current.version };
    this.api.updateCharacter(current.id, payload).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: item => { this.character.set(item); this.draft.version = item.version; this.saved.set(revision === this.editRevision); if (revision !== this.editRevision) this.saveTimer = setTimeout(() => this.save(), 0); },
      error: error => this.error.set(apiError(error))
    });
  }
  onRestored(item: Character) { clearTimeout(this.saveTimer); this.editRevision++; this.character.set(item); this.draft = { ...item, fields: { ...item.fields } }; this.error.set(''); this.saved.set(true); }
  printSheet() { this.document.defaultView?.print(); }
  canDuplicate() { return !this.saving() && (this.editRevision === 0 || this.saved()); }
  duplicate() { const current = this.character(); if (!current) return; this.error.set(''); this.saving.set(true); this.api.duplicateCharacter(current.id).pipe(finalize(() => this.saving.set(false))).subscribe({ next: item => void this.router.navigate(['/characters', item.id]), error: e => this.error.set(apiError(e)) }); }
  saveSecret() { const current = this.character(), secret = this.secret(); if (!current || !secret) return; this.error.set(''); this.secretSaved.set(false); this.secretBusy.set(true); this.api.updateCharacterSecret(current.id, this.secretDraft, secret.revision).pipe(finalize(() => this.secretBusy.set(false))).subscribe({ next: result => { this.secret.set(result); this.secretSaved.set(true); }, error: e => this.error.set(apiError(e)) }); }
  toggleFavorite() { const current = this.character(); if (!current) return; this.error.set(''); this.favoriteBusy.set(true); const request = this.favorite() ? this.api.removeCharacterFavorite(current.id) : this.api.addCharacterFavorite(current.id); request.pipe(finalize(() => this.favoriteBusy.set(false))).subscribe({ next: () => this.favorite.update(value => !value), error: e => this.error.set(apiError(e)) }); }
  ngOnDestroy() { clearTimeout(this.saveTimer); this.routeSubscription.unsubscribe(); }
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

