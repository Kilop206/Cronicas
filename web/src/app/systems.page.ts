import { CommonModule, DOCUMENT } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService, NpcTemplate, RpgSystem, SheetLayout, SystemField, SystemRevision, apiError } from './api';
import { SheetLayoutDesignerComponent, SheetLayoutPreviewComponent } from './sheet-layout-designer.component';
import { SHEET_TEMPLATES } from './system-templates';
import { canAssociateTemplate } from './sheet-renderer-registry';

const newSheetLayout = (): SheetLayout => ({ accentColor: '#aa9acd', skin: 'investigation', pages: [
  { id: 'principal', title: 'Principal', blocks: [
    { id: 'identidade', kind: 'identity', span: 8, title: 'Identidade', key: null, text: null, style: 'plain' },
    { id: 'retrato', kind: 'portrait', span: 4, title: 'Retrato', key: null, text: null, style: 'panel' }
  ] },
  { id: 'inventario', title: 'Inventário', blocks: [{ id: 'colecao_inventario', kind: 'inventory', span: 12, title: 'Inventário', key: null, text: null, style: 'plain' }] },
  { id: 'habilidades', title: 'Habilidades', blocks: [{ id: 'colecao_habilidades', kind: 'abilities', span: 12, title: 'Habilidades', key: null, text: null, style: 'plain' }] },
  { id: 'historico', title: 'Histórico', blocks: [{ id: 'colecao_historico', kind: 'history', span: 12, title: 'Histórico', key: null, text: null, style: 'plain' }] }
] });

function layoutFromFields(fields: SystemField[]): SheetLayout {
  const layout = newSheetLayout();
  for (const field of fields) layout.pages[0].blocks.push({ id: `campo_${field.key}`, kind: 'field', span: field.span || 6, title: field.label, key: field.key, text: null, style: 'panel' });
  return layout;
}

@Component({ standalone: true, imports: [CommonModule, RouterLink], template: `
  <main class="page"><div class="page-heading"><div><span class="overline">REGRAS DO ARQUIVO</span><h1>Seus sistemas</h1><p>Crie fichas livres ou componha páginas, campos e recursos para a sua mesa.</p></div><a routerLink="/systems/new" class="button">Criar sistema</a></div>
    <section class="template-catalog"><div class="section-heading"><div><span class="overline">PONTOS DE PARTIDA</span><h2>Modelos prontos para editar</h2><p>Escolha um sistema, ajuste os campos e salve sua própria versão. Modelos não oficiais com visual do Crônicas.</p></div></div>
      <div class="system-grid"><a *ngFor="let template of templates" [routerLink]="['/systems', 'new']" [queryParams]="{ preset: template.id }" class="system-card"><span class="overline">MODELO NÃO OFICIAL</span><h2>{{ template.name }}</h2><p>{{ template.summary }}</p><small>{{ template.fields.length }} campos · usar modelo →</small></a></div>
    </section>
    <div *ngIf="error()" class="notice error" role="alert">{{ error() }}</div><div *ngIf="loading()" class="loading">Carregando sistemas...</div>
    <div *ngIf="!loading() && !systems().length" class="empty-state"><h2>Nenhum sistema criado</h2><p>Comece com uma ficha livre ou modele uma ficha estruturada do seu jeito.</p><a routerLink="/systems/new" class="button">Criar meu sistema</a></div>
    <div class="system-grid"><a *ngFor="let system of systems()" [routerLink]="['/systems', system.id]" class="system-card"><span class="overline">{{ system.layout ? 'FICHA DIAGRAMADA' : system.fields.length ? 'FICHA ESTRUTURADA' : 'FICHA LIVRE' }}</span><h2>{{ system.name }}</h2><p>{{ system.description || 'Sistema sem descrição.' }}</p><small>{{ system.fields.length }} campos · abrir →</small></a></div>
  </main>`, styles: [`.template-catalog { margin: 30px 0 42px; } .template-catalog .section-heading { margin-bottom: 18px; }`] })
export class SystemsPage implements OnInit {
  private api = inject(ApiService); systems = signal<RpgSystem[]>([]); loading = signal(true); error = signal('');
  templates = SHEET_TEMPLATES;
  ngOnInit() { this.api.systems().pipe(finalize(() => this.loading.set(false))).subscribe({ next: items => this.systems.set(items.filter(item => item.ownerId === this.api.user()?.id)), error: e => this.error.set(apiError(e)) }); }
}

