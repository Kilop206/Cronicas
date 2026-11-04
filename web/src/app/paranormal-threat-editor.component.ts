import {CommonModule} from '@angular/common';
import {Component,EventEmitter,Input,Output} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {ParanormalThreatSheet,ThreatAbility,THREAT_ACTIONS,THREAT_ATTRIBUTES,THREAT_CONDITIONS,THREAT_DAMAGE_TYPES,THREAT_ELEMENTS,THREAT_SIZES,threatId} from './paranormal-threat';

@Component({selector:'app-paranormal-threat-editor',standalone:true,imports:[CommonModule,FormsModule],templateUrl:'./paranormal-threat-editor.component.html',styleUrls:['./paranormal-threat.css'],host:{'(input)':'changed.emit()','(change)':'changed.emit()'}})
export class ParanormalThreatEditorComponent {
  @Input({required:true})sheet!:ParanormalThreatSheet;@Output()changed=new EventEmitter<void>();
  attributes=THREAT_ATTRIBUTES;types=THREAT_DAMAGE_TYPES;elements=THREAT_ELEMENTS;sizes=THREAT_SIZES;actions=THREAT_ACTIONS;conditions=THREAT_CONDITIONS;
  standalone={standalone:true};nex=[...Array.from({length:20},(_,i)=>i*5),99];
  sources=[{value:'base',label:'Estatística base'},{value:'ability',label:'Habilidade'},{value:'item',label:'Item'},{value:'ritual',label:'Ritual'},{value:'ally',label:'Aliado'}];
  addSkill(){if(this.sheet.skills.length>=40)return;this.sheet.skills.push({id:threatId('pericia'),name:'Nova perícia',attribute:'intelecto',bonus:0});this.changed.emit();}
  addAttack(){if(this.sheet.attacks.length>=20)return;this.sheet.attacks.push({id:threatId('ataque'),name:'Novo ataque',attribute:'forca',bonus:0,style:'melee',damage:'1d6',damageType:'impacto',extraDamage:'0',extraType:'impacto',count:1,margin:20,multiplier:2,ammunition:-1,cost:0,action:'standard',range:'',effect:''});this.changed.emit();}
  addAbility(){if(this.sheet.abilities.length>=30)return;this.sheet.abilities.push({id:threatId('habilidade'),name:'Nova habilidade',action:'standard',cost:0,roll:'',maximumUses:0,remainingUses:0,recharge:'manual',description:''});this.changed.emit();}
  addDefense(){if(this.sheet.defenses.length>=40)return;this.sheet.defenses.push({id:threatId('defesa'),type:'all',effect:'reduction',amount:0,sourceKind:'base',sourceId:'base',source:'',except:[],disabledByEnigma:false});this.changed.emit();}
  remove<T>(list:T[],index:number){list.splice(index,1);this.changed.emit();}
  maximumUses(ability:ThreatAbility,value:number){ability.maximumUses=value;ability.remainingUses=Math.min(ability.remainingUses,value);this.changed.emit();}
  toggleImmunity(key:string,enabled:boolean){this.sheet.conditionImmunities=enabled?[...this.sheet.conditionImmunities,key]:this.sheet.conditionImmunities.filter(k=>k!==key);this.changed.emit();}
  context(key:string,value:number){if(value)this.sheet.state[key]=value;else delete this.sheet.state[key];this.changed.emit();}
}
