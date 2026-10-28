import { CommonModule, DOCUMENT } from '@angular/common';
import { Component, forwardRef, Input, OnDestroy, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize, Subscription, timer } from 'rxjs';
import { ApiService, apiError, Character, CharacterInput, CharacterSecret, OverlayCharacter, OverlayIndicator, OverlayResource, RpgSystem, SheetFieldValue, SystemField } from './api';
import { CharacterHistoryPanelComponent } from './character-history.component';
import { CharacterInventoryComponent } from './character-inventory.component';
import { CharacterAbilitiesComponent } from './character-abilities.component';
import { SheetLayoutComponent } from './sheet-layout.component';
import { SheetRollComponent } from './sheet-roll.component';
import { SpecializedSheetComponent } from './specialized-sheet.component';
import { ParanormalSheetComponent } from './paranormal-sheet.component';
import { DndSheetComponent } from './dnd-sheet.component';
import { CocSheetComponent } from './coc-sheet.component';
import { T20SheetComponent } from './t20-sheet.component';
import { SheetCollectionFieldComponent } from './sheet-collection-field.component';
import { resolveSheetRenderer, sheetIndicators, sheetResources, sheetOverlayResources } from './sheet-renderer-registry';
import { calculateSheet } from './sheet-rules';

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

@Component({ selector: 'app-sheet-fields-view', standalone: true, imports: [CommonModule], template: `
  <section *ngFor="let section of sections()" class="sheet-view-section"><h3>{{ section }}</h3><div class="sheet-view-grid">
    <div *ngFor="let field of sectionFields(section)" class="sheet-view-field" [style.grid-column]="'span ' + (field.span || 6)">
      <span>{{ field.label }}</span><strong [class.longtext]="field.type === 'longtext'">{{ display(field) }}</strong>
    </div>
  </div></section>` })
export class SheetFieldsViewComponent {
  @Input() fields: SystemField[] = [];
  @Input() values: Record<string, SheetFieldValue> = {};
  sections() { return [...new Set(this.fields.map(field => field.section))]; }
  sectionFields(section: string) { return this.fields.filter(field => field.section === section); }
  display(field: SystemField) {
    const value = this.values[field.key];
    if (value === undefined || value === '') return '—';
    return field.type === 'boolean' ? value ? 'Sim' : 'Não' : String(value);
  }
}