@Component({ standalone: true, imports: [CommonModule, FormsModule, RouterLink, SheetLayoutDesignerComponent, SheetLayoutPreviewComponent], template: `
  <main class="page system-designer"><a routerLink="/systems" class="back-link">← Seus sistemas</a>
    <div class="page-heading"><div><span class="overline">ATELIÊ DE FICHAS</span><h1>{{ isNew ? 'Criar sistema' : name }}</h1><p>Desenhe a organização da ficha sem código executável. Cada campo é validado pela API.</p></div></div>
    <div *ngIf="error()" class="notice error" role="alert">{{ error() }}</div><div *ngIf="loading()" class="loading">Abrindo sistema...</div>
    <form *ngIf="!loading()" (ngSubmit)="save()" #form="ngForm" class="designer-layout">
      <section class="editor-panel designer-controls"><div class="panel-heading"><span>IDENTIDADE</span><h2>Definição do sistema</h2></div>
        <label>Nome<input name="name" [(ngModel)]="name" required minlength="3" maxlength="120" [disabled]="readonly()"></label>
        <label>Descrição<textarea name="description" [(ngModel)]="description" maxlength="2000" rows="3" [disabled]="readonly()"></textarea></label>
        <label *ngIf="!isNew && !templateId && !readonly() && compatibleTemplates().length">Vincular ficha existente a um modelo<select name="associateTemplate" [ngModel]="''" (ngModelChange)="associateTemplate($event)"><option value="">Escolha um modelo</option><option *ngFor="let template of compatibleTemplates()" [value]="template.id">{{ template.name }}</option></select><small>Preserva campos e personagens; a ficha passa a usar a experiência do modelo após salvar.</small></label>
        <label *ngIf="templateId && mode === 'structured'">Apresentação da ficha<select name="rendererMode" [(ngModel)]="rendererMode" [disabled]="readonly()"><option value="specialized">Ficha especializada do sistema</option><option value="layout">Composição visual personalizada</option></select><small>Use a composição visual se quiser posicionar livremente todos os blocos.</small></label>
        <div *ngIf="latestRulesTemplate() as update" class="rules-upgrade"><strong>Regras disponíveis: {{ update.name }} · modelo {{ update.version }}</strong><p>O sistema usa o modelo {{ templateVersion }}. Atualize os campos e revise as alterações antes de salvar. Personagens existentes serão recalculados; o histórico permite consultar os valores anteriores.</p><p *ngIf="update.id === 'tormenta20'">Em Tormenta20, os valores manuais de perícias e Defesa passarão a ser calculados a partir dos atributos, nível e treino.</p><button type="button" class="button button-small secondary" (click)="prepareRulesUpgrade()">Preparar atualização de regras</button></div>
        <p *ngIf="rulesUpgradeNotice" class="form-success" role="status">{{ rulesUpgradeNotice }}</p>
        <label *ngIf="isNew">Ponto de partida<select name="preset" [ngModel]="preset" (ngModelChange)="applyPreset($event)"><option value="blank">Em branco</option><option value="fantasy">Aventura fantástica genérica</option><option value="mystery">Investigação genérica</option><option *ngFor="let template of templates" [value]="template.id">{{ template.name }} (não oficial)</option></select></label>
        <fieldset class="mode-choice" [disabled]="readonly()"><legend>Como criar a ficha</legend>
          <label [class.selected]="mode === 'free'"><input type="radio" name="mode" [checked]="mode === 'free'" (change)="mode = 'free'"><span><strong>Livre</strong><small>Jogadores adicionam campos manualmente à própria ficha.</small></span></label>
          <label [class.selected]="mode === 'structured'"><input type="radio" name="mode" [checked]="mode === 'structured'" (change)="mode = 'structured'"><span><strong>Diagramada</strong><small>Você define páginas, blocos, campos e recursos da ficha.</small></span></label>
        </fieldset>
        <div *ngIf="mode === 'structured'" class="designer-fields"><div class="section-heading designer-field-heading"><h2>Diagrama da ficha</h2><button *ngIf="!readonly() && matchingTemplate()" type="button" class="button button-small secondary" (click)="applyCatalogLayout()">Usar composição do modelo</button><button *ngIf="!readonly()" type="button" class="button" [disabled]="fields.length >= 100" (click)="addField()">Adicionar campo</button></div>
          <p class="muted">Defina os dados aqui e posicione cada campo no compositor de páginas abaixo.</p>
          <div class="editor-switch" role="group" aria-label="Modo de edição"><button type="button" [class.active]="editorMode === 'visual'" (click)="setEditorMode('visual')">Editor visual</button><button type="button" [class.active]="editorMode === 'json'" (click)="setEditorMode('json')">Definição JSON</button></div>
          <div *ngIf="editorMode === 'visual'"><div *ngFor="let field of fields; let i = index" class="system-field-editor">
            <div class="system-field-head"><strong>{{ field.label || 'Novo campo' }}</strong><div *ngIf="!readonly()" class="field-actions"><button type="button" class="text-link" [disabled]="i === 0" (click)="moveField(i, -1)" aria-label="Mover campo para cima">↑</button><button type="button" class="text-link" [disabled]="i === fields.length - 1" (click)="moveField(i, 1)" aria-label="Mover campo para baixo">↓</button><button type="button" class="text-link danger" (click)="removeField(i)">Remover</button></div></div>
            <div class="system-field-grid"><label>Chave<input [name]="'key' + i" [ngModel]="field.key" (ngModelChange)="renameField(field, $event)" required pattern="[a-z][a-z0-9_]{1,39}" [disabled]="readonly()"></label>
              <label>Rótulo<input [name]="'label' + i" [(ngModel)]="field.label" required minlength="2" maxlength="80" [disabled]="readonly()"></label>
              <label>Seção<input [name]="'section' + i" [(ngModel)]="field.section" required minlength="2" maxlength="60" [disabled]="readonly()"></label>
              <label>Tipo<select [name]="'type' + i" [ngModel]="field.type" (ngModelChange)="setFieldType(field, $event)" [disabled]="readonly()"><option value="text">Texto</option><option value="longtext">Texto longo</option><option value="number">Número</option><option value="boolean">Sim/não</option><option value="select">Seleção</option><option value="list">Lista repetível</option><option value="table">Tabela repetível</option></select></label>
              <label>Largura<select [name]="'span' + i" [(ngModel)]="field.span" [disabled]="readonly()"><option [ngValue]="3">¼ da linha</option><option [ngValue]="4">⅓ da linha</option><option [ngValue]="6">½ linha</option><option [ngValue]="12">Linha inteira</option></select></label>
              <label>Permissão de edição<select [name]="'editRole' + i" [ngModel]="field.editRole || 'both'" (ngModelChange)="field.editRole = $event" [disabled]="readonly()"><option value="both">Dono e mestre</option><option value="gm">Somente mestre</option></select></label>
              <label *ngIf="field.type === 'number'">Mínimo<input type="number" [name]="'min' + i" [(ngModel)]="field.min" [disabled]="readonly()"></label>
              <label *ngIf="field.type === 'number'">Máximo<input type="number" [name]="'max' + i" [(ngModel)]="field.max" [disabled]="readonly()"></label>
              <label *ngIf="field.type === 'number'">Valor inicial<input type="number" [name]="'default' + i" [ngModel]="field.defaultValue" (ngModelChange)="field.defaultValue = $event === '' || $event == null ? null : +$event" [disabled]="readonly() || !!field.formula"></label>
              <label *ngIf="field.type === 'text'">Valor inicial<input [name]="'default' + i" [(ngModel)]="field.defaultValue" maxlength="300" [disabled]="readonly()"></label>
              <label *ngIf="field.type === 'boolean'" class="checkbox-field"><input type="checkbox" [name]="'default' + i" [ngModel]="field.defaultValue === true" (ngModelChange)="field.defaultValue = $event" [disabled]="readonly()"> Começa marcado</label>
              <label *ngIf="field.type === 'number'" class="wide">Cálculo automático<input [name]="'formula' + i" [(ngModel)]="field.formula" maxlength="200" placeholder="nivel * 100 + experiencia" [disabled]="readonly()"><small>Use chaves de outros campos numéricos, +, -, *, / e parênteses. O resultado fica somente para leitura.</small></label>
              <label *ngIf="field.type === 'number'" class="wide">Rolagem do campo<input [name]="'roll' + i" [(ngModel)]="field.rollExpression" maxlength="80" placeholder="1d20+&#123;bonus&#125;" [disabled]="readonly()"><small>Ex.: 1d20+3 ou 1d20+&#123;bonus&#125;. O resultado aparece na campanha.</small></label>
              <label *ngIf="field.type === 'select'" class="wide">Opções separadas por vírgula<input [name]="'options' + i" [ngModel]="field.options?.join(', ') || ''" (ngModelChange)="field.options = parseOptions($event)" [disabled]="readonly()"></label></div>
              <label *ngIf="field.type === 'table'">Colunas da tabela<input [name]="'columns' + i" [ngModel]="columnLabels(field)" (ngModelChange)="setColumns(field,$event)" placeholder="Nome, Tipo, Custo" [disabled]="readonly()"><small>Separe os títulos por vírgula. Até 8 colunas; cada ficha pode adicionar até 30 linhas.</small></label>
              <div *ngIf="field.type === 'table'" class="system-field-grid"><label *ngFor="let column of field.columns; let c=index">{{ column.label }}<select [name]="'columnType' + i + '_' + c" [(ngModel)]="column.type" [disabled]="readonly()"><option value="text">Texto</option><option value="number">Número</option><option value="boolean">Sim/não</option><option value="dice">Rolagem de dados</option></select></label></div>
          </div></div>
          <label *ngIf="editorMode === 'json'">Campos em JSON<textarea name="json" [(ngModel)]="jsonDraft" rows="18" spellcheck="false" [disabled]="readonly()"></textarea><small>Use um array de campos com key, label, type, section, span, min, max, options, defaultValue, formula e rollExpression. Nenhum script é executado.</small></label>
          <app-sheet-layout-designer [layout]="layout" [fields]="fields" [readOnly]="readonly()" (layoutChange)="layout = $event" (pageSelected)="selectedPreviewPage = $event" />
        </div>
        <details *ngIf="!readonly()" class="layout-json"><summary>Modelos de NPC simplificado · {{ npcTemplates.length }}</summary><p>O mestre pode preencher um NPC a partir destes modelos. O conteúdo do modelo é visível aos participantes do sistema; segredos são escritos apenas no NPC.</p><button type="button" class="button button-small secondary" [disabled]="npcTemplates.length >= 12" (click)="addNpcTemplate()">Adicionar modelo de NPC</button>
          <div *ngFor="let npc of npcTemplates; let n=index" class="editor-panel"><label>Nome do modelo<input [name]="'npcName' + n" [(ngModel)]="npc.name" required minlength="2" maxlength="80"></label><label>Aparência<textarea [name]="'npcAppearance' + n" [(ngModel)]="npc.appearance" maxlength="2000" rows="2"></textarea></label><label>Personalidade<textarea [name]="'npcPersonality' + n" [(ngModel)]="npc.personality" maxlength="2000" rows="2"></textarea></label><label>Motivação<textarea [name]="'npcMotivation' + n" [(ngModel)]="npc.motivation" maxlength="2000" rows="2"></textarea></label><label>Relações<textarea [name]="'npcRelations' + n" [(ngModel)]="npc.relationships" maxlength="2000" rows="2"></textarea></label><label>Localização<input [name]="'npcLocation' + n" [(ngModel)]="npc.location" maxlength="160"></label><div class="resource-fields"><label>Vida inicial<input type="number" [name]="'npcHealth' + n" [(ngModel)]="npc.health" min="0" [max]="npc.maxHealth"></label><label>Vida máxima<input type="number" [name]="'npcMaxHealth' + n" [(ngModel)]="npc.maxHealth" min="1" max="9999"></label></div><label>Visibilidade<select [name]="'npcVisibility' + n" [(ngModel)]="npc.visibility"><option value="gm">Somente mestres</option><option value="campaign">Campanha</option></select></label><button type="button" class="text-link danger" (click)="removeNpcTemplate(n)">Remover modelo</button></div>
        </details>
        <p *ngIf="readonly()" class="room-note">Você pode consultar este sistema porque participa de uma campanha que o usa.</p>
        <div *ngIf="!readonly()" class="form-actions"><button type="button" class="button button-small secondary" (click)="exportTemplate()">Exportar modelo JSON</button><label class="button button-small secondary" style="cursor:pointer">Importar modelo JSON<input type="file" accept="application/json,.json" (change)="importTemplate($event)" hidden></label></div>
        <details *ngIf="!isNew && revisions.length" class="layout-json"><summary>Histórico do modelo · revisão {{ revision }}</summary><div *ngFor="let item of revisions" class="sheet-read-row"><span>Revisão {{ item.revision }} · {{ item.name }} · {{ item.savedAt | date:'short' }}</span><button type="button" class="button button-small secondary" [disabled]="busy() || readonly()" (click)="restoreRevision(item.revision)">Restaurar</button></div></details>
        <div *ngIf="!readonly()" class="form-actions"><button *ngIf="!isNew" type="button" class="text-link danger" (click)="remove()" [disabled]="busy()">Excluir sistema</button><button class="button" [disabled]="form.invalid || busy()">{{ busy() ? 'Salvando...' : 'Salvar sistema' }}</button></div>
        <p *ngIf="saved()" class="form-success" role="status">Sistema salvo.</p>
      </section>
      <aside class="editor-panel designer-preview"><div class="panel-heading"><span>PRÉVIA</span><h2>Ficha montada</h2></div>
        <p *ngIf="mode === 'free'" class="room-note">Ficha livre: cada jogador adiciona seus próprios campos.</p>
        <app-sheet-layout-preview *ngIf="mode === 'structured'" [layout]="layout" [fields]="fields" [readOnly]="readonly()" [pageId]="selectedPreviewPage" />
      </aside>
    </form>
  </main>`, styles: [`
    .designer-layout { display: grid; grid-template-columns: minmax(0, 1.35fr) minmax(320px, .85fr); align-items: start; gap: 22px; }
    .designer-controls, .designer-preview { min-width: 0; }
    .designer-preview { position: sticky; top: 20px; }
    .designer-field-heading { align-items: center; flex-wrap: wrap; gap: 12px; }
    .designer-field-heading h2 { margin: 0; }
    .designer-field-heading .button { flex: 0 0 auto; }
    .mode-choice { border: 0; margin: 20px 0; padding: 0; display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; }
    .mode-choice legend { color: var(--muted); font-size: 12px; }
    .mode-choice label { display: flex; align-items: flex-start; gap: 12px; margin: 8px 0 0; padding: 14px; border: 1px solid var(--border); border-radius: 5px; cursor: pointer; }
    .mode-choice label.selected { border-color: var(--gold); background: color-mix(in srgb, var(--gold) 8%, var(--surface)); }
    .mode-choice label span { display: grid; gap: 5px; }
    .mode-choice label small { display: block; color: var(--muted); font-weight: 400; line-height: 1.4; }
    .mode-choice input { margin-top: 1px; }
    .editor-switch { display: flex; gap: 8px; margin: 14px 0; }
    .editor-switch button { border: 1px solid var(--border); background: transparent; color: var(--muted); padding: 8px 12px; cursor: pointer; }
    .editor-switch button.active { color: var(--gold-bright); border-color: var(--gold); }
    .field-actions { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 8px; }
    .system-field-head { gap: 12px; }
    .system-field-head strong { min-width: 0; overflow-wrap: anywhere; }
    .rules-upgrade { margin: 15px 0; padding: 12px; border: 1px solid var(--gold); border-radius: 5px; background: color-mix(in srgb, var(--gold) 8%, var(--surface)); }
    .rules-upgrade p { margin: 7px 0; color: var(--muted); font-size: 13px; line-height: 1.4; }
    @media (max-width: 1320px) { .designer-layout { grid-template-columns: 1fr; } .designer-preview { position: static; } }
    @media (max-width: 620px) { .mode-choice { grid-template-columns: 1fr; } }
  `] })
