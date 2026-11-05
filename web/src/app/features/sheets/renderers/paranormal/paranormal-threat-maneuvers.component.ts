import {CommonModule} from '@angular/common';
import {Component,EventEmitter,Input,OnChanges,OnDestroy,Output,inject,signal} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {Subscription,finalize,map,switchMap} from 'rxjs';
import {ApiService,Character,Npc,apiError} from '../../../../api';
import {THREAT_EFFECT_SOURCES,THREAT_SIZES,ThreatManeuverOutcome} from './paranormal-threat';

@Component({selector:'app-paranormal-threat-maneuvers',standalone:true,imports:[CommonModule,FormsModule],templateUrl:'./paranormal-threat-maneuvers.component.html',styleUrls:['./paranormal-threat.css']})
export class ParanormalThreatManeuversComponent implements OnChanges,OnDestroy {
  @Input({required:true})npc!:Npc;@Input()characters:Character[]=[];@Input()disabled=false;@Input()visibility:'gm'|'private'|'campaign'='gm';
  @Output()applied=new EventEmitter<Npc>();@Output()working=new EventEmitter<boolean>();
  private api=inject(ApiService);private command?:Subscription;private lookup?:Subscription;private currentId='';
  standalone={standalone:true};sizes=THREAT_SIZES;sources=THREAT_EFFECT_SOURCES;
  busy=signal(false);loading=signal(false);error=signal('');message=signal('');outcome=signal<ThreatManeuverOutcome|null>(null);targetName=signal('');
  attackerRolls=signal<{expression:string;rolls:number[];total:number}[]>([]);defenderRolls=signal<{expression:string;rolls:number[];total:number}[]>([]);
  npcs=signal<Npc[]>([]);page=1;total=0;search='';targetKind:'character'|'npc'='character';targetId='';kind:'trip'|'push'='trip';
  inReach=false;targetSize='medium';targetSizeConfirmed=false;targetAttribute:'forca'|'agilidade'='forca';agileWeaponConfirmed=false;attackerAdjustment=0;defenderAdjustment=0;source='normal';usePerceptionContext=false;targetBonusIncludesSize=false;targetImmune=false;
  ngOnChanges(){if(this.currentId===this.npc.id)return;this.currentId=this.npc.id;this.targetId='';this.inReach=false;this.usePerceptionContext=false;this.outcome.set(null);this.message.set('');this.error.set('');this.npcs.set([]);this.total=0;this.page=1;}
  kindChanged(kind:'character'|'npc'){this.targetKind=kind;this.targetChanged();if(kind==='npc')this.loadTargets(1);}
  targetChanged(){this.targetId='';this.inReach=false;this.targetSizeConfirmed=false;this.usePerceptionContext=false;this.targetImmune=false;}
  selectTarget(id:string){this.targetChanged();this.targetId=id;}
  loadTargets(page=1){this.page=page;this.loading.set(true);this.lookup?.unsubscribe();this.lookup=this.api.npcs(this.npc.campaignId,page,this.search).pipe(finalize(()=>this.loading.set(false))).subscribe({next:r=>{this.npcs.set(r.items.filter(n=>n.id!==this.npc.id));this.total=r.total;},error:e=>this.error.set(apiError(e))});}
  maneuverLabel(kind:string){return kind==='trip'?'Derrubar':'Empurrar';}
  outcomeLabel(status:string){return ({success:'Sucesso',failed:'Alvo resistiu',blocked:'Imunidade ou origem',tied:'Empates não resolvidos',concealment:'Falha por camuflagem'} as Record<string,string>)[status]||status;}
  resolve(){
    if(this.disabled||this.busy()||!this.targetId||!this.npc.sheet?.maneuverProfile?.configured)return;
    this.error.set('');this.message.set('');this.outcome.set(null);this.attackerRolls.set([]);this.defenderRolls.set([]);this.busy.set(true);this.working.emit(true);
    const target=this.targetKind==='character'?this.api.character(this.targetId).pipe(map(c=>({id:c.id,version:c.version}))):this.api.npc(this.targetId).pipe(map(n=>({id:n.id,version:n.version})));
    this.command=target.pipe(switchMap(t=>this.api.npcManeuver(this.npc.id,this.npc.version,{kind:this.kind,targetId:t.id,targetKind:this.targetKind,targetVersion:t.version,inReach:this.inReach,visibility:this.visibility,targetSize:this.targetSize,targetSizeConfirmed:this.targetSizeConfirmed,targetAttribute:this.targetAttribute,agileWeaponConfirmed:this.agileWeaponConfirmed,attackerAdjustment:this.attackerAdjustment,defenderAdjustment:this.defenderAdjustment,source:this.source,usePerceptionContext:this.usePerceptionContext,targetBonusIncludesSize:this.targetBonusIncludesSize,targetImmune:this.targetImmune})),finalize(()=>{this.busy.set(false);this.working.emit(false);})).subscribe({next:r=>{this.outcome.set(r.outcome);this.message.set(r.outcome.message);this.targetName.set(r.targetName);this.attackerRolls.set(r.attackerRolls);this.defenderRolls.set(r.defenderRolls);this.applied.emit(r.npc);},error:e=>this.error.set(apiError(e))});
  }
  ngOnDestroy(){this.command?.unsubscribe();this.lookup?.unsubscribe();}
}
