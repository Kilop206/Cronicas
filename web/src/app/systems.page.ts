import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService, RpgSystem, SystemField, apiError } from './api';

@Component({ standalone: true, imports: [CommonModule, RouterLink], template: `
  <main class="page"><div class="page-heading"><div><span class="overline">REGRAS DO ARQUIVO</span><h1>Sistemas de RPG</h1><p>Modelos originais e sistemas criados por você.</p></div><a routerLink="/systems/new" class="button">+ Criar sistema</a></div>
    <div *ngIf="error()" class="notice error" role="alert">{{ error() }}</div><div *ngIf="loading()" class="loading">Carregando sistemas...</div>
    <div class="system-grid"><a *ngFor="let system of systems()" [routerLink]="['/systems', system.id]" class="system-card"><span class="overline">{{ system.ownerId ? 'SEU SISTEMA' : 'MODELO ORIGINAL' }}</span><h2>{{ system.name }}</h2><p>{{ system.description }}</p><small>{{ system.fields.length }} campos de ficha · abrir →</small></a></div>
  </main>` })
export class SystemsPage implements OnInit {
  private api = inject(ApiService); systems = signal<RpgSystem[]>([]); loading = signal(true); error = signal('');
  ngOnInit() { this.api.systems().pipe(finalize(() => this.loading.set(false))).subscribe({ next: items => this.systems.set(items), error: e => this.error.set(apiError(e)) }); }
}

@Component({ standalone: true, imports: [CommonModule, FormsModule, RouterLink], template: `
  <main class="page narrow"><a routerLink="/systems" class="back-link">← Biblioteca de sistemas</a>
    <div class="page-heading"><div><span class="overline">EDITOR DE SISTEMAS</span><h1>{{ isNew ? 'Novo sistema' : name }}</h1><p>Defina campos tipados; o servidor valida os valores das fichas.</p></div></div>
    <div *ngIf="error()" class="notice error" role="alert">{{ error() }}</div><div *ngIf="loading()" class="loading">Abrindo sistema...</div>
    <form *ngIf="!loading()" class="editor-panel" (ngSubmit)="save()" #form="ngForm"><div class="panel-heading"><span>DEFINIÇÃO</span><h2>{{ isNew ? 'Sua regra' : 'Estrutura da ficha' }}</h2></div>
      <label>Nome<input name="name" [(ngModel)]="name" required minlength="3" maxlength="120" [disabled]="readonly()"></label>
      <label>Descrição<textarea name="description" [(ngModel)]="description" maxlength="2000" rows="3" [disabled]="readonly()"></textarea></label>
      <div class="section-heading"><h2>Campos</h2><button *ngIf="!readonly()" type="button" class="text-link" (click)="addField()">+ Adicionar campo</button></div>
      <div *ngFor="let field of fields; let i = index" class="system-field-editor">
        <div class="system-field-head"><strong>{{ field.label || 'Novo campo' }}</strong><button *ngIf="!readonly()" type="button" class="text-link danger" (click)="removeField(i)">Remover</button></div>
        <div class="system-field-grid"><label>Chave<input [name]="'key' + i" [(ngModel)]="field.key" required pattern="[a-z][a-z0-9_]{1,39}" [disabled]="readonly()"></label>
          <label>Rótulo<input [name]="'label' + i" [(ngModel)]="field.label" required minlength="2" maxlength="80" [disabled]="readonly()"></label>
          <label>Seção<input [name]="'section' + i" [(ngModel)]="field.section" required minlength="2" maxlength="60" [disabled]="readonly()"></label>
          <label>Tipo<select [name]="'type' + i" [(ngModel)]="field.type" [disabled]="readonly()"><option value="text">Texto</option><option value="longtext">Texto longo</option><option value="number">Número</option><option value="boolean">Sim/não</option><option value="select">Seleção</option></select></label>
          <label *ngIf="field.type === 'number'">Mínimo<input type="number" [name]="'min' + i" [(ngModel)]="field.min" [disabled]="readonly()"></label>
          <label *ngIf="field.type === 'number'">Máximo<input type="number" [name]="'max' + i" [(ngModel)]="field.max" [disabled]="readonly()"></label>
          <label *ngIf="field.type === 'select'" class="wide">Opções separadas por vírgula<input [name]="'options' + i" [ngModel]="field.options?.join(', ') || ''" (ngModelChange)="field.options = parseOptions($event)" [disabled]="readonly()"></label></div>
      </div>
      <p *ngIf="readonly()" class="room-note">Modelos originais podem ser usados em campanhas. Crie um sistema próprio para personalizar campos.</p>
      <div *ngIf="!readonly()" class="form-actions"><button type="button" *ngIf="!isNew" class="text-link danger" (click)="remove()" [disabled]="busy()">Excluir sistema</button><button class="button" [disabled]="form.invalid || busy() || !fields.length">{{ busy() ? 'Salvando...' : 'Salvar sistema' }}</button></div>
      <p *ngIf="saved()" class="form-success" role="status">Sistema salvo.</p>
    </form>
  </main>` })
export class SystemEditorPage implements OnInit {
  private api = inject(ApiService); private route = inject(ActivatedRoute); private router = inject(Router);
  id = this.route.snapshot.paramMap.get('id') || 'new'; isNew = this.id === 'new'; ownerId: string | null = null;
  name = ''; description = ''; fields: SystemField[] = []; loading = signal(!this.isNew); busy = signal(false); saved = signal(false); error = signal('');
  ngOnInit() { if (!this.isNew) this.api.system(this.id).pipe(finalize(() => this.loading.set(false))).subscribe({ next: system => { this.name = system.name; this.description = system.description; this.fields = system.fields.map(f => ({ ...f, options: f.options?.slice() || null })); this.ownerId = system.ownerId; }, error: e => this.error.set(apiError(e)) }); }
  readonly() { return !this.isNew && this.ownerId !== this.api.user()?.id; }
  addField() { this.fields.push({ key: `campo_${this.fields.length + 1}`, label: '', section: 'Geral', type: 'text', min: null, max: null, options: null }); }
  removeField(index: number) { this.fields.splice(index, 1); }
  parseOptions(value: string) { return value.split(',').map(x => x.trim()).filter(Boolean); }
  save() {
    this.error.set(''); this.saved.set(false); this.busy.set(true);
    const payload = { name: this.name, description: this.description, fields: this.fields.map(f => ({ ...f, options: f.type === 'select' ? f.options : null, min: f.type === 'number' ? f.min : null, max: f.type === 'number' ? f.max : null })) };
    const request = this.isNew ? this.api.createSystem(payload) : this.api.updateSystem(this.id, payload);
    request.pipe(finalize(() => this.busy.set(false))).subscribe({ next: system => { this.saved.set(true); this.ownerId = system.ownerId; this.id = system.id; this.isNew = false; void this.router.navigate(['/systems', system.id], { replaceUrl: true }); }, error: e => this.error.set(apiError(e)) });
  }
  remove() { if (!confirm('Excluir este sistema?')) return; this.busy.set(true); this.api.deleteSystem(this.id).pipe(finalize(() => this.busy.set(false))).subscribe({ next: () => void this.router.navigateByUrl('/systems'), error: e => this.error.set(apiError(e)) }); }
}
