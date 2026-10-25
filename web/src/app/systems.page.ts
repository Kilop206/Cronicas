import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService, RpgSystem, SystemField, apiError } from './api';

@Component({ standalone: true, imports: [CommonModule, RouterLink], template: `
  <main class="page"><div class="page-heading"><div><span class="overline">REGRAS DO ARQUIVO</span><h1>Seus sistemas</h1><p>Crie a ficha livre ou desenhe campos e seções para a sua mesa.</p></div><a routerLink="/systems/new" class="button">Criar sistema</a></div>
    <div *ngIf="error()" class="notice error" role="alert">{{ error() }}</div><div *ngIf="loading()" class="loading">Carregando sistemas...</div>
    <div *ngIf="!loading() && !systems().length" class="empty-state"><h2>Nenhum sistema criado</h2><p>Comece com uma ficha livre ou modele uma ficha estruturada do seu jeito.</p><a routerLink="/systems/new" class="button">Criar meu sistema</a></div>
    <div class="system-grid"><a *ngFor="let system of systems()" [routerLink]="['/systems', system.id]" class="system-card"><span class="overline">{{ system.fields.length ? 'FICHA ESTRUTURADA' : 'FICHA LIVRE' }}</span><h2>{{ system.name }}</h2><p>{{ system.description || 'Sistema sem descrição.' }}</p><small>{{ system.fields.length }} campos · abrir →</small></a></div>
  </main>` })
export class SystemsPage implements OnInit {
  private api = inject(ApiService); systems = signal<RpgSystem[]>([]); loading = signal(true); error = signal('');
  ngOnInit() { this.api.systems().pipe(finalize(() => this.loading.set(false))).subscribe({ next: items => this.systems.set(items.filter(item => item.ownerId === this.api.user()?.id)), error: e => this.error.set(apiError(e)) }); }
}

