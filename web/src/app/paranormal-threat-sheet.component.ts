import {CommonModule} from '@angular/common';
import {Component,EventEmitter,Input,OnChanges,OnDestroy,Output,inject,signal} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {Subscription,finalize,switchMap} from 'rxjs';
import {ApiService,Character,Npc,SheetFieldValue,apiError} from './api';
import {d20ConditionState,d20Defense,d20Speed} from './d20-condition-display';
import {ThreatCommand,THREAT_ACTIONS,THREAT_ATTRIBUTES,THREAT_CONDITIONS,THREAT_DAMAGE_TYPES,THREAT_ELEMENTS,THREAT_SIZES,THREAT_IMMUNITY_GROUPS,THREAT_EFFECT_SOURCES,threatImmunities} from './paranormal-threat';
type AttackOutcome={index:number;total:number;hit:boolean|null;critical:boolean;damage:number;damageType:string;extraDamage:number;extraType:string};

@Component({selector:'app-paranormal-threat-sheet',standalone:true,imports:[CommonModule,FormsModule],templateUrl:'./paranormal-threat-sheet.component.html',styleUrls:['./paranormal-threat.css']})
export class ParanormalThreatSheetComponent implements OnChanges,OnDestroy {
  @Input({required:true})npc!:Npc;@Output()applied=new EventEmitter<Npc>();@Output()working=new EventEmitter<boolean>();
  private api=inject(ApiService);private command?:Subscription;private lookup?:Subscription;private currentId='';
  attributes=THREAT_ATTRIBUTES;types=THREAT_DAMAGE_TYPES;conditions=THREAT_CONDITIONS;standalone={standalone:true};
  effectSources=THREAT_EFFECT_SOURCES;conditionSource='manual';
  busy=signal(false);error=signal('');message=signal('');rolls=signal<{expression:string;rolls:number[];total:number}[]>([]);outcomes=signal<AttackOutcome[]>([]);
  characters=signal<Character[]>([]);revisions=signal<{version:number;savedAt:string;name:string}[]>([]);
  visibility:'gm'|'private'|'campaign'='gm';targetDefense=0;checkDifficulty=0;amount=1;damageType='impacto';half=false;ignoreResistance=false;source='normal';critical=false;normalAmount=0;sneakDamage=0;
  condition='caido';rounds=0;persistent=false;targetId='';additionalCreatures=0;highestConfirmed=false;revision=0;
  get sheet(){return this.npc.sheet!;}
  ngOnChanges(){if(this.currentId===this.npc.id)return;this.currentId=this.npc.id;this.message.set('');this.error.set('');this.rolls.set([]);this.outcomes.set([]);this.revisions.set([]);this.lookup?.unsubscribe();this.lookup=this.api.characters(this.npc.campaignId).subscribe({next:items=>this.characters.set(items),error:e=>this.error.set(apiError(e))});}
  label(key:string){return this.types.find(t=>t.value===key)?.label || key.replaceAll('_',' ');}
  actionLabel(key:string){return THREAT_ACTIONS.find(a=>a.value===key)?.label || key;}
  elementLabel(){return THREAT_ELEMENTS.find(e=>e.value===this.sheet.element)?.label || '';}
  sizeLabel(){return THREAT_SIZES.find(s=>s.value===this.sheet.size)?.label || '';}
  fields():Record<string,SheetFieldValue>{return {defesa:this.sheet.defense,deslocamento:this.sheet.speed,estado_regras:Object.entries(this.sheet.state).map(([chave,valor])=>({chave,valor}))};}
  activeConditions(){return [...d20ConditionState(this.fields(),this.npc.health,threatImmunities(this.sheet))];}
  immunityLabels(){return [...THREAT_IMMUNITY_GROUPS.filter(g=>(this.sheet.conditionImmunityGroups||[]).includes(g.value)).map(g=>g.label),...this.sheet.conditionImmunities.map(c=>c.replaceAll('_',' '))].join(' · ');}
  explicitCondition(key:string){return this.sheet.state[key==='sangrando'?'sangrando':'cond_'+key]!==undefined;}
  defense(ranged=false){return d20Defense(this.fields(),true,ranged,this.npc.health,threatImmunities(this.sheet));}
  speed(){return d20Speed({name:this.npc.name,portraitUrl:null,backgroundUrl:null,health:this.npc.health,maxHealth:this.npc.maxHealth,sanity:0,maxSanity:0,mana:0,maxMana:0,fields:this.fields(),version:this.npc.version},threatImmunities(this.sheet));}
  percent(){return 100*this.npc.health/this.npc.maxHealth;}
  run(action:string,option:string|null=null,amount=0){this.execute({action,option,amount});}
  damage(){this.execute({action:'damage',amount:this.amount,option:this.damageType,half:this.half,ignoreResistance:this.ignoreResistance,source:this.source,critical:this.critical,normalAmount:this.critical&&(this.sheet.criticalImmune||!!this.sheet.fortification)?this.normalAmount:null,sneakDamage:this.sneakDamage});}
  applyCondition(){this.execute({action:this.persistent?'condition-persistent':'condition',option:this.condition,amount:this.rounds,source:this.conditionSource});}
  private started(){this.busy.set(true);this.working.emit(true);this.error.set('');this.message.set('');this.rolls.set([]);this.outcomes.set([]);}
  private finished(){this.busy.set(false);this.working.emit(false);}
  private result(result:{npc:Npc;message:string;rolls:{expression:string;rolls:number[];total:number}[];attacks?:AttackOutcome[]}){this.message.set(result.message);this.rolls.set(result.rolls);this.outcomes.set(result.attacks||[]);this.applied.emit(result.npc);}
  execute(command:ThreatCommand){if(this.busy())return;this.started();this.command=this.api.npcRule(this.npc.id,this.npc.version,{...command,visibility:this.visibility}).pipe(finalize(()=>this.finished())).subscribe({next:r=>this.result(r),error:e=>this.error.set(apiError(e))});}
  presence(){if(this.busy()||!this.targetId)return;if(this.additionalCreatures>0&&!this.highestConfirmed){this.error.set('Para várias criaturas, selecione a ameaça de maior VD e confirme essa escolha.');return;}this.started();this.command=this.api.character(this.targetId).pipe(switchMap(target=>this.api.npcRule(this.npc.id,this.npc.version,{action:'presence',targetId:target.id,targetVersion:target.version,additionalCreatures:this.additionalCreatures,visibility:this.visibility})),finalize(()=>this.finished())).subscribe({next:r=>this.result(r),error:e=>this.error.set(apiError(e))});}
  loadHistory(){this.api.npcRevisions(this.npc.id).subscribe({next:r=>{this.revisions.set(r);this.revision=r[0]?.version||0;},error:e=>this.error.set(apiError(e))});}
  restore(){if(this.busy()||!this.revision)return;this.started();this.command=this.api.restoreNpc(this.npc.id,this.revision,this.npc.version).pipe(finalize(()=>this.finished())).subscribe({next:n=>{this.applied.emit(n);this.message.set('Versão anterior restaurada; a versão substituída permanece no histórico.');this.loadHistory();},error:e=>this.error.set(apiError(e))});}
  ngOnDestroy(){this.command?.unsubscribe();this.lookup?.unsubscribe();}
}