@Component({ standalone: true, imports: [CommonModule, FormsModule, RouterLink, SheetFieldsViewComponent, SheetLayoutComponent, SpecializedSheetComponent, ParanormalSheetComponent, DndSheetComponent, CocSheetComponent, T20SheetComponent, SheetCollectionFieldComponent, SheetRollComponent, forwardRef(() => OverlayCardComponent), CharacterHistoryPanelComponent, CharacterInventoryComponent, CharacterAbilitiesComponent], template: `
  <main class="page" [class.narrow]="!renderer()" [class.immersive-sheet-page]="renderer()" *ngIf="!loading() && !systemLoading() && !systemFailed() && character() as item"><a [routerLink]="['/campaigns', item.campaignId, 'characters']" class="back-link">← Personagens</a>
    <div *ngIf="!renderer()" class="page-heading"><div><span class="overline">FICHA / CENA</span><h1>{{ item.name }}</h1><p>Ficha da campanha e composição para OBS.</p></div></div>
    <button type="button" class="text-link character-favorite" [attr.aria-pressed]="favorite()" [disabled]="favoriteBusy()" (click)="toggleFavorite()">{{ favorite() ? 'Remover dos favoritos' : 'Adicionar aos favoritos' }}</button>
    <details *ngIf="!renderer()" class="obs-actions"><summary>Dados estruturados</summary><p>Exporte ou importe os campos da ficha em JSON.</p><button type="button" class="button button-small secondary" (click)="exportCharacter()">Exportar JSON</button><label *ngIf="item.canEdit" class="button button-small secondary">Importar JSON<input type="file" accept="application/json,.json" (change)="importCharacter($event)" [disabled]="saving()" hidden></label></details>
    <div *ngIf="error()" class="notice error" role="alert">{{ error() }}</div>
    <div *ngIf="!system()?.layout" class="sheet-summary"><span class="character-thumb"><img *ngIf="draft.portraitUrl" [src]="draft.portraitUrl" alt=""></span><div><span class="overline">{{ systemLoading() ? 'CARREGANDO SISTEMA' : system()?.name || 'FICHA LIVRE' }}</span><h2>{{ draft.name }}</h2></div><div class="sheet-resource-summary"><span *ngFor="let resource of visibleResources()"><small>{{ resource.label }}</small><strong>{{ draft[resource.key] }} / {{ draft[maxKey(resource.key)] }}</strong></span></div></div>
    <nav class="sheet-tabs" aria-label="Seções da ficha"><button type="button" [class.active]="activeTab === 'sheet'" (click)="activeTab = 'sheet'">Ficha</button><ng-container *ngIf="!system()?.layout"><button type="button" [class.active]="activeTab === 'inventory'" (click)="activeTab = 'inventory'">Inventário</button><button type="button" [class.active]="activeTab === 'abilities'" (click)="activeTab = 'abilities'">Habilidades</button><button type="button" [class.active]="activeTab === 'history'" (click)="activeTab = 'history'">Histórico</button></ng-container><button *ngIf="master()" type="button" [class.active]="activeTab === 'secret'" (click)="activeTab = 'secret'">Mestre</button></nav>
    <div *ngIf="activeTab === 'sheet' && !system()?.layout" class="character-editor"><form *ngIf="item.canEdit" class="editor-panel" (ngSubmit)="save()" #form="ngForm">
      <div class="panel-heading"><span>IDENTIDADE VISUAL</span><h2>Portrait e recursos</h2></div>
      <label>Nome<input name="name" [(ngModel)]="draft.name" (ngModelChange)="scheduleSave()" required minlength="2" maxlength="120"></label>
      <label>URL HTTPS do portrait<input name="portraitUrl" [(ngModel)]="draft.portraitUrl" (ngModelChange)="scheduleSave()" type="url" placeholder="https://..."></label>
      <label>URL HTTPS do fundo<input name="backgroundUrl" [(ngModel)]="draft.backgroundUrl" (ngModelChange)="scheduleSave()" type="url" placeholder="https://..."></label>
      <div class="resource-fields"><div *ngFor="let resource of resources"><label>{{ resource.label }}<input type="number" [name]="resource.key" [(ngModel)]="draft[resource.key]" (ngModelChange)="scheduleSave()" min="0" max="9999" required></label><label>Máximo<input type="number" [name]="resource.max" [(ngModel)]="draft[resource.max]" (ngModelChange)="scheduleSave()" min="1" max="9999" required></label></div></div>
      <div *ngIf="!systemLoading() && system()?.fields?.length" class="dynamic-fields"><div class="panel-heading"><span>SISTEMA / {{ system()?.name }}</span><h2>Campos da ficha</h2></div>
        <section *ngFor="let section of sheetSections()" class="sheet-field-section"><h3>{{ section }}</h3><div class="sheet-field-grid"><div *ngFor="let field of sectionFields(section)" class="dynamic-field" [style.grid-column]="'span ' + (field.span || 6)">
          <label *ngIf="field.type === 'text' || field.type === 'number'">{{ field.label }}<input [name]="field.key" [type]="field.type === 'number' ? 'number' : 'text'" [min]="field.min" [max]="field.max" [ngModel]="fieldValue(field.key)" (ngModelChange)="setField(field, $event)" [readonly]="!!field.formula || !fieldEditable(field)"></label>
          <app-sheet-roll *ngIf="field.rollExpression" [campaignId]="item.campaignId" [expression]="field.rollExpression" [values]="draft.fields" [label]="field.label" />
          <label *ngIf="field.type === 'longtext'">{{ field.label }}<textarea [name]="field.key" [ngModel]="fieldValue(field.key)" (ngModelChange)="setField(field, $event)" [readonly]="!fieldEditable(field)" rows="5" maxlength="10000"></textarea></label>
          <label *ngIf="field.type === 'select'">{{ field.label }}<select [name]="field.key" [ngModel]="fieldValue(field.key)" (ngModelChange)="setField(field, $event)" [disabled]="!fieldEditable(field)"><option value="">Selecione</option><option *ngFor="let option of field.options" [value]="option">{{ option }}</option></select></label>
          <label *ngIf="field.type === 'boolean'" class="checkbox-field"><input type="checkbox" [name]="field.key" [ngModel]="fieldValue(field.key)" (ngModelChange)="setField(field, $event)" [disabled]="!fieldEditable(field)"> {{ field.label }}</label>
          <app-sheet-collection-field *ngIf="field.type === 'list' || field.type === 'table'" [field]="field" [draft]="draft" [campaignId]="item.campaignId" [mode]="fieldEditable(field) ? 'edit' : 'read'" (changed)="scheduleSave()" />
        </div></div></section>
      </div>
      <div *ngIf="!systemLoading() && !system()?.fields?.length" class="dynamic-fields"><div class="panel-heading"><span>FICHA LIVRE</span><h2>Campos pessoais</h2></div><p class="room-note">Adicione informações para este personagem. Cada campo é salvo junto à ficha.</p>
        <div *ngFor="let field of manualFields()" class="manual-field"><label>{{ manualLabel(field.key) }}<input *ngIf="field.type !== 'boolean'" [name]="field.key" [type]="field.type === 'number' ? 'number' : 'text'" [ngModel]="field.value" (ngModelChange)="setManualField(field.key, $event, field.type)" maxlength="10000"><input *ngIf="field.type === 'boolean'" type="checkbox" [name]="field.key" [ngModel]="field.value" (ngModelChange)="setManualField(field.key, $event, field.type)"></label><button type="button" class="text-link danger" (click)="removeManualField(field.key)">Remover</button></div>
        <div class="manual-field-add"><label>Nome do campo<input name="manualLabel" [(ngModel)]="newManualLabel" maxlength="40" placeholder="Ex.: Objetivo pessoal"></label><label>Tipo<select name="manualType" [(ngModel)]="newManualType"><option value="text">Texto</option><option value="number">Número</option><option value="boolean">Sim/não</option></select></label><button type="button" class="button button-small secondary" [disabled]="!newManualLabel.trim() || manualFields().length >= 50" (click)="addManualField()">Adicionar campo</button></div>
      </div>
      <button class="button" [disabled]="form.invalid || saving()">{{ saving() ? 'Salvando...' : 'Salvar ficha' }}</button><p *ngIf="saved()" class="form-success" role="status">Ficha salva automaticamente.</p>
    </form><div *ngIf="!item.canEdit" class="editor-panel"><div class="panel-heading"><span>FICHA</span><h2>{{ system()?.name || 'Personagem' }}</h2></div><app-sheet-fields-view *ngIf="system()?.fields?.length" [fields]="system()?.fields || []" [values]="draft.fields" /><div *ngIf="!system()?.fields?.length"><div *ngFor="let field of manualFields()" class="sheet-read-row"><span>{{ manualLabel(field.key) }}</span><strong>{{ field.value }}</strong></div></div></div><div class="character-preview"><app-overlay-card [character]="draft" [system]="system()" /><div class="character-actions"><button type="button" class="button button-small print-action" (click)="printSheet()">Imprimir / salvar PDF</button><button *ngIf="item.canEdit" type="button" class="button button-small secondary" [disabled]="!canDuplicate()" (click)="duplicate()">Duplicar ficha</button></div><details *ngIf="item.ownerId === api.user()?.id" class="obs-actions"><summary>Fontes para OBS</summary><h2>Portrait e recursos</h2><p>Gere uma URL de Browser Source. A chave anterior deixa de funcionar a cada nova geração.</p><button class="button button-small" type="button" (click)="createOverlayKey()" [disabled]="saving()">Gerar nova URL</button><p *ngIf="overlayUrl()" class="obs-url"><code>{{ overlayUrl() }}</code><button type="button" (click)="copyUrl()">Copiar</button></p><small *ngIf="overlayUrl()">Guarde esta URL agora; ela não pode ser recuperada depois.</small><h3>Câmera e ficha juntas</h3><p>Adicione esta cena como outra Browser Source. Ela exige entrar na sua conta pelo botão Interagir do OBS e mostra a câmera publicada por este personagem.</p><p class="obs-url"><code>{{ cameraUrl(item) }}</code><button type="button" (click)="copyCameraUrl(item)">Copiar</button></p></details></div></div>
    <div *ngIf="activeTab === 'sheet' && system()?.layout as layout" class="character-editor custom-character-editor">
      <div class="custom-sheet-main"><div class="sheet-play-toolbar"><span>{{ editingSheet() ? 'Editando ficha' : 'Ficha de jogo' }}</span><button *ngIf="item.canEdit" type="button" class="button button-small secondary" [attr.aria-pressed]="editingSheet()" (click)="editingSheet.update(value => !value)">{{ editingSheet() ? 'Concluir edição' : 'Editar ficha' }}</button><small *ngIf="saved()" role="status">Alterações salvas</small></div>
        <app-paranormal-sheet *ngIf="renderer()?.id === 'ordem_paranormal'; else otherSpecialized" [system]="system()!" [isMaster]="master()" [draft]="draft" [character]="item" [mode]="item.canEdit && editingSheet() ? 'edit' : 'read'" (changed)="scheduleSave()" (restored)="onRestored($event)" />
        <ng-template #otherSpecialized><app-dnd-sheet *ngIf="renderer()?.id === 'dnd_5e'; else remainingSpecialized" [system]="system()!" [isMaster]="master()" [draft]="draft" [character]="item" [mode]="item.canEdit && editingSheet() ? 'edit' : 'read'" (changed)="scheduleSave()" (restored)="onRestored($event)" /></ng-template>
        <ng-template #remainingSpecialized><app-coc-sheet *ngIf="renderer()?.id === 'call_of_cthulhu_7e'; else otherRemainingSpecialized" [system]="system()!" [isMaster]="master()" [draft]="draft" [character]="item" [mode]="item.canEdit && editingSheet() ? 'edit' : 'read'" (changed)="scheduleSave()" (restored)="onRestored($event)" /></ng-template>
        <ng-template #otherRemainingSpecialized><app-t20-sheet *ngIf="renderer()?.id === 'tormenta20'; else finalSpecialized" [system]="system()!" [isMaster]="master()" [draft]="draft" [character]="item" [mode]="item.canEdit && editingSheet() ? 'edit' : 'read'" (changed)="scheduleSave()" (restored)="onRestored($event)" /></ng-template>
        <ng-template #finalSpecialized><app-specialized-sheet *ngIf="renderer(); else customLayout" [system]="system()!" [isMaster]="master()" [draft]="draft" [character]="item" [mode]="item.canEdit && editingSheet() ? 'edit' : 'read'" (changed)="scheduleSave()" (restored)="onRestored($event)" /></ng-template>
        <ng-template #customLayout><app-sheet-layout [layout]="layout" [isMaster]="master()" [fields]="system()?.fields || []" [draft]="draft" [character]="item" [mode]="item.canEdit && editingSheet() ? 'edit' : 'read'" (changed)="scheduleSave()" (restored)="onRestored($event)" /></ng-template>
        <div *ngIf="item.canEdit && editingSheet()" class="form-actions"><button type="button" class="button" [disabled]="saving()" (click)="save()">{{ saving() ? 'Salvando...' : 'Salvar ficha' }}</button></div>
      </div>
      <details class="character-preview sheet-utilities"><summary>Ferramentas da ficha e OBS</summary><details class="obs-actions"><summary>Dados estruturados</summary><p>Exporte ou importe os campos da ficha em JSON.</p><button type="button" class="button button-small secondary" (click)="exportCharacter()">Exportar JSON</button><label *ngIf="item.canEdit" class="button button-small secondary">Importar JSON<input type="file" accept="application/json,.json" (change)="importCharacter($event)" [disabled]="saving()" hidden></label></details><app-overlay-card [character]="draft" [system]="system()" /><div class="character-actions"><button type="button" class="button button-small print-action" (click)="printSheet()">Imprimir / salvar PDF</button><button *ngIf="item.canEdit" type="button" class="button button-small secondary" [disabled]="!canDuplicate()" (click)="duplicate()">Duplicar ficha</button></div>
        <details *ngIf="item.ownerId === api.user()?.id" class="obs-actions"><summary>Fontes para OBS</summary><p>Gere uma URL de retrato para a fonte de navegador ou use a câmera junto à ficha.</p><button class="button button-small" type="button" (click)="createOverlayKey()" [disabled]="saving()">Gerar nova URL</button><p *ngIf="overlayUrl()" class="obs-url"><code>{{ overlayUrl() }}</code><button type="button" (click)="copyUrl()">Copiar</button></p><p class="obs-url"><code>{{ cameraUrl(item) }}</code><button type="button" (click)="copyCameraUrl(item)">Copiar câmera</button></p></details>
      </details>
    </div>
    <section class="print-sheet"><app-paranormal-sheet *ngIf="renderer()?.id === 'ordem_paranormal'; else otherPrintSpecialized" [system]="system()!" [isMaster]="master()" [draft]="draft" [character]="item" mode="print" /><ng-template #otherPrintSpecialized><app-dnd-sheet *ngIf="renderer()?.id === 'dnd_5e'; else remainingPrintSpecialized" [system]="system()!" [isMaster]="master()" [draft]="draft" [character]="item" mode="print" /></ng-template><ng-template #remainingPrintSpecialized><app-coc-sheet *ngIf="renderer()?.id === 'call_of_cthulhu_7e'; else finalPrintSpecialized" [system]="system()!" [isMaster]="master()" [draft]="draft" [character]="item" mode="print" /></ng-template><ng-template #finalPrintSpecialized><app-t20-sheet *ngIf="renderer()?.id === 'tormenta20'; else remainingPrintLayout" [system]="system()!" [isMaster]="master()" [draft]="draft" [character]="item" mode="print" /></ng-template><ng-template #remainingPrintLayout><app-specialized-sheet *ngIf="renderer(); else printCustom" [system]="system()!" [isMaster]="master()" [draft]="draft" [character]="item" mode="print" /></ng-template><ng-template #printCustom><app-sheet-layout *ngIf="system()?.layout as layout; else legacyPrint" [layout]="layout" [isMaster]="master()" [fields]="system()?.fields || []" [draft]="draft" [character]="item" mode="print" /></ng-template>
      <ng-template #legacyPrint><h2>{{ draft.name }}</h2><p>Vida {{ draft.health }}/{{ draft.maxHealth }} · Sanidade {{ draft.sanity }}/{{ draft.maxSanity }} · Mana {{ draft.mana }}/{{ draft.maxMana }}</p><app-sheet-fields-view *ngIf="system()?.fields?.length" [fields]="system()?.fields || []" [values]="draft.fields" /><div *ngIf="!system()?.fields?.length"><div *ngFor="let field of manualFields()" class="sheet-read-row"><span>{{ manualLabel(field.key) }}</span><strong>{{ field.value }}</strong></div></div></ng-template>
    </section>
    <app-character-inventory *ngIf="activeTab === 'inventory'" [character]="item" />
    <app-character-abilities *ngIf="activeTab === 'abilities'" [character]="item" />
    <app-character-history *ngIf="activeTab === 'history'" [character]="item" (restored)="onRestored($event)" />
    <section *ngIf="master() && activeTab === 'secret'" class="editor-panel character-secret"><div class="panel-heading"><span>SOMENTE MESTRES</span><h2>Anotações secretas</h2></div><p class="room-note">Estas notas ficam em uma área separada da ficha pública. Jogadores não recebem o conteúdo pela API.</p><label>Segredos e planejamento<textarea [(ngModel)]="secretDraft" rows="7" maxlength="10000" placeholder="Informações ainda não reveladas..."></textarea></label><div class="form-actions"><button type="button" class="button" [disabled]="secretBusy() || !secret()" (click)="saveSecret()">{{ secretBusy() ? 'Salvando...' : 'Salvar notas secretas' }}</button><span *ngIf="secretSaved()" class="form-success" role="status">Notas salvas.</span></div></section>
  </main><main class="page" *ngIf="loading() || systemLoading()"><div class="loading">Abrindo ficha...</div></main><main class="page" *ngIf="!loading() && !systemLoading() && (!character() || systemFailed()) && error()"><div class="notice error" role="alert">{{ error() }}</div></main>` })