@Component({ standalone: true, imports: [CommonModule, FormsModule, RouterLink], template: `
  <main class="page system-designer"><a routerLink="/systems" class="back-link">← Seus sistemas</a>
    <div class="page-heading"><div><span class="overline">ATELIÊ DE FICHAS</span><h1>{{ isNew ? 'Criar sistema' : name }}</h1><p>Desenhe a organização da ficha sem código executável. Cada campo é validado pela API.</p></div></div>
    <div *ngIf="error()" class="notice error" role="alert">{{ error() }}</div><div *ngIf="loading()" class="loading">Abrindo sistema...</div>
    <form *ngIf="!loading()" (ngSubmit)="save()" #form="ngForm" class="designer-layout">
      <section class="editor-panel designer-controls"><div class="panel-heading"><span>IDENTIDADE</span><h2>Definição do sistema</h2></div>
        <label>Nome<input name="name" [(ngModel)]="name" required minlength="3" maxlength="120" [disabled]="readonly()"></label>
        <label>Descrição<textarea name="description" [(ngModel)]="description" maxlength="2000" rows="3" [disabled]="readonly()"></textarea></label>
        <fieldset class="mode-choice" [disabled]="readonly()"><legend>Como criar a ficha</legend>
          <label><input type="radio" name="mode" [checked]="mode === 'free'" (change)="mode = 'free'"> Livre <small>Jogadores adicionam campos manualmente à própria ficha.</small></label>
          <label><input type="radio" name="mode" [checked]="mode === 'structured'" (change)="mode = 'structured'"> Estruturada <small>Você define campos tipados, seções e larguras.</small></label>
        </fieldset>
        <div *ngIf="mode === 'structured'" class="designer-fields"><div class="section-heading"><h2>Diagrama da ficha</h2><button *ngIf="!readonly()" type="button" class="text-link" (click)="addField()">Adicionar campo</button></div>
          <p class="muted">A ordem dos campos define a leitura. A largura controla a posição na grade de 12 colunas.</p>
          <div class="editor-switch" role="group" aria-label="Modo de edição"><button type="button" [class.active]="editorMode === 'visual'" (click)="setEditorMode('visual')">Editor visual</button><button type="button" [class.active]="editorMode === 'json'" (click)="setEditorMode('json')">Definição JSON</button></div>
          <div *ngIf="editorMode === 'visual'"><div *ngFor="let field of fields; let i = index" class="system-field-editor">
            <div class="system-field-head"><strong>{{ field.label || 'Novo campo' }}</strong><div *ngIf="!readonly()" class="field-actions"><button type="button" class="text-link" [disabled]="i === 0" (click)="moveField(i, -1)" aria-label="Mover campo para cima">↑</button><button type="button" class="text-link" [disabled]="i === fields.length - 1" (click)="moveField(i, 1)" aria-label="Mover campo para baixo">↓</button><button type="button" class="text-link danger" (click)="removeField(i)">Remover</button></div></div>
            <div class="system-field-grid"><label>Chave<input [name]="'key' + i" [(ngModel)]="field.key" required pattern="[a-z][a-z0-9_]{1,39}" [disabled]="readonly()"></label>
              <label>Rótulo<input [name]="'label' + i" [(ngModel)]="field.label" required minlength="2" maxlength="80" [disabled]="readonly()"></label>
              <label>Seção<input [name]="'section' + i" [(ngModel)]="field.section" required minlength="2" maxlength="60" [disabled]="readonly()"></label>
              <label>Tipo<select [name]="'type' + i" [(ngModel)]="field.type" [disabled]="readonly()"><option value="text">Texto</option><option value="longtext">Texto longo</option><option value="number">Número</option><option value="boolean">Sim/não</option><option value="select">Seleção</option></select></label>
              <label>Largura<select [name]="'span' + i" [(ngModel)]="field.span" [disabled]="readonly()"><option [ngValue]="3">¼ da linha</option><option [ngValue]="4">⅓ da linha</option><option [ngValue]="6">½ linha</option><option [ngValue]="12">Linha inteira</option></select></label>
              <label *ngIf="field.type === 'number'">Mínimo<input type="number" [name]="'min' + i" [(ngModel)]="field.min" [disabled]="readonly()"></label>
              <label *ngIf="field.type === 'number'">Máximo<input type="number" [name]="'max' + i" [(ngModel)]="field.max" [disabled]="readonly()"></label>
              <label *ngIf="field.type === 'select'" class="wide">Opções separadas por vírgula<input [name]="'options' + i" [ngModel]="field.options?.join(', ') || ''" (ngModelChange)="field.options = parseOptions($event)" [disabled]="readonly()"></label></div>
          </div></div>
          <label *ngIf="editorMode === 'json'">Campos em JSON<textarea name="json" [(ngModel)]="jsonDraft" rows="18" spellcheck="false" [disabled]="readonly()"></textarea><small>Use um array de campos com key, label, type, section, span, min, max e options. Nenhum script é executado.</small></label>
        </div>
        <p *ngIf="readonly()" class="room-note">Você pode consultar este sistema porque participa de uma campanha que o usa.</p>
        <div *ngIf="!readonly()" class="form-actions"><button *ngIf="!isNew" type="button" class="text-link danger" (click)="remove()" [disabled]="busy()">Excluir sistema</button><button class="button" [disabled]="form.invalid || busy() || mode === 'structured' && editorMode === 'visual' && !fields.length">{{ busy() ? 'Salvando...' : 'Salvar sistema' }}</button></div>
        <p *ngIf="saved()" class="form-success" role="status">Sistema salvo.</p>
      </section>
      <aside class="editor-panel designer-preview"><div class="panel-heading"><span>PRÉVIA</span><h2>Estrutura da ficha</h2></div><p *ngIf="mode === 'free'" class="room-note">Ficha livre: o jogador monta seus próprios campos. A identidade e os recursos sempre ficam acessíveis.</p>
        <ng-container *ngIf="mode === 'structured'"><div *ngFor="let section of sections()" class="preview-section"><h3>{{ section }}</h3><div class="preview-grid"><div *ngFor="let field of sectionFields(section)" class="preview-field" [style.grid-column]="'span ' + (field.span || 6)"><span>{{ field.label || field.key }}</span><div>{{ field.type === 'boolean' ? '☐' : field.type === 'longtext' ? 'Texto longo' : field.type === 'select' ? 'Selecione uma opção' : 'Valor' }}</div></div></div></div><p *ngIf="!fields.length" class="room-note">Adicione campos para desenhar a ficha.</p></ng-container>
      </aside>
    </form>
  </main>`, styles: [`
    .designer-layout { display: grid; grid-template-columns: minmax(0, 1.35fr) minmax(280px, .85fr); align-items: start; gap: 22px; }
    .designer-preview { position: sticky; top: 20px; }
    .mode-choice { border: 1px solid var(--border); margin: 20px 0; padding: 16px; }
    .mode-choice legend { color: var(--muted); font-size: 12px; }
    .mode-choice label { display: flex; align-items: flex-start; gap: 9px; margin: 8px 0; }
    .mode-choice label small { display: block; color: var(--muted); }
    .mode-choice input { width: auto; margin: 3px 0; }
    .editor-switch { display: flex; gap: 8px; margin: 14px 0; }
    .editor-switch button { border: 1px solid var(--border); background: transparent; color: var(--muted); padding: 8px 12px; cursor: pointer; }
    .editor-switch button.active { color: var(--gold-bright); border-color: var(--gold); }
    .field-actions { display: flex; gap: 12px; }
    .preview-section { border-top: 1px solid var(--border); padding-top: 14px; margin-top: 18px; }
    .preview-section h3 { font: 20px var(--font-editorial); margin: 0 0 12px; }
    .preview-grid { display: grid; grid-template-columns: repeat(12, minmax(0, 1fr)); gap: 9px; }
    .preview-field { min-width: 0; }
    .preview-field span { display: block; color: var(--muted); font-size: 11px; margin-bottom: 5px; overflow-wrap: anywhere; }
    .preview-field div { border: 1px solid var(--border); padding: 9px; font-size: 12px; color: var(--muted); }
    @media (max-width: 1000px) { .designer-layout { grid-template-columns: 1fr; } .designer-preview { position: static; } }
    @media (max-width: 620px) { .preview-field { grid-column: span 12 !important; } }
  `] })
