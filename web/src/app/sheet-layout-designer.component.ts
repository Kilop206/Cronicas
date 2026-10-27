import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { SheetBlock, SheetLayout, SheetPage, SystemField } from './api';

const kinds: { value: SheetBlock['kind']; label: string }[] = [
  { value: 'identity', label: 'Identidade' }, { value: 'portrait', label: 'Retrato' },
  { value: 'resource', label: 'Recurso' }, { value: 'field', label: 'Campo' },
  { value: 'note', label: 'Texto' }, { value: 'inventory', label: 'Inventário' },
  { value: 'abilities', label: 'Habilidades' }, { value: 'history', label: 'Histórico' }
];

@Component({ selector: 'app-sheet-layout-designer', standalone: true, imports: [CommonModule], template: `
  <section class="layout-designer">
    <div class="section-heading"><div><span class="overline">COMPOSIÇÃO</span><h2>Páginas e blocos</h2></div></div>
    <p class="room-note">Monte a ficha como um arquivo de páginas. Na grade, escolha a largura; na página livre, arraste os blocos na prévia ou ajuste as posições abaixo.</p>
    <div class="sheet-appearance-controls"><label>Estilo da ficha<select [value]="layout.skin || 'archive'" (change)="setSkin($any($event.target).value)" [disabled]="readOnly"><option value="investigation">Investigação</option><option value="fantasy">Fantasia</option><option value="archive">Arquivo clássico</option></select></label><label>Cor de destaque<input type="color" [value]="layout.accentColor" (input)="layout.accentColor = $any($event.target).value" [disabled]="readOnly"></label></div>
    <div class="layout-page-tabs" role="tablist" aria-label="Páginas da ficha">
      <button *ngFor="let page of layout.pages" type="button" role="tab" [attr.aria-selected]="selectedPage().id === page.id" [class.active]="selectedPage().id === page.id" (click)="selectPage(page.id)">{{ page.title || 'Página' }}</button>
      <button *ngIf="!readOnly" type="button" class="button" [disabled]="layout.pages.length >= 8" (click)="addPage()">Adicionar página</button>
    </div>
    <ng-container *ngIf="selectedPage() as page"><div class="layout-page-controls"><label>Título da página<input [value]="page.title" (input)="page.title = $any($event.target).value" maxlength="60" [disabled]="readOnly"></label><div *ngIf="!readOnly" class="field-actions"><button type="button" class="text-link" [disabled]="pageIndex(page) === 0" (click)="movePage(page, -1)" aria-label="Mover página para a esquerda">←</button><button type="button" class="text-link" [disabled]="pageIndex(page) === layout.pages.length - 1" (click)="movePage(page, 1)" aria-label="Mover página para a direita">→</button><button type="button" class="text-link danger" [disabled]="layout.pages.length === 1" (click)="removePage(page)">Excluir página</button></div></div>
      <div class="layout-page-options"><label>Diagramação<select [value]="page.layoutMode || 'grid'" (change)="setPageMode(page, $any($event.target).value)" [disabled]="readOnly"><option value="grid">Grade de 12 colunas</option><option value="canvas">Posicionamento livre</option></select></label><label *ngIf="page.layoutMode === 'canvas'">Proporção largura / altura<input type="number" [value]="page.aspectRatio || 0.75" (change)="page.aspectRatio = +$any($event.target).value" min="0.5" max="2" step="0.01" [disabled]="readOnly"></label><label *ngIf="page.layoutMode === 'canvas'" class="wide">Imagem de fundo HTTPS<input type="url" [value]="page.backgroundUrl || ''" (input)="page.backgroundUrl = $any($event.target).value || null" maxlength="2048" placeholder="https://..." [disabled]="readOnly"></label></div>
      <div *ngIf="!readOnly" class="layout-add"><label>Novo bloco<select [value]="kindToAdd" (change)="kindToAdd = $any($event.target).value"><option *ngFor="let kind of kinds" [value]="kind.value">{{ kind.label }}</option></select></label><button type="button" class="button" [disabled]="totalBlocks() >= 100 || page.blocks.length >= 40 || kindToAdd === 'field' && !fields.length" (click)="addBlock(page)">Adicionar bloco</button></div>
      <div *ngFor="let block of page.blocks; let i = index" class="layout-block-editor">
        <div class="system-field-head"><strong>{{ kindLabel(block.kind) }} <small>{{ block.title }}</small></strong><div *ngIf="!readOnly" class="field-actions"><button type="button" class="text-link" [disabled]="i === 0" (click)="moveBlock(page, i, -1)" aria-label="Mover bloco para cima">↑</button><button type="button" class="text-link" [disabled]="i === page.blocks.length - 1" (click)="moveBlock(page, i, 1)" aria-label="Mover bloco para baixo">↓</button><button type="button" class="text-link danger" (click)="removeBlock(page, i)">Remover</button></div></div>
        <div class="layout-block-options"><label>Título<input [value]="block.title || ''" (input)="block.title = $any($event.target).value" maxlength="80" [disabled]="readOnly"></label><label *ngIf="page.layoutMode !== 'canvas'">Largura<select [value]="block.span" (change)="block.span = +$any($event.target).value" [disabled]="readOnly"><option *ngFor="let width of widths" [value]="width">{{ width }} / 12</option></select></label><label>Visual<select [value]="block.style || 'panel'" (change)="block.style = $any($event.target).value" [disabled]="readOnly"><option value="panel">Painel</option><option value="plain">Limpo</option><option value="accent">Destaque</option></select></label>
          <ng-container *ngIf="page.layoutMode === 'canvas'"><label>Esquerda %<input type="number" [value]="block.x ?? 0" (change)="block.x = +$any($event.target).value" min="0" max="95" [disabled]="readOnly"></label><label>Topo %<input type="number" [value]="block.y ?? 0" (change)="block.y = +$any($event.target).value" min="0" max="95" [disabled]="readOnly"></label><label>Largura %<input type="number" [value]="block.width ?? 49" (change)="block.width = +$any($event.target).value" min="5" max="100" [disabled]="readOnly"></label><label>Altura %<input type="number" [value]="block.height ?? 18" (change)="block.height = +$any($event.target).value" min="5" max="100" [disabled]="readOnly"></label></ng-container>
          <label *ngIf="block.kind === 'field'">Campo<select [value]="block.key || ''" (change)="block.key = $any($event.target).value" [disabled]="readOnly"><option *ngFor="let field of fields" [value]="field.key">{{ field.label || field.key }}</option></select></label>
          <label *ngIf="block.kind === 'resource'">Valor atual<select [value]="block.key || 'health'" (change)="setResourceKey(block, $any($event.target).value)" [disabled]="readOnly"><option value="health">Vida</option><option value="sanity">Sanidade</option><option value="mana">Mana</option><option *ngFor="let field of numericFields()" [value]="field.key">{{ field.label || field.key }}</option></select></label>
          <label *ngIf="block.kind === 'resource'">Valor máximo<select [value]="block.maxKey || ''" (change)="block.maxKey = $any($event.target).value || null" [disabled]="readOnly"><option value="">{{ isBuiltInResource(block.key) ? 'Automático' : 'Escolha um campo' }}</option><option value="maxHealth">Vida máxima</option><option value="maxSanity">Sanidade máxima</option><option value="maxMana">Mana máxima</option><option *ngFor="let field of numericFields()" [value]="field.key">{{ field.label || field.key }}</option></select></label>
          <label *ngIf="block.kind === 'note'" class="wide">Conteúdo<textarea [value]="block.text || ''" (input)="block.text = $any($event.target).value" maxlength="2000" rows="3" [disabled]="readOnly"></textarea></label>
          <label *ngIf="layout.pages.length > 1 && !readOnly">Mover para página<select [value]="page.id" (change)="moveToPage(page, block, $any($event.target).value)"><option *ngFor="let destination of layout.pages" [value]="destination.id">{{ destination.title }}</option></select></label>
        </div>
      </div>
      <p *ngIf="!page.blocks.length" class="room-note">Esta página está vazia. Adicione um bloco para começar.</p>
    </ng-container>
    <details class="layout-json" (toggle)="showJson($event)"><summary>Editar layout em JSON</summary><p class="room-note">Formato avançado validado pelo servidor. Nenhum código é executado.</p><textarea [value]="jsonDraft()" (input)="jsonDraft.set($any($event.target).value)" rows="12" spellcheck="false" [disabled]="readOnly"></textarea><button *ngIf="!readOnly" type="button" class="button button-small secondary" (click)="applyJson()">Aplicar JSON</button><p *ngIf="jsonError()" class="form-error" role="alert">{{ jsonError() }}</p></details>
  </section>`, styles: [`
    .layout-designer { margin-top: 32px; border-top: 1px solid var(--border); padding-top: 28px; }
    .sheet-appearance-controls { display: grid; grid-template-columns: minmax(0, 220px) minmax(0, 140px); gap: 12px; align-items: end; margin: 22px 0; }
    .sheet-appearance-controls label { margin: 0; }
    .sheet-appearance-controls input[type=color] { width: 72px; height: 40px; margin-top: 8px; }
    .layout-page-tabs { display: flex; flex-wrap: wrap; gap: 7px; margin: 24px 0; border-bottom: 1px solid var(--border); padding-bottom: 10px; }
    .layout-page-tabs button[role=tab] { border: 1px solid var(--border); border-radius: 4px 4px 0 0; background: transparent; color: var(--muted); padding: 8px 12px; }
    .layout-page-tabs button[role=tab].active { border-color: var(--gold); background: color-mix(in srgb, var(--gold) 10%, var(--surface)); color: var(--gold-bright); }
    .layout-page-tabs .button { margin-left: auto; }
    .layout-page-controls { display: flex; align-items: end; justify-content: space-between; flex-wrap: wrap; gap: 12px; }
    .layout-page-controls label { flex: 1 1 180px; margin: 0; }
    .layout-page-options { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; margin: 18px 0; }
    .layout-page-options .wide { grid-column: 1 / -1; }
    .layout-add { display: flex; align-items: end; flex-wrap: wrap; gap: 10px; border-top: 1px solid var(--border); padding-top: 18px; margin: 22px 0; }
    .layout-add label { flex: 1 1 180px; margin: 0; }
    .layout-block-editor { border-top: 1px solid var(--border); padding: 17px 0; }
    .layout-block-editor small { color: var(--muted); font-weight: 400; }
    .layout-block-options { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; }
    .layout-block-options label { min-width: 0; margin: 0; }
    .layout-block-options .wide { grid-column: 1 / -1; }
    .field-actions { display: flex; flex-wrap: wrap; gap: 9px; }
    .layout-json { border-top: 1px solid var(--border); padding-top: 16px; margin-top: 24px; }
    .layout-json summary { cursor: pointer; color: var(--gold-bright); }
    .layout-json textarea { width: 100%; }
    @media (max-width: 620px) { .sheet-appearance-controls, .layout-block-options, .layout-page-options { grid-template-columns: 1fr; } .layout-block-options .wide, .layout-page-options .wide { grid-column: auto; } }
  `] })
