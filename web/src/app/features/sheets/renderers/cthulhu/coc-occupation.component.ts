import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription, finalize } from 'rxjs';
import { ApiService, Character, CthulhuOccupation, CthulhuOccupationSlot, RpgSystem, apiError } from '../../../../api';

interface Allocation { key: string; label: string; base: number; occupation: number; interest: number; occupational: boolean; }
interface OccupationView {
  phase: number; occupations: CthulhuOccupation[]; selected: string; slots: CthulhuOccupationSlot[];
  choices: {slot: string; skill: string}[]; skills: Allocation[]; occupationalTotal: number; interestTotal: number;
  occupationalSpent: number; interestSpent: number; creditMin: number; creditMax: number; personalCredit: boolean; error: string | null;
}
interface CreationView { occupation?: OccupationView; state: Record<string, number>; }

@Component({selector: 'app-coc-occupation', standalone: true, imports: [CommonModule, FormsModule], template: `
  <section class="occupation" aria-label="Criação por ocupação">
    <h2>OCUPAÇÃO E INTERESSES <small>7e · criação por pontos</small></h2>
    <p *ngIf="loading()" role="status">Carregando distribuição…</p>
    <ng-container *ngIf="view() as creation"><ng-container *ngIf="creation.occupation as occupation">
      <div *ngIf="occupation.phase === 0">
        <p>Conclua a geração de características e os ajustes de idade nas ações da ficha. Depois escolha a ocupação para aplicar as bases das perícias e separar os dois orçamentos.</p>
        <p>Esta etapa começa apenas com perícias e Crédito zerados. Investigadores já preenchidos preservam seus valores e podem continuar usando desenvolvimento.</p>
        <div class="utilities" *ngIf="character.canEdit && mode !== 'print'">
          <label>Ocupação<select aria-label="Ocupação na criação" [(ngModel)]="selected" [ngModelOptions]="{standalone:true}" [disabled]="busy()">
            <option value="">Escolha uma ocupação</option><option *ngFor="let profile of occupation.occupations" [value]="profile.id">{{ profile.name }}</option>
          </select></label><button type="button" [disabled]="busy() || !ready || !selected || creation.state['criacao_fase'] !== 2" (click)="execute('occupation-start', 0, selected)">Iniciar distribuição</button>
        </div>
        <p *ngIf="selectedProfile() as profile">Profissionais: {{ profile.points }} · Interesses: INT × 2 · Crédito: {{ profile.creditMin }}–{{ profile.creditMax }}.</p>
      </div>
      <ng-container *ngIf="occupation.phase > 0">
        <div class="budgets"><div><span>PONTOS PROFISSIONAIS</span><b>{{ occupation.occupationalTotal - occupation.occupationalSpent }}</b><small>restantes de {{ occupation.occupationalTotal }}</small></div><div><span>INTERESSES PESSOAIS</span><b>{{ occupation.interestTotal - occupation.interestSpent }}</b><small>restantes de {{ occupation.interestTotal }}</small></div><div><span>CRÉDITO NA CRIAÇÃO</span><b>{{ occupation.creditMin }}–{{ occupation.creditMax }}</b><small>{{ occupation.personalCredit ? 'Aceita os dois orçamentos' : 'Usa pontos profissionais' }}</small></div></div>
        <p *ngIf="occupation.phase === 2" role="status">Criação concluída. Os valores abaixo registram a distribuição inicial; o desenvolvimento posterior fica na ficha e em seu histórico. Pontos restantes foram descartados.</p>
        <p *ngIf="occupation.error" role="alert" class="feedback error">{{ occupation.error }}</p>
        <div class="choices" aria-label="Oito perícias ocupacionais">
          <label *ngFor="let slot of occupation.slots">{{ slot.label }}
            <select [attr.aria-label]="'Perícia ocupacional: ' + slot.label" [ngModel]="choice(slot.id)" (ngModelChange)="execute('occupation-choice', 0, slot.id, $event)" [ngModelOptions]="{standalone:true}" [disabled]="!editable()">
              <option value="">Escolha uma perícia</option><option *ngFor="let skill of available(slot)" [value]="skill.key" [disabled]="usedElsewhere(slot.id, skill.key)">{{ skill.label }}</option>
            </select>
          </label>
        </div>
        <div class="allocation-list" role="table" aria-label="Distribuição de pontos por perícia">
          <div class="allocation-row headings" role="row"><span role="columnheader">Perícia</span><span role="columnheader">Base</span><span role="columnheader">Prof.</span><span role="columnheader">Interesse</span><span role="columnheader">Total</span></div>
          <div *ngFor="let skill of occupation.skills" class="allocation-row" role="row" [attr.data-skill]="skill.key">
            <span role="cell">{{ skill.label }}<small *ngIf="skill.occupational">ocupacional</small></span><span role="cell">{{ skill.base }}</span>
            <span role="cell"><input type="number" [attr.aria-label]="'Pontos profissionais: ' + skill.label" [ngModel]="skill.occupation" [ngModelOptions]="{standalone:true}" (blur)="allocate(skill, 'occupation', $any($event.target).value)" min="0" max="9999" [disabled]="!editable() || !skill.occupational"></span>
            <span role="cell"><input type="number" [attr.aria-label]="'Interesses pessoais: ' + skill.label" [ngModel]="skill.interest" [ngModelOptions]="{standalone:true}" (blur)="allocate(skill, 'interest', $any($event.target).value)" min="0" max="9999" [disabled]="!editable() || skill.key === 'credito' && !occupation.personalCredit"></span>
            <b role="cell">{{ skill.base + skill.occupation + skill.interest }}%</b>
          </div>
        </div>
        <div class="utilities" *ngIf="occupation.phase === 1 && character.canEdit && mode !== 'print'">
          <button type="button" [disabled]="!editable() || occupation.choices.length !== 8" (click)="execute('occupation-finish')">Concluir ocupação e perícias</button>
          <button type="button" [disabled]="busy() || !ready" (click)="reset()">Reiniciar distribuição</button>
        </div><small *ngIf="occupation.phase === 1">Os valores são salvos ao sair do campo. Zero devolve os pontos. Ao concluir, pontos não utilizados são descartados.</small>
      </ng-container>
    </ng-container></ng-container>
    <p *ngIf="character.canEdit && !ready && mode !== 'print'" role="status">Aguarde o salvamento da ficha.</p>
    <p *ngIf="message() && mode !== 'print'" role="status" class="feedback">{{ message() }}</p><p *ngIf="error()" role="alert" class="feedback error">{{ error() }}</p>
  </section>
`, styles: [`
 :host{display:block;min-width:0}.occupation{color:#20352e;font-family:Arial,sans-serif}.occupation h2{display:flex;flex-wrap:wrap;justify-content:space-between;gap:6px;border-bottom:2px solid #688175;padding-bottom:6px;margin:0 0 10px;font-size:12px;letter-spacing:.06em}.occupation h2 small{font-size:10px}p{font-size:12px;line-height:1.5}.utilities{display:flex;flex-wrap:wrap;align-items:end;gap:8px;margin:10px 0}label{display:grid;gap:4px;min-width:0;font-size:11px}select,input{min-width:0;max-width:100%;background:#faf9ed;color:#20352e;border:1px solid #83968a;padding:5px;font:12px Arial,sans-serif;min-height:32px}button{background:#385849;color:#fff;border:1px solid #385849;padding:7px 10px;font-size:11px;min-height:34px;cursor:pointer}button:disabled,input:disabled,select:disabled{opacity:.6;cursor:default}button:focus-visible,select:focus-visible,input:focus-visible{outline:2px solid #20352e;outline-offset:2px}.budgets{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:7px}.budgets>div{padding:10px;border:1px solid #83968a;background:#f4f2e5}.budgets span,.budgets small{display:block;font-size:10px}.budgets b{display:block;font:700 27px Georgia,serif;margin:4px 0}.choices{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:8px;margin:12px 0}.allocation-list{border:1px solid #83968a;background:#f4f2e5}.allocation-row{display:grid;grid-template-columns:minmax(0,1fr) 42px 70px 70px 60px;align-items:center;gap:6px;padding:5px 8px;border-bottom:1px dotted #8a9f91;font-size:12px}.allocation-row:last-child{border:0}.allocation-row>span:not(:first-child),.allocation-row>b{text-align:center}.allocation-row input{width:100%;text-align:center}.allocation-row small{display:block;font-size:9px;color:#486451}.headings{background:#354d43;color:#fff;font-size:10px;font-weight:700}.occupation>small{display:block;line-height:1.4;font-size:11px}.feedback{border:1px solid #83968a;padding:8px;background:#f4f2e5}.error{color:#8c2525}@media(max-width:600px){.budgets{grid-template-columns:1fr 1fr}.budgets>div:last-child{grid-column:1/-1}.choices{grid-template-columns:1fr}.allocation-row{grid-template-columns:minmax(0,1fr) 25px 48px 48px 38px;gap:3px;padding:5px;font-size:10px}.allocation-row input{padding:3px;font-size:11px}.allocation-row>b{font-size:11px}.utilities>label{width:100%}}@media print{.utilities{display:none}.choices{break-inside:avoid}.allocation-row input{border:0;background:transparent;opacity:1}}
 .occupation input[type=number]{min-width:0;width:100%;margin:0;padding:5px;border:1px solid #83968a;border-radius:2px;background:#faf9ed;color:#20352e;font:12px Arial,sans-serif;min-height:32px}.occupation input[type=number]:disabled{background:#d9dfd1;color:#20352e;opacity:1}.occupation input[type=number]:focus{background:#fffdf0;border-color:#385849}@media(max-width:600px){.occupation input[type=number]{padding:3px;font-size:11px}}
`]})
export class CocOccupationComponent implements OnChanges, OnDestroy {
  @Input({required:true}) character!: Character;
  @Input({required:true}) system!: RpgSystem;
  @Input() ready = false;
  @Input() mode: 'read' | 'edit' | 'print' = 'read';
  @Output() applied = new EventEmitter<Character>();
  @Output() working = new EventEmitter<boolean>();
  private api = inject(ApiService); private request?: Subscription; private command?: Subscription; private version = '';
  view = signal<CreationView | null>(null); loading = signal(false); busy = signal(false); error = signal(''); message = signal(''); selected = '';
  ngOnChanges() {
    const version = `${this.character.id}:${this.character.version}:${this.system.id}:${this.system.revision}`;
    if (version === this.version) return; this.version = version; this.request?.unsubscribe(); this.loading.set(true);
    this.request = this.api.characterRules(this.character.id).pipe(finalize(() => this.loading.set(false))).subscribe({next: result => {this.view.set(result.supported ? result.rules as CreationView : null);}, error: error => this.error.set(apiError(error))});
  }
  selectedProfile() { return this.view()?.occupation?.occupations.find(profile => profile.id === this.selected); }
  editable() { return this.character.canEdit && this.mode !== 'print' && this.ready && !this.busy() && this.view()?.occupation?.phase === 1 && !this.view()?.occupation?.error; }
  choice(slot: string) { return this.view()?.occupation?.choices.find(choice => choice.slot === slot)?.skill || ''; }
  usedElsewhere(slot: string, key: string) { return this.view()?.occupation?.choices.some(choice => choice.slot !== slot && choice.skill === key); }
  available(slot: CthulhuOccupationSlot) { return this.view()?.occupation?.skills.filter(skill => skill.key !== 'credito' && (!slot.skills || slot.skills.includes(skill.key))) || []; }
  allocate(skill: Allocation, source: 'occupation' | 'interest', raw: string) {
    const amount = Number(raw); if (!this.editable() || raw.trim() === '' || amount === skill[source]) return;
    this.execute('occupation-allocate', amount, skill.key, source);
  }
  reset() { if (confirm('Reiniciar a distribuição? Os pontos serão devolvidos e as perícias voltarão ao estado anterior à ocupação. As características serão mantidas.')) this.execute('occupation-reset'); }
  execute(action: string, amount = 0, option: string | null = null, expression?: string) {
    if (!this.ready || this.busy() || !this.character.canEdit || this.mode === 'print') return;
    if (!Number.isInteger(amount) || amount < 0 || amount > 9999) { this.error.set('Informe uma quantidade inteira entre 0 e 9999.'); return; }
    this.busy.set(true); this.working.emit(true); this.error.set(''); this.message.set('');
    this.command = this.api.applyCharacterRule(this.character.id, this.character.version, {action, amount, option, expression, visibility:'private'})
      .pipe(finalize(() => {this.busy.set(false); this.working.emit(false);})).subscribe({next: result => {this.message.set(result.message); this.applied.emit(result.character);}, error: error => this.error.set(apiError(error))});
  }
  ngOnDestroy() { this.request?.unsubscribe(); this.command?.unsubscribe(); }
}