export class SystemEditorPage implements OnInit {
  private api = inject(ApiService); private route = inject(ActivatedRoute); private router = inject(Router);
  id = this.route.snapshot.paramMap.get('id') || 'new'; isNew = this.id === 'new'; ownerId: string | null = null;
  name = ''; description = ''; mode: 'free' | 'structured' = 'free'; editorMode: 'visual' | 'json' = 'visual'; jsonDraft = '[]';
  fields: SystemField[] = []; loading = signal(!this.isNew); busy = signal(false); saved = signal(false); error = signal('');
  ngOnInit() { if (!this.isNew) this.api.system(this.id).pipe(finalize(() => this.loading.set(false))).subscribe({ next: system => { this.name = system.name; this.description = system.description; this.fields = system.fields.map(f => ({ ...f, span: f.span || 6, options: f.options?.slice() || null })); this.mode = this.fields.length ? 'structured' : 'free'; this.ownerId = system.ownerId; }, error: e => this.error.set(apiError(e)) }); }
  readonly() { return !this.isNew && this.ownerId !== this.api.user()?.id; }
  addField() { this.fields.push({ key: `campo_${this.fields.length + 1}`, label: '', section: 'Geral', type: 'text', min: null, max: null, options: null, span: 6 }); }
  removeField(index: number) { this.fields.splice(index, 1); }
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
    try { const parsed: unknown = JSON.parse(this.jsonDraft); if (!Array.isArray(parsed)) throw new Error(); this.fields = parsed as SystemField[]; this.error.set(''); return true; }
    catch { this.error.set('Definição JSON inválida. Informe um array de campos.'); return false; }
  }
  save() {
    this.error.set(''); this.saved.set(false);
    if (this.mode === 'structured' && this.editorMode === 'json' && !this.parseJson()) return;
    if (this.mode === 'structured' && !this.fields.length) { this.error.set('Adicione ao menos um campo ou escolha ficha livre.'); return; }
    this.busy.set(true);
    const fields = this.mode === 'free' ? [] : this.fields.map(f => ({ ...f, options: f.type === 'select' ? f.options : null, min: f.type === 'number' ? f.min : null, max: f.type === 'number' ? f.max : null }));
    const payload = { name: this.name, description: this.description, fields };
    const request = this.isNew ? this.api.createSystem(payload) : this.api.updateSystem(this.id, payload);
    request.pipe(finalize(() => this.busy.set(false))).subscribe({ next: system => { this.fields = system.fields; this.saved.set(true); this.ownerId = system.ownerId; this.id = system.id; this.isNew = false; void this.router.navigate(['/systems', system.id], { replaceUrl: true }); }, error: e => this.error.set(apiError(e)) });
  }
  remove() { if (!confirm('Excluir este sistema?')) return; this.busy.set(true); this.api.deleteSystem(this.id).pipe(finalize(() => this.busy.set(false))).subscribe({ next: () => void this.router.navigateByUrl('/systems'), error: e => this.error.set(apiError(e)) }); }
}