export class SheetLayoutDesignerComponent {
  @Input({ required: true }) layout!: SheetLayout;
  @Input() fields: SystemField[] = [];
  @Input() readOnly = false;
  @Output() layoutChange = new EventEmitter<SheetLayout>();
  @Output() pageSelected = new EventEmitter<string>();
  kinds = kinds;
  widths = [3, 4, 6, 8, 9, 12];
  kindToAdd: SheetBlock['kind'] = 'field';
  selectedPageId = signal('principal');
  jsonDraft = signal('');
  jsonError = signal('');
  selectedPage() { return this.layout.pages.find(x => x.id === this.selectedPageId()) || this.layout.pages[0]; }
  selectPage(id: string) { this.selectedPageId.set(id); this.pageSelected.emit(id); }
  pageIndex(page: SheetPage) { return this.layout.pages.indexOf(page); }
  totalBlocks() { return this.layout.pages.reduce((sum, page) => sum + page.blocks.length, 0); }
  kindLabel(kind: SheetBlock['kind']) { return kinds.find(x => x.value === kind)?.label || kind; }
  numericFields() { return this.fields.filter(field => field.type === 'number'); }
  isBuiltInResource(key: string | null) { return key === 'health' || key === 'sanity' || key === 'mana'; }
  setResourceKey(block: SheetBlock, key: string) { block.key = key; block.maxKey = null; }
  setSkin(skin: 'archive' | 'investigation' | 'fantasy') {
    const colors = { archive: '#b8a079', investigation: '#aa9acd', fantasy: '#c4a477' };
    const previous = this.layout.skin || 'archive';
    if (this.layout.accentColor.toLowerCase() === colors[previous]) this.layout.accentColor = colors[skin];
    this.layout.skin = skin;
  }
  setPageMode(page: SheetPage, mode: 'grid' | 'canvas') { page.layoutMode = mode; if (mode === 'canvas') { page.aspectRatio ??= 0.75; page.blocks.forEach((block, index) => this.placeBlock(block, index)); } }
  private placeBlock(block: SheetBlock, index: number) { block.x ??= (index % 2) * 51; block.y ??= Math.min(Math.floor(index / 2) * 20, 80); block.width ??= 49; block.height ??= 18; }
  private id(prefix: string) { return `${prefix}_${Math.random().toString(36).slice(2, 10)}`; }
  addPage() { const page: SheetPage = { id: this.id('pagina'), title: 'Nova página', blocks: [{ id: this.id('bloco'), kind: 'note', span: 12, title: 'Introdução', key: null, text: 'Descreva esta página.', style: 'plain' }] }; this.layout.pages.push(page); this.selectPage(page.id); }
  removePage(page: SheetPage) { this.layout.pages.splice(this.pageIndex(page), 1); this.selectPage(this.layout.pages[0].id); }
  movePage(page: SheetPage, step: number) { const index = this.pageIndex(page); this.layout.pages.splice(index, 1); this.layout.pages.splice(index + step, 0, page); }
  addBlock(page: SheetPage) { const kind = this.kindToAdd; const block: SheetBlock = { id: this.id('bloco'), kind, span: kind === 'inventory' || kind === 'abilities' || kind === 'history' ? 12 : 6, title: this.kindLabel(kind), key: kind === 'field' ? this.fields[0]?.key || null : kind === 'resource' ? 'health' : null, text: kind === 'note' ? 'Novo texto' : null, style: 'panel' }; if (page.layoutMode === 'canvas') this.placeBlock(block, page.blocks.length); page.blocks.push(block); }
  removeBlock(page: SheetPage, index: number) { page.blocks.splice(index, 1); }
  moveBlock(page: SheetPage, index: number, step: number) { const [block] = page.blocks.splice(index, 1); page.blocks.splice(index + step, 0, block); }
  moveToPage(page: SheetPage, block: SheetBlock, targetId: string) { if (page.id === targetId) return; const target = this.layout.pages.find(x => x.id === targetId); if (!target || target.blocks.length >= 40) return; page.blocks.splice(page.blocks.indexOf(block), 1); if (target.layoutMode === 'canvas') this.placeBlock(block, target.blocks.length); target.blocks.push(block); this.selectPage(target.id); }
  showJson(event: Event) { if ((event.target as HTMLDetailsElement).open) this.jsonDraft.set(JSON.stringify(this.layout, null, 2)); }
  applyJson() {
    try {
      const parsed: unknown = JSON.parse(this.jsonDraft());
      if (!this.isLayout(parsed)) throw new Error();
      this.layoutChange.emit(parsed);
      this.selectPage(parsed.pages[0].id);
      this.jsonError.set('');
    } catch { this.jsonError.set('Layout inválido. Defina cor, páginas e blocos válidos; cada campo precisa existir no sistema.'); }
  }
  private isLayout(value: unknown): value is SheetLayout {
    if (!value || typeof value !== 'object') return false;
    const layout = value as SheetLayout;
    if (!/^#[0-9a-fA-F]{6}$/.test(layout.accentColor) || !Array.isArray(layout.pages) || layout.pages.length < 1 || layout.pages.length > 8) return false;
    if (layout.skin != null && !['archive', 'investigation', 'fantasy'].includes(layout.skin)) return false;
    const pages = new Set<string>(); const blocks = new Set<string>(); let total = 0;
    for (const page of layout.pages) {
      if (!page || typeof page.id !== 'string' || !/^[a-z][a-z0-9_]{1,39}$/.test(page.id) || pages.has(page.id)
        || typeof page.title !== 'string' || page.title.trim().length < 2 || page.title.length > 60
        || !Array.isArray(page.blocks) || page.blocks.length < 1 || page.blocks.length > 40) return false;
      if (page.layoutMode != null && !['grid', 'canvas'].includes(page.layoutMode)) return false;
      if (page.aspectRatio != null && (!Number.isFinite(page.aspectRatio) || page.aspectRatio < 0.5 || page.aspectRatio > 2)) return false;
      if (page.backgroundUrl != null && (typeof page.backgroundUrl !== 'string' || page.backgroundUrl.length > 2048 || new URL(page.backgroundUrl).protocol !== 'https:')) return false;
      pages.add(page.id); total += page.blocks.length;
      if (total > 100) return false;
      for (const block of page.blocks) {
        if (!block || typeof block.id !== 'string' || !/^[a-z][a-z0-9_]{1,39}$/.test(block.id) || blocks.has(block.id)
          || !kinds.some(kind => kind.value === block.kind) || !Number.isInteger(block.span) || block.span < 1 || block.span > 12
          || (block.style != null && !['panel', 'plain', 'accent'].includes(block.style))
          || (block.title != null && (typeof block.title !== 'string' || block.title.length > 80))) return false;
        blocks.add(block.id);
        if (block.kind === 'field' && !this.fields.some(field => field.key === block.key)) return false;
        if (block.kind === 'resource') {
          const builtIn = this.isBuiltInResource(block.key);
          const numeric = this.numericFields().some(field => field.key === block.key);
          const maxValid = ['maxHealth', 'maxSanity', 'maxMana'].includes(block.maxKey || '') || this.numericFields().some(field => field.key === block.maxKey);
          if ((!builtIn && !numeric) || (!builtIn && !block.maxKey) || (block.maxKey && !maxValid) || block.key === block.maxKey) return false;
        }
        if (block.kind === 'note' && (typeof block.text !== 'string' || !block.text.trim() || block.text.length > 2000)) return false;
        if (['inventory', 'abilities', 'history'].includes(block.kind) && block.span < 6) return false;
        if (page.layoutMode === 'canvas' && (![block.x, block.y, block.width, block.height].every(Number.isFinite)
          || block.x! < 0 || block.y! < 0 || block.width! < 5 || block.height! < 5
          || block.x! + block.width! > 100 || block.y! + block.height! > 100)) return false;
      }
    }
    return true;
  }
}

@Component({ selector: 'app-sheet-layout-preview', standalone: true, imports: [CommonModule], template: `
  <div class="sheet-canvas" [class.sheet-skin-investigation]="layout.skin === 'investigation'" [class.sheet-skin-fantasy]="layout.skin === 'fantasy'" [style.--sheet-accent]="layout.accentColor">
    <header class="sheet-masthead"><div><span>CRÔNICAS / PRÉVIA DA FICHA</span><strong>Personagem de exemplo</strong></div><small>{{ layout.pages.length }} {{ layout.pages.length === 1 ? 'PÁGINA' : 'PÁGINAS' }}</small></header>
    <div *ngFor="let page of visiblePages()" class="sheet-canvas-page"><div class="sheet-page-heading"><span>CAPÍTULO {{ pageNumber(page) }}</span><h3>{{ page.title }}</h3></div><div class="sheet-canvas-grid" [class.free-canvas]="page.layoutMode === 'canvas'" [style.aspect-ratio]="page.layoutMode === 'canvas' ? page.aspectRatio || 0.75 : null" [style.background-image]="page.layoutMode === 'canvas' && page.backgroundUrl ? 'url(' + page.backgroundUrl + ')' : null">
      <div *ngFor="let block of page.blocks" class="sheet-canvas-block" [class.plain]="block.style === 'plain'" [class.accent]="block.style === 'accent'" [class.kind-identity]="block.kind === 'identity'" [class.kind-portrait]="block.kind === 'portrait'" [class.kind-resource]="block.kind === 'resource'" [class.kind-field]="block.kind === 'field'" [class.draggable]="page.layoutMode === 'canvas' && !readOnly" [style.grid-column]="'span ' + block.span" [style.left.%]="page.layoutMode === 'canvas' ? block.x : null" [style.top.%]="page.layoutMode === 'canvas' ? block.y : null" [style.width.%]="page.layoutMode === 'canvas' ? block.width : null" [style.height.%]="page.layoutMode === 'canvas' ? block.height : null" (pointerdown)="startDrag($event, page, block)" (pointermove)="dragBlock($event)" (pointerup)="stopDrag($event)" (pointercancel)="stopDrag($event)">
        <small>{{ block.title || blockLabel(block) }}</small>
        <strong *ngIf="block.kind === 'identity'">Nome da personagem</strong>
        <div *ngIf="block.kind === 'portrait'" class="sheet-canvas-portrait">Retrato</div>
        <strong *ngIf="block.kind === 'resource'">{{ block.title || resourceLabel(block.key) }} · 10 / 20</strong>
        <strong *ngIf="block.kind === 'field'">{{ fieldLabel(block.key) }}</strong>
        <p *ngIf="block.kind === 'note'">{{ block.text }}</p>
        <span *ngIf="block.kind === 'inventory' || block.kind === 'abilities' || block.kind === 'history'">Lista de {{ blockLabel(block).toLowerCase() }}</span>
      </div>
    </div></div>
  </div>` })
export class SheetLayoutPreviewComponent {
  @Input({ required: true }) layout!: SheetLayout;
  @Input() fields: SystemField[] = [];
  @Input() readOnly = false;
  @Input() pageId = '';
  private dragging: { pointerId: number; block: SheetBlock; x: number; y: number; clientX: number; clientY: number; width: number; height: number } | null = null;
  blockLabel(block: SheetBlock) { return kinds.find(x => x.value === block.kind)?.label || block.kind; }
  visiblePages() { return [this.layout.pages.find(page => page.id === this.pageId) || this.layout.pages[0]]; }
  pageNumber(page: SheetPage) { return String(this.layout.pages.indexOf(page) + 1).padStart(2, '0'); }
  fieldLabel(key: string | null) { return this.fields.find(x => x.key === key)?.label || key || 'Campo'; }
  resourceLabel(key: string | null) { return this.fields.find(field => field.key === key)?.label || ({ health: 'Vida', sanity: 'Sanidade', mana: 'Mana', maxHealth: 'Vida máxima', maxSanity: 'Sanidade máxima', maxMana: 'Mana máxima' } as Record<string, string>)[key || ''] || 'Recurso'; }
  startDrag(event: PointerEvent, page: SheetPage, block: SheetBlock) {
    if (this.readOnly || page.layoutMode !== 'canvas' || event.button !== 0 || window.matchMedia('(max-width: 620px)').matches) return;
    const element = event.currentTarget as HTMLElement;
    const canvas = element.parentElement;
    if (!canvas) return;
    event.preventDefault(); element.setPointerCapture(event.pointerId);
    this.dragging = { pointerId: event.pointerId, block, x: block.x || 0, y: block.y || 0, clientX: event.clientX, clientY: event.clientY, width: Math.max(1, canvas.clientWidth), height: Math.max(1, canvas.clientHeight) };
  }
  dragBlock(event: PointerEvent) {
    const drag = this.dragging;
    if (!drag || drag.pointerId !== event.pointerId) return;
    drag.block.x = Math.round(Math.max(0, Math.min(100 - (drag.block.width || 49), drag.x + (event.clientX - drag.clientX) / drag.width * 100)));
    drag.block.y = Math.round(Math.max(0, Math.min(100 - (drag.block.height || 18), drag.y + (event.clientY - drag.clientY) / drag.height * 100)));
  }
  stopDrag(event: PointerEvent) { if (this.dragging?.pointerId === event.pointerId) this.dragging = null; }
}