export class SystemEditorPage implements OnInit {
  private api = inject(ApiService); private route = inject(ActivatedRoute); private router = inject(Router); private document = inject(DOCUMENT);
  id = this.route.snapshot.paramMap.get('id') || 'new'; isNew = this.id === 'new'; ownerId: string | null = null;
  templates = SHEET_TEMPLATES;
  name = ''; description = ''; preset = 'blank'; mode: 'free' | 'structured' = 'structured'; editorMode: 'visual' | 'json' = 'visual'; jsonDraft = '[]';
  templateId: string | null = null; templateVersion = 1; rulesetVersion: string | null = null;
  rulesUpgradeNotice = '';
  npcTemplates: NpcTemplate[] = [];
  rendererMode: 'specialized' | 'layout' = 'specialized'; revision = 1; revisions: SystemRevision[] = [];
  private originalFieldKeys = new Set<string>(); private fieldRenames: Record<string, string> = {};
  layout = newSheetLayout();
  selectedPreviewPage = 'principal';
  fields: SystemField[] = []; loading = signal(!this.isNew); busy = signal(false); saved = signal(false); error = signal('');
  ngOnInit() { if (this.isNew) { const preset = this.route.snapshot.queryParamMap.get('preset'); if (preset) this.applyPreset(preset); return; } this.api.system(this.id).pipe(finalize(() => this.loading.set(false))).subscribe({ next: system => this.useSystem(system), error: e => this.error.set(apiError(e)) }); }
  private useSystem(system: RpgSystem) { this.name = system.name; this.description = system.description; this.fields = system.fields.map(f => ({ ...f, span: f.span || 6, options: f.options?.slice() || null })); this.layout = system.layout || layoutFromFields(this.fields); this.selectedPreviewPage = this.layout.pages[0].id; this.mode = (system.layout || this.fields.length) ? 'structured' : 'free'; this.ownerId = system.ownerId; this.templateId = system.templateId; this.templateVersion = system.templateVersion || 1; this.rulesetVersion = system.rulesetVersion; this.rendererMode = system.rendererMode || 'specialized'; this.revision = system.revision || 1; this.revisions = system.revisions || []; this.npcTemplates = structuredClone(system.npcTemplates || []); this.originalFieldKeys = new Set(system.fields.map(field => field.key)); this.fieldRenames = {}; this.rulesUpgradeNotice = ''; }
  addNpcTemplate() { if (this.npcTemplates.length >= 12) return; let number = 1; while (this.npcTemplates.some(item => item.id === `npc_${number}`)) number++; this.npcTemplates.push({ id: `npc_${number}`, name: 'Novo modelo', appearance: '', personality: '', motivation: '', relationships: '', location: '', status: 'active', visibility: 'gm', health: 10, maxHealth: 10 }); }
  removeNpcTemplate(index: number) { this.npcTemplates.splice(index, 1); }
  readonly() { return !this.isNew && this.ownerId !== this.api.user()?.id; }
  matchingTemplate() { return this.templates.find(template => template.id === this.templateId && template.version === this.templateVersion) || null; }
  latestRulesTemplate() { if (this.isNew || this.readonly() || this.mode !== 'structured' || this.templateId === 'dnd_5e') return null; return this.templates.find(template => template.id === this.templateId && template.version > this.templateVersion) || null; }
  prepareRulesUpgrade() {
    const template = this.latestRulesTemplate();
    if (!template) return;
    const conflict = template.fields.find(next => this.fields.some(current => current.key === next.key && current.type !== next.type));
    if (conflict) { this.error.set(`O campo ${conflict.label} tem outro tipo. Ajuste o campo antes de atualizar.`); return; }
    const missing = template.fields.filter(next => !this.fields.some(current => current.key === next.key));
    if (this.fields.length + missing.length > 100) { this.error.set('Remova campos antes de atualizar: o limite é de 100.'); return; }
    const byKey = new Map(template.fields.map(field => [field.key, field]));
    this.fields = [...this.fields.map(current => {
      if (template.id !== 'tormenta20' || current.key !== 'defesa' && !current.key.startsWith('per_')) return current;
      const next = byKey.get(current.key);
      return next ? { ...current, formula: next.formula, rollExpression: next.rollExpression } : current;
    }), ...structuredClone(missing)];
    this.templateVersion = template.version;
    this.rulesetVersion = template.rulesetVersion;
    this.rulesUpgradeNotice = `Atualização para o modelo ${template.version} preparada. Revise os campos e clique em Salvar sistema para aplicar às fichas da campanha.`;
    this.error.set(''); this.saved.set(false);
  }
  compatibleTemplates() { return this.templates.filter(template => canAssociateTemplate(template.id, this.fields)); }
  private mergeTemplateFields(template: (typeof SHEET_TEMPLATES)[number]) {
    const missing = template.fields.filter(field => !this.fields.some(current => current.key === field.key));
    if (this.fields.length + missing.length > 100) { this.error.set('Remova campos antes de aplicar este modelo: o limite é de 100.'); return false; }
    this.fields.push(...structuredClone(missing)); return true;
  }
  associateTemplate(id: string) { const template = this.compatibleTemplates().find(item => item.id === id); if (!template || !this.mergeTemplateFields(template)) return; this.templateId = template.id; this.templateVersion = template.version; this.rulesetVersion = template.rulesetVersion; this.rendererMode = 'specialized'; this.saved.set(false); }
  applyCatalogLayout() { const template = this.matchingTemplate(); if (!template || !this.mergeTemplateFields(template)) return; this.layout = structuredClone(template.layout); this.selectedPreviewPage = this.layout.pages[0].id; this.saved.set(false); }
  setFieldType(field: SystemField, type: SystemField['type']) { field.type = type; field.defaultValue = null; field.columns = type === 'table' ? [{ key: 'nome', label: 'Nome', type: 'text' }] : null; if (type !== 'number') { field.formula = null; field.rollExpression = null; } }
  columnLabels(field: SystemField) { return field.columns?.map(column => column.label).join(', ') || ''; }
  setColumns(field: SystemField, value: string) { const names = value.split(',').map(item => item.trim()).filter(Boolean).slice(0, 8); const previous = field.columns || []; const keys = new Set<string>(); field.columns = names.map((label, index) => { let key = previous[index]?.key || label.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, '').slice(0, 36); if (!/^[a-z][a-z0-9_]{1,39}$/.test(key)) key = `coluna_${index + 1}`; while (keys.has(key)) key = `${key.slice(0, 35)}_${index + 1}`; keys.add(key); return { key, label, type: previous[index]?.type || 'text' as const }; }); }
  applyPreset(value: string) {
    this.preset = value;
    const template = this.templates.find(item => item.id === value);
    if (template) {
      this.templateId = template.id; this.templateVersion = template.version; this.rulesetVersion = template.rulesetVersion; this.rendererMode = 'specialized';
      this.name = template.name;
      this.description = `Modelo não oficial para ${template.name}. Personalize regras e campos para sua mesa.`;
      this.fields = structuredClone(template.fields);
      this.layout = structuredClone(template.layout);
      this.mode = 'structured';
      this.selectedPreviewPage = this.layout.pages[0].id;
      return;
    }
    this.templateId = null; this.templateVersion = 1; this.rulesetVersion = null; this.rendererMode = 'layout';
    if (value === 'blank') { this.fields = []; this.layout = newSheetLayout(); return; }
    const basic = (key: string, label: string, section: string, formula: string | null = null, rollExpression: string | null = null): SystemField =>
      ({ key, label, section, type: 'number', min: null, max: null, options: null, span: 6, formula, rollExpression });
    this.fields = value === 'fantasy' ? [
      basic('nivel', 'Nível', 'Progressão'), basic('experiencia', 'Experiência', 'Progressão'),
      basic('proximo_marco', 'Próximo marco', 'Progressão', 'nivel * 100'),
      basic('atributo', 'Atributo', 'Aptidões'), basic('bonus', 'Bônus', 'Aptidões', 'atributo + nivel', '1d20+{bonus}')
    ] : [
      basic('capitulo', 'Capítulo', 'Progressão'), basic('pistas', 'Pistas descobertas', 'Progressão'),
      basic('reputacao', 'Reputação', 'Progressão', 'capitulo + pistas'),
      basic('percepcao', 'Percepção', 'Aptidões', null, '1d20+{percepcao}')
    ];
    this.layout = layoutFromFields(this.fields); this.mode = 'structured'; this.selectedPreviewPage = this.layout.pages[0].id;
  }
  addField() { if (this.fields.length >= 100) return; let number = 1; while (this.fields.some(field => field.key === `campo_${number}`)) number++; const key = `campo_${number}`; this.fields.push({ key, label: '', section: 'Geral', type: 'text', min: null, max: null, options: null, span: 6 }); const page = this.layout.pages.find(item => item.blocks.length < 40); if (page && this.layout.pages.reduce((count, item) => count + item.blocks.length, 0) < 100) { const index = page.blocks.length; page.blocks.push({ id: `bloco_${key}`, kind: 'field', span: 6, title: null, key, text: null, style: 'panel', ...(page.layoutMode === 'canvas' ? { x: (index % 2) * 51, y: Math.min(Math.floor(index / 2) * 20, 80), width: 49, height: 18 } : {}) }); } }
  renameField(field: SystemField, key: string) {
    const previous = field.key; field.key = key;
    const origin = Object.keys(this.fieldRenames).find(from => this.fieldRenames[from] === previous) || previous;
    if (this.originalFieldKeys.has(origin)) {
      if (origin === key) delete this.fieldRenames[origin]; else this.fieldRenames[origin] = key;
    }
    for (const item of this.fields) {
      if (item.formula) item.formula = item.formula.replace(/\b[a-z][a-z0-9_]*\b/g, token => token === previous ? key : token);
      if (item.rollExpression) item.rollExpression = item.rollExpression.replaceAll(`{${previous}}`, `{${key}}`);
    }
    for (const page of this.layout.pages) for (const block of page.blocks) {
      if ((block.kind === 'field' || block.kind === 'resource' || block.kind === 'tracker') && block.key === previous) block.key = key;
      if ((block.kind === 'resource' || block.kind === 'tracker') && block.maxKey === previous) block.maxKey = key;
      if (block.visibleWhenKey === previous) block.visibleWhenKey = key;
    }
  }
  removeField(index: number) { const [field] = this.fields.splice(index, 1); for (const [from, to] of Object.entries(this.fieldRenames)) if (to === field.key) delete this.fieldRenames[from]; for (const page of this.layout.pages) { page.blocks = page.blocks.filter(block => !(['field', 'resource', 'tracker'].includes(block.kind) && block.key === field.key)); for (const block of page.blocks) { if (block.visibleWhenKey === field.key) { block.visibleWhenKey = null; block.visibleWhenValue = null; } if (block.maxKey === field.key) block.maxKey = null; } } }
  moveField(index: number, step: number) { const [item] = this.fields.splice(index, 1); this.fields.splice(index + step, 0, item); }
  sections() { return [...new Set(this.fields.map(field => field.section || 'Geral'))]; }
  sectionFields(section: string) { return this.fields.filter(field => (field.section || 'Geral') === section); }
  parseOptions(value: string) { return value.split(',').map(x => x.trim()).filter(Boolean); }
  setEditorMode(value: 'visual' | 'json') {
    if (value === this.editorMode) return;
    if (value === 'json') this.jsonDraft = JSON.stringify(this.fields, null, 2);
    else if (!this.parseJson()) return;
    this.editorMode = value;
  }
  private parseJson() {
    try {
      const parsed: unknown = JSON.parse(this.jsonDraft);
      if (!Array.isArray(parsed) || parsed.length > 100 || !parsed.every(field => field && typeof field === 'object'
        && typeof field.key === 'string' && typeof field.label === 'string' && typeof field.section === 'string'
        && ['text', 'longtext', 'number', 'boolean', 'select', 'list', 'table'].includes(field.type)
        && (field.options == null || Array.isArray(field.options))
        && (field.formula == null || typeof field.formula === 'string')
        && (field.rollExpression == null || typeof field.rollExpression === 'string')
        && (field.defaultValue == null || ['string', 'number', 'boolean'].includes(typeof field.defaultValue) || Array.isArray(field.defaultValue)))) throw new Error();
      this.fields = parsed as SystemField[]; this.error.set(''); return true;
    } catch { this.error.set('Definição JSON inválida. Informe um array de até 100 campos tipados.'); return false; }
  }
  save() {
    this.error.set(''); this.saved.set(false);
    if (this.mode === 'structured' && this.editorMode === 'json' && !this.parseJson()) return;
    this.busy.set(true);
    const fields = this.mode === 'free' ? [] : this.fields.map(f => ({ ...f, options: f.type === 'select' ? f.options : null, min: f.type === 'number' ? f.min : null, max: f.type === 'number' ? f.max : null, columns: f.type === 'table' ? f.columns : null, defaultValue: f.formula ? null : f.defaultValue ?? null }));
    const payload = { name: this.name, description: this.description, fields, layout: this.mode === 'structured' ? this.layout : null, templateId: this.mode === 'free' ? null : this.templateId, templateVersion: this.templateVersion, rulesetVersion: this.rulesetVersion, rendererMode: this.rendererMode, revision: this.revision, fieldRenames: this.fieldRenames, npcTemplates: this.npcTemplates };
    const request = this.isNew ? this.api.createSystem(payload) : this.api.updateSystem(this.id, payload);
    request.pipe(finalize(() => this.busy.set(false))).subscribe({ next: system => { this.useSystem(system); this.saved.set(true); this.id = system.id; this.isNew = false; void this.router.navigate(['/systems', system.id], { replaceUrl: true }); }, error: e => this.error.set(apiError(e)) });
  }
  exportTemplate() {
    const payload = { format: 'cronicas.sheet-template', formatVersion: 1, name: this.name, description: this.description,
      templateId: this.templateId, templateVersion: this.templateVersion, rulesetVersion: this.rulesetVersion,
      rendererMode: this.rendererMode, fields: this.mode === 'free' ? [] : this.fields, npcTemplates: this.npcTemplates,
      layout: this.mode === 'free' ? null : this.layout };
    const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const link = this.document.createElement('a'); link.href = url;
    link.download = `${(this.name || 'modelo').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().replace(/[^a-z0-9]+/g, '-')}.json`;
    link.click(); URL.revokeObjectURL(url);
  }
  async importTemplate(event: Event) {
    const input = event.target as HTMLInputElement; const file = input.files?.[0]; input.value = '';
    if (!file) return;
    if (file.size > 100_000) { this.error.set('O arquivo excede 100 KB.'); return; }
    try {
      const value: unknown = JSON.parse(await file.text());
      if (!value || typeof value !== 'object') throw new Error();
      const model = value as Record<string, unknown>;
      if (model['format'] !== 'cronicas.sheet-template' || model['formatVersion'] !== 1
        || typeof model['name'] !== 'string' || typeof model['description'] !== 'string'
        || !Array.isArray(model['fields']) || model['fields'].length > 100
        || !model['fields'].every(field => field && typeof field.key === 'string' && /^[a-z][a-z0-9_]{1,39}$/.test(field.key)
          && typeof field.label === 'string' && typeof field.section === 'string'
          && ['text','longtext','number','boolean','select','list','table'].includes(field.type))) throw new Error();
      const layout = model['layout'] as SheetLayout | null;
      if (layout && (!/^#[0-9a-fA-F]{6}$/.test(layout.accentColor) || !Array.isArray(layout.pages)
        || layout.pages.length < 1 || layout.pages.length > 8
        || !layout.pages.every(page => page && typeof page.id === 'string' && /^[a-z][a-z0-9_]{1,39}$/.test(page.id)
          && typeof page.title === 'string' && Array.isArray(page.blocks) && page.blocks.length >= 1 && page.blocks.length <= 40
          && page.blocks.every(block => block && typeof block.id === 'string' && typeof block.kind === 'string'
            && typeof block.span === 'number' && block.span >= 1 && block.span <= 12)))) throw new Error();
      this.name = model['name']; this.description = model['description']; this.fields = model['fields'] as SystemField[];
      this.fieldRenames = {};
      this.layout = layout || newSheetLayout(); this.mode = layout || this.fields.length ? 'structured' : 'free';
      this.selectedPreviewPage = this.layout.pages[0].id;
      this.templateId = typeof model['templateId'] === 'string' && canAssociateTemplate(model['templateId'], this.fields) ? model['templateId'] : null;
      this.templateVersion = typeof model['templateVersion'] === 'number' ? model['templateVersion'] : 1;
      this.rulesetVersion = typeof model['rulesetVersion'] === 'string' ? model['rulesetVersion'] : null;
      this.rendererMode = model['rendererMode'] === 'specialized' && this.templateId ? 'specialized' : 'layout';
      this.npcTemplates = Array.isArray(model['npcTemplates']) && model['npcTemplates'].length <= 12 ? model['npcTemplates'] as NpcTemplate[] : [];
      this.editorMode = 'visual'; this.saved.set(false); this.error.set('');
    } catch { this.error.set('Arquivo de modelo inválido. Importe um JSON exportado pelo Crônicas.'); }
  }
  restoreRevision(revision: number) {
    if (this.isNew || this.readonly()) return;
    this.busy.set(true); this.error.set('');
    this.api.restoreSystemRevision(this.id, revision, this.revision).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: system => { this.useSystem(system); this.saved.set(true); }, error: e => this.error.set(apiError(e))
    });
  }
  remove() { if (!confirm('Excluir este sistema?')) return; this.busy.set(true); this.api.deleteSystem(this.id).pipe(finalize(() => this.busy.set(false))).subscribe({ next: () => void this.router.navigateByUrl('/systems'), error: e => this.error.set(apiError(e)) }); }
}