export class CharacterEditorPage implements OnDestroy {
  api = inject(ApiService); private route = inject(ActivatedRoute); private router = inject(Router); private document = inject(DOCUMENT);
  character = signal<Character | null>(null); system = signal<RpgSystem | null>(null); systemLoading = signal(true); systemFailed = signal(false); draft: CharacterInput = emptyCharacter(); loading = signal(true); saving = signal(false); saved = signal(false); error = signal(''); overlayUrl = signal('');
  master = signal(false); secret = signal<CharacterSecret | null>(null); secretDraft = ''; secretBusy = signal(false); secretSaved = signal(false);
  favorite = signal(false); favoriteBusy = signal(false);
  editingSheet = signal(false);
  renderer = () => resolveSheetRenderer(this.system());
  visibleResources = () => sheetResources(this.system());
  maxKey(key: 'health' | 'sanity' | 'mana'): 'maxHealth' | 'maxSanity' | 'maxMana' { return key === 'health' ? 'maxHealth' : key === 'sanity' ? 'maxSanity' : 'maxMana'; }
  activeTab: 'sheet' | 'inventory' | 'abilities' | 'history' | 'secret' = 'sheet';
  newManualLabel = ''; newManualType: 'text' | 'number' | 'boolean' = 'text';
  private saveTimer?: ReturnType<typeof setTimeout>; private editRevision = 0; private routeSubscription: Subscription;
  resources: { label: string; key: 'health' | 'sanity' | 'mana'; max: 'maxHealth' | 'maxSanity' | 'maxMana' }[] = [
    { label: 'Vida', key: 'health', max: 'maxHealth' }, { label: 'Sanidade', key: 'sanity', max: 'maxSanity' }, { label: 'Mana', key: 'mana', max: 'maxMana' }
  ];
  constructor() { this.routeSubscription = this.route.paramMap.subscribe(params => this.loadCharacter(params.get('id')!)); }
  private loadCharacter(id: string) {
    clearTimeout(this.saveTimer);
    this.loading.set(true); this.systemLoading.set(true); this.systemFailed.set(false); this.character.set(null); this.system.set(null);
    this.activeTab = 'sheet'; this.editingSheet.set(false); this.favorite.set(false); this.master.set(false); this.secret.set(null);
    this.secretDraft = ''; this.overlayUrl.set(''); this.error.set('');
    this.api.character(id).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: item => {
        this.character.set(item); this.draft = { ...item, fields: { ...item.fields } };
        this.api.characterFavorite(id).subscribe({ next: value => this.favorite.set(value.favorite), error: e => this.error.set(apiError(e)) });
        this.api.campaign(item.campaignId).subscribe({
          next: campaign => {
            const master = ['owner', 'gm', 'co-gm'].includes(campaign.role); this.master.set(master);
            if (master) this.api.characterSecret(id).subscribe({ next: secret => { this.secret.set(secret); this.secretDraft = secret.notes; }, error: e => this.error.set(apiError(e)) });
            if (campaign.systemId) this.api.system(campaign.systemId).pipe(finalize(() => this.systemLoading.set(false))).subscribe({ next: system => { this.system.set(system); this.recalculate(); }, error: e => { this.systemFailed.set(true); this.error.set(apiError(e)); } });
            else this.systemLoading.set(false);
          }, error: e => { this.systemFailed.set(true); this.systemLoading.set(false); this.error.set(apiError(e)); }
        });
      }, error: error => { this.systemLoading.set(false); this.error.set(apiError(error)); }
    });
  }
  sheetSections() { return [...new Set(this.system()?.fields.map(field => field.section) || [])]; }
  sectionFields(section: string) { return this.system()?.fields.filter(field => field.section === section) || []; }
  manualFields() { return Object.entries(this.draft.fields).map(([key, value]) => ({ key, value, type: typeof value as 'string' | 'number' | 'boolean' })); }
  manualLabel(key: string) { return key.replaceAll('_', ' ').replace(/^./, letter => letter.toUpperCase()); }
  setManualField(key: string, value: string | number | boolean, type: string) { this.draft.fields[key] = type === 'number' ? Number(value) : value; this.scheduleSave(); }
  addManualField() { const key = this.newManualLabel.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, ''); if (!/^[a-z][a-z0-9_]{1,39}$/.test(key) || this.draft.fields[key] !== undefined) { this.error.set('Use outro nome para este campo.'); return; } this.draft.fields[key] = this.newManualType === 'number' ? 0 : this.newManualType === 'boolean' ? false : ''; this.newManualLabel = ''; this.error.set(''); this.scheduleSave(); }
  removeManualField(key: string) { delete this.draft.fields[key]; this.scheduleSave(); }
  fieldValue(key: string) { return this.draft.fields[key] ?? ''; }
  fieldEditable(field: SystemField) { return !field.formula && (field.editRole !== 'gm' || this.master()); }
  setField(field: SystemField, value: string | number | boolean) { if (!this.fieldEditable(field)) return; if (value === '' || value === null) delete this.draft.fields[field.key]; else this.draft.fields[field.key] = field.type === 'number' ? Number(value) : value; this.scheduleSave(); }
  private recalculate() { const before = JSON.stringify(this.draft.fields); const error = calculateSheet(this.system()?.fields || [], this.draft.fields); if (error) this.error.set(error); else if (before !== JSON.stringify(this.draft.fields) && this.character()?.canEdit) this.scheduleSave(); }
  scheduleSave() { this.editRevision++; this.saved.set(false); clearTimeout(this.saveTimer); if (calculateSheet(this.system()?.fields || [], this.draft.fields)) { this.error.set('Confira as fórmulas e os valores da ficha.'); return; } this.error.set(''); this.saveTimer = setTimeout(() => { if (this.validDraft()) this.save(); }, 900); }
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
  exportCharacter() {
    const item = this.character(); if (!item) return;
    const { name, portraitUrl, backgroundUrl, health, maxHealth, sanity, maxSanity, mana, maxMana, fields } = this.draft;
    const document = { format: 'cronicas-character', formatVersion: 1, systemId: this.system()?.id || null, templateId: this.system()?.templateId || null, templateVersion: this.system()?.templateVersion || 1, character: { name, portraitUrl, backgroundUrl, health, maxHealth, sanity, maxSanity, mana, maxMana, fields } };
    const blob = new Blob([JSON.stringify(document, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob); const link = this.document.createElement('a'); link.href = url; link.download = `cronicas-${item.id}.json`; link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
  async importCharacter(event: Event) {
    const input = event.target as HTMLInputElement; const file = input.files?.[0]; input.value = '';
    const current = this.character(); if (!file || !current?.canEdit || this.saving()) return;
    try {
      if (file.size > 256_000) throw new Error('Arquivo JSON acima de 256 KB.');
      const document = JSON.parse(await file.text()) as { format?: string; formatVersion?: number; systemId?: string | null; character?: Partial<CharacterInput> };
      if (document.format !== 'cronicas-character' || document.formatVersion !== 1 || document.systemId !== (this.system()?.id || null) || !document.character || typeof document.character.name !== 'string' || !document.character.fields || typeof document.character.fields !== 'object' || Array.isArray(document.character.fields))
        throw new Error('Arquivo de ficha incompatível com o sistema desta campanha.');
      const source = document.character;
      const fields = { ...source.fields };
      for (const field of this.system()?.fields || []) if (!this.fieldEditable(field) && JSON.stringify(fields[field.key]) !== JSON.stringify(this.draft.fields[field.key])) throw new Error(`O campo ${field.label} só pode ser alterado pelo mestre.`);
      const resources = ['health', 'maxHealth', 'sanity', 'maxSanity', 'mana', 'maxMana'] as const;
      if (resources.some(key => typeof source[key] !== 'number' || !Number.isInteger(source[key]))) throw new Error('Recursos inválidos no arquivo.');
      const previous = this.draft;
      this.draft = { ...this.draft, name: source.name!, portraitUrl: source.portraitUrl || '', backgroundUrl: source.backgroundUrl || '', fields, health: source.health!, maxHealth: source.maxHealth!, sanity: source.sanity!, maxSanity: source.maxSanity!, mana: source.mana!, maxMana: source.maxMana! };
      if (!this.validDraft()) { this.draft = previous; throw new Error('Os valores importados estão fora dos limites da ficha.'); }
      this.scheduleSave();
    } catch (error) { this.error.set(error instanceof Error ? error.message : 'Não foi possível importar a ficha.'); }
  }
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
  <div class="obs-card" [class.obs-order]="templateId() === 'ordem_paranormal'" [class.obs-dnd]="templateId() === 'dnd_5e'" [class.obs-t20]="templateId() === 'tormenta20'" [class.obs-coc]="templateId() === 'call_of_cthulhu_7e'" [style.background-image]="character.backgroundUrl ? 'url(' + character.backgroundUrl + ')' : null">
    <div class="obs-portrait"><img *ngIf="character.portraitUrl" [src]="character.portraitUrl" alt=""><span *ngIf="!character.portraitUrl">{{ character.name.slice(0, 1) || 'C' }}</span></div>
    <div class="obs-info"><h2>{{ character.name || 'Personagem' }}</h2><div *ngIf="indicators().length" class="obs-indicators"><span *ngFor="let item of indicators()"><small>{{ item.label }}</small><strong>{{ item.value }}</strong></span></div><div class="obs-resources">
      <div *ngFor="let item of resources()"><span>{{ item.label }}</span><b>{{ item.current }} / {{ item.max }}</b><i [style.width.%]="percent(item.current, item.max)"></i></div>
    </div></div>
  </div>` })
export class OverlayCardComponent {
  @Input() character: CharacterInput | OverlayCharacter = emptyCharacter();
  @Input() system: RpgSystem | null = null;
  templateId() { return this.system?.templateId || ('templateId' in this.character ? this.character.templateId : null); }
  resources(): OverlayResource[] {
    if ('resources' in this.character) return this.character.resources;
    return sheetOverlayResources(this.system, this.character);
  }
  indicators(): OverlayIndicator[] { return 'indicators' in this.character ? this.character.indicators : sheetIndicators(this.system, this.character.fields); }
  maxKey(key: 'health' | 'sanity' | 'mana'): 'maxHealth' | 'maxSanity' | 'maxMana' { return key === 'health' ? 'maxHealth' : key === 'sanity' ? 'maxSanity' : 'maxMana'; }
  percent(value: number, max: number) { return Math.max(0, Math.min(100, max ? value / max * 100 : 0)); }
}

@Component({ standalone: true, imports: [CommonModule, OverlayCardComponent], template: `<div class="obs-page"><app-overlay-card *ngIf="character() as item" [character]="item" /><p *ngIf="error()" role="alert">Fonte indisponível. Gere uma nova URL na ficha.</p></div>` })
export class ObsOverlayPage implements OnDestroy {
  private api = inject(ApiService); private route = inject(ActivatedRoute);
  character = signal<OverlayCharacter | null>(null); error = signal(false);
  private polling: Subscription;
  constructor() { const key = this.route.snapshot.paramMap.get('key')!; this.polling = timer(0, 3000).subscribe(() => this.api.overlay(key).subscribe({ next: item => { this.character.set(item); this.error.set(false); }, error: () => { this.character.set(null); this.error.set(true); } })); }
  ngOnDestroy() { this.polling.unsubscribe(); }
}

