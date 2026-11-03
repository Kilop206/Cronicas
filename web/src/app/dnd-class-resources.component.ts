import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription, finalize } from 'rxjs';
import { ApiService, Character, RpgSystem, apiError } from './api';

interface ClassResource { id: string; label: string; current: number; maximum: number | null; recharge: string; detail: string | null; }
interface ClassView { resources?: ClassResource[]; actions: {id: string; options?: {value: string; label: string}[]}[]; state: Record<string, number>; }

@Component({selector: 'app-dnd-class-resources', standalone: true, imports: [CommonModule, FormsModule], template: `
  <section *ngIf="view()" class="class-resources" aria-label="Recursos de classe">
    <h2>RECURSOS DE CLASSE <small>2014</small></h2>
    <details *ngIf="mode !== 'print' && character.canEdit" [open]="!resources().length"><summary>Configurar níveis por classe</summary><p>Defina quantos níveis o personagem tem em cada classe para calcular seus recursos e sua multiclasse.</p><div class="class-utilities">
      <label>Classe<select aria-label="Classe para recursos" [ngModel]="classKey" (ngModelChange)="selectClass($event)" [ngModelOptions]="{standalone:true}" [disabled]="busy()"><option *ngFor="let option of classes()" [value]="option.value">{{ option.label }}</option></select></label>
      <label>Níveis<input type="number" aria-label="Níveis na classe selecionada" [(ngModel)]="levelDraft" [ngModelOptions]="{standalone:true}" min="0" max="20" [disabled]="busy()"></label>
      <button type="button" [disabled]="busy() || !ready || !classKey" (click)="execute('configure', levelDraft, classKey)">Salvar níveis</button>
    </div></details>
    <div class="resource-grid"><article *ngFor="let resource of resources()" [attr.data-resource]="resource.id">
      <div class="resource-title"><strong>{{ resource.label }}</strong><b>{{ resource.maximum === null ? '∞' : resource.current + ' / ' + resource.maximum }}</b></div>
      <small>{{ resource.recharge }}</small><p *ngIf="resource.detail">{{ resource.detail }}</p>
      <div *ngIf="mode !== 'print' && character.canEdit" class="resource-controls">
        <label *ngIf="variable(resource.id)">Quantidade<input type="number" [attr.aria-label]="'Quantidade de ' + resource.label" [(ngModel)]="amounts[resource.id]" [ngModelOptions]="{standalone:true}" min="1" [max]="resource.current" [disabled]="busy()"></label>
        <label *ngIf="resource.id === 'indomavel'">Resistência falha<select aria-label="Resistência para Indomável" [(ngModel)]="saveKey" [ngModelOptions]="{standalone:true}" [disabled]="busy()"><option *ngFor="let field of saves()" [value]="field.key">{{ field.label }}</option></select></label>
        <button type="button" [attr.aria-label]="'Usar ' + resource.label" [disabled]="busy() || !ready || resource.maximum !== null && resource.current === 0 || resource.id === 'indomavel' && !saveKey || resource.id === 'surto' && !!view()?.state?.['surto_turno']" (click)="use(resource)">Usar</button>
        <button *ngIf="resource.id === 'cura_maos'" type="button" [disabled]="busy() || !ready || resource.current === 0" (click)="execute('lay-hands-self', amounts[resource.id])">Curar a si</button>
      </div>
    </article></div>
    <div *ngIf="mode !== 'print' && character.canEdit" class="class-utilities">
      <label>Rolagens<select aria-label="Visibilidade dos recursos de classe" [(ngModel)]="visibility" [ngModelOptions]="{standalone:true}"><option value="campaign">Mesa</option><option value="private">Privadas</option><option *ngIf="isMaster" value="gm">Mestres</option></select></label>
      <button type="button" [disabled]="busy() || !ready" (click)="execute('short-rest')">Descanso curto (1h)</button>
      <button *ngIf="hasAction('short-rest-meditation')" type="button" [disabled]="busy() || !ready" (click)="execute('short-rest-meditation')">Descanso + meditação</button>
      <button *ngIf="hasAction('class-turn')" type="button" [disabled]="busy() || !ready" (click)="execute('class-turn')">Novo turno</button>
    </div>
    <div *ngIf="mode !== 'print' && character.canEdit" class="class-utilities">
      <label>Descanso longo concluído<select aria-label="Condições do descanso longo" [(ngModel)]="longRestOption" [ngModelOptions]="{standalone:true}"><option value="confirmed">8h; limite de 24h respeitado</option><option value="fed">8h, comida e água; limite de 24h respeitado</option><option value="meditated">8h com 30 min de meditação; limite de 24h respeitado</option><option value="fed-meditated">8h, comida, água e meditação; limite de 24h respeitado</option></select></label>
      <button type="button" [disabled]="busy() || !ready" (click)="execute('long-rest', 0, longRestOption)">Aplicar descanso longo</button>
    </div>
    <details *ngIf="hasAction('sorcery-create') && mode !== 'print' && character.canEdit"><summary>Conjuração flexível</summary><div class="class-utilities">
      <label>Nível para criar<select aria-label="Nível do espaço a criar" [(ngModel)]="createLevel" [ngModelOptions]="{standalone:true}"><option *ngFor="let level of [1,2,3,4,5]" [ngValue]="level">{{ level }}</option></select></label>
      <button type="button" [disabled]="busy() || !ready" (click)="execute('sorcery-create', createLevel)">Criar espaço</button>
      <label>Converter de<select aria-label="Origem do espaço a converter" [(ngModel)]="convertSource" [ngModelOptions]="{standalone:true}"><option value="spell">Conjuração</option><option *ngIf="hasAction('pact-cast')" value="pact">Pacto</option></select></label>
      <label *ngIf="convertSource === 'spell'">Nível<select aria-label="Nível do espaço a converter" [(ngModel)]="convertLevel" [ngModelOptions]="{standalone:true}"><option *ngFor="let level of [1,2,3,4,5,6,7,8,9]" [ngValue]="level">{{ level }}</option></select></label>
      <button type="button" [disabled]="busy() || !ready" (click)="execute('sorcery-convert', convertLevel, convertSource)">Converter em pontos</button>
    </div></details>
    <details *ngIf="hasAction('arcane-start') && mode !== 'print' && character.canEdit"><summary>Recuperação Arcana</summary>
      <p>Hoje: {{ view()?.state?.['arcana_usada'] ? 'já utilizada' : 'disponível após descanso curto' }}. Níveis de espaço restantes na seleção: {{ view()?.state?.['arcana_saldo'] || 0 }}.</p><div class="class-utilities">
        <button type="button" [disabled]="busy() || !ready" (click)="execute('arcane-start')">Iniciar seleção após descanso</button>
        <label>Nível<select aria-label="Nível para Recuperação Arcana" [(ngModel)]="arcaneLevel" [ngModelOptions]="{standalone:true}"><option *ngFor="let level of [1,2,3,4,5]" [ngValue]="level">{{ level }}</option></select></label>
        <button type="button" [disabled]="busy() || !ready" (click)="execute('arcane-slot', arcaneLevel)">Recuperar espaço</button>
        <button type="button" [disabled]="busy() || !ready" (click)="execute('arcane-end')">Concluir seleção</button>
        <button type="button" [disabled]="busy() || !ready" (click)="execute('class-day')">Confirmar novo dia de jogo</button>
      </div><small>Disponível uma vez por dia de jogo. A seleção precisa ocorrer após o descanso, antes de outra atividade.</small>
    </details>
    <p *ngIf="character.canEdit && !ready" class="feedback">Aguarde o salvamento da ficha.</p>
    <p *ngIf="message() && mode !== 'print'" role="status" class="feedback">{{ message() }}</p><p *ngIf="error()" role="alert" class="feedback error">{{ error() }}</p>
  </section>
`, styles:[`
 :host{display:block;min-width:0}.class-resources{margin:10px 0;color:#332a22;font-family:Arial,sans-serif}.class-resources h2{display:flex;justify-content:space-between;margin:0 0 7px;padding-bottom:5px;border-bottom:2px solid #876e51;font-size:11px;letter-spacing:.07em}.class-resources h2 small{font-size:9px}.resource-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,175px),1fr));gap:5px}.resource-grid article{min-width:0;border:1px solid #ae9676;background:#faf1df;padding:7px}.resource-title{display:flex;justify-content:space-between;gap:5px;align-items:baseline}.resource-title strong{font-size:11px}.resource-title b{font:700 17px Georgia,serif;white-space:nowrap}small{font-size:9px;color:#725e47}p{font-size:10px;line-height:1.35;margin:5px 0}.resource-controls,.class-utilities{display:flex;align-items:end;gap:5px;flex-wrap:wrap;margin-top:6px}label{display:grid;gap:3px;font-size:9px;min-width:0}input,select{min-width:0;max-width:100%;background:#fff8e9;color:#332a22;border:1px solid #ae9676;padding:4px;font-size:11px;min-height:30px}input{width:60px}button{border:1px solid #987750;background:#eee1c7;color:#3b2d20;padding:5px 7px;min-height:30px;cursor:pointer;font-size:10px}button:disabled{opacity:.45;cursor:default}button:focus-visible,input:focus-visible,select:focus-visible,summary:focus-visible{outline:2px solid #68462e;outline-offset:2px}details{margin-top:7px;padding:6px;border:1px solid #b69c7b;background:#f5ebd6}summary{cursor:pointer;font-size:10px;font-weight:700}.feedback{padding:6px;border:1px solid #ae9676;background:#fbf3e4}.error{color:#8d2525}@media print{.resource-grid article{break-inside:avoid}.class-utilities{display:none}}
 .class-resources input[type=number]{width:60px;min-width:0;margin:0;padding:4px;border:1px solid #ae9676;border-radius:2px;background:#fff8e9;color:#332a22;font-size:11px;min-height:30px}.class-resources input[type=number]:focus{background:#fff8e9;border-color:#68462e}
`]})
export class DndClassResourcesComponent implements OnChanges, OnDestroy {
  @Input({required: true}) character!: Character;
  @Input({required: true}) system!: RpgSystem;
  @Input() ready = false;
  @Input() mode: 'read' | 'edit' | 'print' = 'read';
  @Input() isMaster = false;
  @Output() applied = new EventEmitter<Character>();
  @Output() working = new EventEmitter<boolean>();
  private api = inject(ApiService);
  private request?: Subscription;
  private version = '';
  resources = signal<ClassResource[]>([]); view = signal<ClassView | null>(null); busy = signal(false); message = signal(''); error = signal('');
  amounts: Record<string, number> = {}; saveKey = ''; createLevel = 1; convertLevel = 1; convertSource = 'spell'; arcaneLevel = 1; longRestOption = 'confirmed'; classKey = ''; levelDraft = 0;
  visibility: 'campaign' | 'private' | 'gm' = 'campaign';
  ngOnChanges() {
    const version = `${this.character.id}:${this.character.version}:${this.system.id}`; if (version === this.version) return;
    this.version = version; this.request?.unsubscribe();
    if (this.system.rulesetVersion !== '2014' || this.system.templateId !== 'dnd_5e') { this.resources.set([]); return; }
    this.request = this.api.characterRules(this.character.id).subscribe({next: result => {
      const view = result.supported ? result.rules as ClassView : null; this.view.set(view); this.resources.set(view?.resources || []);
      for (const resource of this.resources()) this.amounts[resource.id] ??= 1;
      this.saveKey ||= this.saves()[0]?.key || '';
      if (!this.classKey && this.classes().length) this.selectClass(this.classes()[0].value);
    }, error: error => this.error.set(apiError(error))});
  }
  saves() { return this.system.fields.filter(f => f.key.startsWith('res_') && !!f.rollExpression); }
  classes() { return this.view()?.actions.find(action => action.id === 'configure')?.options?.filter(option => option.value.startsWith('nivel_')) || []; }
  selectClass(key: string) { this.classKey = key; this.levelDraft = this.view()?.state[key] || 0; }
  variable(id: string) { return ['ki','feiticaria','cura_maos'].includes(id); }
  hasAction(id: string) { return this.view()?.actions.some(action => action.id === id) || false; }
  use(resource: ClassResource) {
    const special: Record<string, string> = {folego:'second-wind', indomavel:'indomitable', mestre_mistico:'eldritch-master'};
    this.execute(special[resource.id] || 'class-spend', special[resource.id] ? 0 : this.variable(resource.id) ? this.amounts[resource.id] : 1, resource.id === 'indomavel' ? this.saveKey : resource.id);
  }
  execute(action: string, amount = 0, option: string | null = null) {
    if (!this.ready || this.busy() || !this.character.canEdit) return;
    if (!Number.isInteger(amount) || amount < 0) { this.error.set('Informe uma quantidade inteira válida.'); return; }
    this.busy.set(true); this.working.emit(true); this.error.set(''); this.message.set('');
    this.api.applyCharacterRule(this.character.id, this.character.version, {action, amount, option, visibility: this.visibility})
      .pipe(finalize(() => {this.busy.set(false); this.working.emit(false);})).subscribe({next: result => {this.message.set(result.message); this.applied.emit(result.character);}, error: error => this.error.set(apiError(error))});
  }
  ngOnDestroy() { this.request?.unsubscribe(); }
}
