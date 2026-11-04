import {CommonModule} from '@angular/common';
import {Component,EventEmitter,Input,Output} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {ThreatSensoryContext} from './paranormal-threat';

@Component({selector:'app-paranormal-threat-context',standalone:true,imports:[CommonModule,FormsModule],styleUrls:['./paranormal-threat.css'],template:`
  <fieldset [disabled]="disabled" class="sense-context"><legend>Contexto de percepção do oponente</legend>
    <label class="check"><input type="checkbox" aria-label="Ativar contexto de percepção" [ngModel]="value.active" (ngModelChange)="patch({active:$event})" [ngModelOptions]="standalone"> Usar este contexto nos ataques e testes</label>
    <div class="edit-grid" *ngIf="value.active">
      <label>Distância ao oponente (m)<input type="number" aria-label="Distância ao oponente" [ngModel]="value.distance" (ngModelChange)="patch({distance:$event})" [ngModelOptions]="standalone" min="0" max="999" step="any"><small>Vazio: distância desconhecida.</small></label>
      <label>Camuflagem<select aria-label="Camuflagem no contexto de percepção" [ngModel]="value.concealment" (ngModelChange)="patch({concealment:$event})" [ngModelOptions]="standalone"><option [ngValue]="0">Nenhuma</option><option [ngValue]="1">Leve · 20%</option><option [ngValue]="2">Total · 50%</option></select></label>
      <label>Origem da camuflagem<select aria-label="Origem da camuflagem" [ngModel]="value.cause" (ngModelChange)="cause($event)" [ngModelOptions]="standalone"><option value="other">Outro efeito (névoa, folhagem…)</option><option value="darkness">Escuridão comum</option><option value="paranormal-darkness">Escuridão paranormal</option><option value="invisibility">Invisibilidade</option><option value="blindness">Falta de visão da ameaça</option></select></label>
      <label class="check"><input type="checkbox" aria-label="Oponente não pode ser visto" [ngModel]="value.opponentUnseen" (ngModelChange)="patch({opponentUnseen:$event})" [ngModelOptions]="standalone"> Oponente não pode ser visto</label>
      <label class="check"><input type="checkbox" aria-label="Sentidos especiais indisponíveis" [ngModel]="value.unavailable" (ngModelChange)="patch({unavailable:$event})" [ngModelOptions]="standalone"> Sentidos especiais indisponíveis neste contexto</label>
    </div><p class="hint">Este contexto vale para um oponente. Informe a distância para aplicar benefícios de alcance. Ele não remove condições físicas nem revela automaticamente pistas, cores ou escrita.</p>
  </fieldset>`})
export class ParanormalThreatContextComponent {
  @Input({required:true})value!:ThreatSensoryContext;@Input()disabled=false;@Output()valueChange=new EventEmitter<ThreatSensoryContext>();
  standalone={standalone:true};
  patch(change:Partial<ThreatSensoryContext>){this.valueChange.emit({...this.value,...change});}
  cause(cause:ThreatSensoryContext['cause']){this.patch(cause==='invisibility'||cause==='blindness'?{cause,concealment:2,opponentUnseen:true}:{cause});}
}
