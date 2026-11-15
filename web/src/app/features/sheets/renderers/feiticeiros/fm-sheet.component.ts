import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ApiService, Character, CharacterInput, FmActionContext, RpgSystem, SheetCellValue, apiError } from '../../../../api';
import { SystemFieldComponent } from '../../editor/specialized-sheet.component';
import { SheetCollectionFieldComponent } from '../../components/sheet-collection-field.component';
import { CharacterInventoryComponent } from '../../../characters/character-inventory.component';
import { CharacterHistoryPanelComponent } from '../../../characters/character-history.component';
import { DndActionPanelComponent } from '../dnd/dnd-sheet.component';
import { FM_ABILITIES, FM_APTITUDES, FM_ATTRIBUTES, FM_CLASSES, FM_DAMAGE, FM_ORIGINS, FM_SAVES, FM_SKILLS, FM_TEMPLATE } from './fm-definition';
import { fmConditions, fmDefense, fmMod, fmNumber, fmRows, fmSkillBonus, fmSoulStage, fmSpeed, fmState } from './fm-projection';
import { FM_REFERENCE } from './fm-reference';
import bookIndex from './fm-book-index.json';

@Component({selector:'app-fm-sheet',standalone:true,imports:[CommonModule,FormsModule,SystemFieldComponent,SheetCollectionFieldComponent,CharacterInventoryComponent,CharacterHistoryPanelComponent,DndActionPanelComponent],templateUrl:'./fm-sheet.component.html',styleUrl:'./fm-sheet.component.css'})
export class FmSheetComponent {
  @Input({required:true}) system!:RpgSystem;
  @Input({required:true}) character!:Character;
  @Input({required:true}) draft!:CharacterInput;
  @Input() mode:'read'|'edit'|'print'='read';
  @Input() isMaster=false;
  @Input() rulesReady=false;
  @Output() changed=new EventEmitter<void>();
  @Output() restored=new EventEmitter<Character>();
  @Output() working=new EventEmitter<boolean>();
  private api=inject(ApiService);
  active=signal('principal'); busy=signal(false); message=signal(''); error=signal(''); query='';
  visibility:'campaign'|'private'='campaign'; target=0;
  attributes=FM_ATTRIBUTES;aptitudes=FM_APTITUDES;skills=FM_SKILLS;saves=FM_SAVES;abilities=FM_ABILITIES;
  optionLabels:Record<string,string>={...Object.fromEntries(FM_ATTRIBUTES),...Object.fromEntries(FM_ORIGINS.map(v=>[v.id,v.label])),...Object.fromEntries(FM_CLASSES.map(v=>[v.id,v.label])),humano:'Humano',maldicao:'Maldição',construto:'Construto',nenhum:'Nenhum',nenhuma:'Nenhuma',gojo:'Clã Gojo',inumaki:'Clã Inumaki',kamo:'Clã Kamo',zenin:'Clã Zenin',destruicao:'Destruição',economia:'Economia',refino:'Refino'};
  tabs=[['principal','Feiticeiro'],['combate','Combate'],['perfil','Perfil Amaldiçoado'],['progressao','Progressão'],['equipamento','Equipamento'],['referencia','Regras e índice'],['cronica','Crônica']];
  spellLevel=1;spellKind='ataque';spellName='';
  creationMethod='fixos';
  attackKind:FmActionContext['attackKind']='unspecified';targetExposed=false;targetUnconscious=false;targetParalyzed=false;attackAdvantage=false;attackDisadvantage=false;offhandLight=false;concealment=0;
  ritualMode:FmActionContext['ritual']='none';ritualFailure:FmActionContext['ritualFailure']='cancel';ritualCounts:Record<string,number>={dano:0,precisao:0,cd:0,alcance:0,area:0,alvos:0,sustento:0,efeito:0};
  fighterManeuver='none';combatArt='none';targetOffguard=false;targetFlanked=false;sneakAttack=false;unarmedAction='normal';
  maneuvers=[['ajuste','Ajuste'],['comando','Comando (você paga 1 PE)'],['desarme','Desarme'],['esquiva','Esquiva'],['trabalho_pes','Trabalho de Pés']];
  learnedManeuver(id:string){const list=this.draft.fields['manobras_lutador'];return Array.isArray(list)&&list.some(v=>v===id);}
  toggleManeuver(id:string){if(!this.editable())return;const list=this.draft.fields['manobras_lutador'];const current=Array.isArray(list)?list.filter((v):v is string=>typeof v==='string'):[];this.draft.fields['manobras_lutador']=current.includes(id)?current.filter(v=>v!==id):[...current,id];this.changed.emit();}
  preparationMax(){const real=this.classLevel('combate');return real?Math.max(0,real+Math.floor((this.number('nivel',1)-real)/2)+this.mod('sabedoria')):0;}
  preparation(){return Math.max(0,this.preparationMax()-(this.state()['preparo_gasto']||0));}
  spellAccess(){const level=this.number('nivel',1),real=this.classLevel('tecnica'),effect=real+Math.floor((level-real)/2);return Math.max(1+Math.floor((level-1)/4),real>=4?effect>=15?5:effect>=11?4:effect>=7?3:2:0);}
  ritualOptions=[{key:'dano',label:'Aumento de dano',max:2},{key:'precisao',label:'Precisão',max:2},{key:'cd',label:'Classe de Dificuldade',max:2},{key:'alcance',label:'Alcance',max:1},{key:'area',label:'Área',max:2},{key:'alvos',label:'Ajuste de alvos',max:1},{key:'sustento',label:'Sustento → concentração',max:1},{key:'efeito',label:'Potencialização de efeito',max:1}];
  field(key:string){return this.system.fields.find(f=>f.key===key);}
  number(key:string,fallback=0){return fmNumber(this.draft,key,fallback);}
  state(){return fmState(this.draft);}
  mod(key:string){return fmMod(this.draft,key);}
  signed(value:number){return value>=0?'+'+value:String(value);}
  show(id:string){return this.mode==='print'||this.active()===id;}
  editable(){return this.mode==='edit'&&this.character.canEdit;}
  rows(key:string){return fmRows(this.draft,key);}
  set(key:string,value:SheetCellValue){if(!this.editable()||this.field(key)?.formula)return;this.draft.fields[key]=value;this.changed.emit();}
  label(key:string){return this.field(key)?.label||key;}
  origin(){return FM_ORIGINS.find(v=>v.id===this.draft.fields['origem'])?.label||'Escolha a origem';}
  specialization(){return FM_CLASSES.find(v=>v.id===this.draft.fields['especializacao'])?.label||'Escolha a especialização';}
  soulMax(){return this.state()['alma_max']??this.draft.maxHealth;}
  soulCurrent(){return Math.max(0,this.soulMax()-(this.state()['alma_dano']||0));}
  soulLabel(){return ['Estável','Danificada','Instável','Crítica'][fmSoulStage(this.draft)];}
  conditions(){return [...fmConditions(this.draft)];}
  defense(){return fmDefense(this.draft);}
  speed(){return fmSpeed(this.draft);}
  attention(){return 10+fmSkillBonus(this.draft,'percepcao','sabedoria')+this.number('bonus_atencao');}
  skill(key:string,attribute:string,save=false){return fmSkillBonus(this.draft,key,attribute,save);}
  training(key:string,save=false){return Number(this.rows(save?'resistencias':'pericias').find(r=>r['chave']===key)?.['treino']||0);}
  other(key:string,save=false){return Number(this.rows(save?'resistencias':'pericias').find(r=>r['chave']===key)?.['outros']||0);}
  setTraining(key:string,column:string,value:number,save=false){
    if(!this.editable())return;const field=save?'resistencias':'pericias',rows=this.rows(field).map(r=>({...r}));let row=rows.find(r=>r['chave']===key);
    if(!row){row={chave:key,treino:0,outros:0};rows.push(row);}row[column]=Number(value);this.draft.fields[field]=rows;this.changed.emit();
  }
  classLevel(id:string){const rows=this.rows('multiclasse');return rows.length?Number(rows.find(r=>r['classe']===id)?.['nivel']||0):this.draft.fields['especializacao']===id?this.number('nivel',1):0;}
  gifted(id:string){return id==='energia_reversa'&&this.classLevel('suporte')>=6||id==='dominio_simples'&&this.draft.fields['origem']==='sem_tecnica'&&this.number('nivel')>=4;}
  hasAbility(id:string){return this.gifted(id)||Array.isArray(this.draft.fields['aptidoes'])&&(this.draft.fields['aptidoes'] as unknown[]).includes(id);}
  techniqueDc(){const level=this.number('nivel',1),bt=2+Math.floor((level-1)/4);const martial=Math.max(...['lutador','combate'].map(id=>{const real=this.classLevel(id),effect=real+Math.floor((level-real)/2);return real>=4?2+(effect>=8?1:0)+(effect>=16?1:0):0;}));return 10+Math.floor(level/2)+bt+this.mod(String(this.draft.fields['atributo_tecnica']||'sabedoria'))+this.number('bonus_cd')+martial+(this.classLevel('tecnica')>=10&&this.draft.fields['foco_tecnica']==='refino'?Math.floor(bt/2):0)+(this.classLevel('tecnica')===20?5:0)-(this.state()['exaustao']||0);}
  toggleAbility(id:string){if(!this.editable())return;const value=this.draft.fields['aptidoes'];const current=Array.isArray(value)?value.filter((v):v is string=>typeof v==='string'):[];this.draft.fields['aptidoes']=current.includes(id)?current.filter(v=>v!==id):[...current,id];this.changed.emit();}
  perform(action:string,option:string|null=null,amount=0){
    if(!this.character.canEdit||!this.rulesReady||this.busy()||this.mode==='print')return;
    this.busy.set(true);this.working.emit(true);this.error.set('');this.message.set('');
    const jujutsu:FmActionContext|undefined=action==='fm-attack'?{attackKind:this.attackKind,targetExposed:this.targetExposed,targetUnconscious:this.targetUnconscious,targetParalyzed:this.targetParalyzed,advantage:this.attackAdvantage,disadvantage:this.attackDisadvantage,offhandLight:this.offhandLight,concealment:Number(this.concealment),fighterManeuver:this.fighterManeuver,combatArt:this.combatArt,targetOffguard:this.targetOffguard,targetFlanked:this.targetFlanked,sneakAttack:this.sneakAttack,unarmedAction:this.unarmedAction}:action==='fm-cast'?{ritual:this.ritualMode,ritualFailure:this.ritualFailure,improvements:this.ritualMode==='none'?[]:this.ritualOptions.flatMap(v=>Array.from({length:Number(this.ritualCounts[v.key]||0)},()=>v.key))}:undefined;
    this.api.applyCharacterRule(this.character.id,this.character.version,{action,option,amount,visibility:this.visibility,...(jujutsu?{jujutsu}:{})}).pipe(finalize(()=>{this.busy.set(false);this.working.emit(false);})).subscribe({next:result=>{this.message.set(result.message+(result.rolls.length?' · '+result.rolls.map(r=>`${r.expression} [${r.rolls.join(', ')}] = ${r.total}`).join(' | '):''));this.restored.emit(result.character);},error:e=>this.error.set(apiError(e))});
  }
  filteredReferences(){const query=this.normalize(this.query);return FM_REFERENCE.filter(v=>!query||this.normalize(v.title+' '+v.section+' '+v.text).includes(query));}
  indexed(){const query=this.normalize(this.query);return query.length>=2?bookIndex.filter(v=>this.normalize(v.title+' '+v.category).includes(query)).slice(0,50):[];}
  indexCount=bookIndex.length;
  normalize(text:string){return text.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLocaleLowerCase();}
  fieldsFor(section:string){return this.system.fields.filter(f=>f.section===section&&!['estado_regras','manobras_lutador'].includes(f.key));}
  extras(){const keys=new Set(FM_TEMPLATE.fields.map(f=>f.key));return this.system.fields.filter(f=>!keys.has(f.key));}
  options(key:string):Record<string,{value:string;label:string}[]>{
    const choices=(values:string[])=>values.map(value=>({value,label:value.replaceAll('_',' ')}));
    if(key==='armas')return {atributo:FM_ATTRIBUTES.map(([value,label])=>({value,label})),acao:choices(['comum','bonus','reacao'])};
    if(key==='feiticos')return {tipo:choices(['ataque','resistencia','area','cura','auxiliar','passivo']),acao:choices(['comum','bonus','reacao','completa']),duracao:choices(['imediata','duradoura','sustentada','concentrada'])};
    if(key==='defesas')return {tipo:choices([...FM_DAMAGE,'fisico','todos']),efeito:choices(['rd','resistencia','imunidade','vulnerabilidade'])};
    if(key==='multiclasse')return {classe:FM_CLASSES.map(v=>({value:v.id,label:v.label}))};
    if(key==='criacao_atributos')return {chave:FM_ATTRIBUTES.map(([value,label])=>({value,label}))};
    if(key==='pericias'||key==='resistencias')return {chave:(key==='pericias'?FM_SKILLS:FM_SAVES).map(([value,label])=>({value,label})),treino:[{value:'0',label:'Não treinado'},{value:'1',label:'Treinado'},{value:'2',label:'Mestre'}]};
    return {};
  }
  defaults(key:string):Record<string,SheetCellValue>{
    if(key==='armas')return {nome:'Novo ataque',atributo:'forca',dados:'1d4',apos:'',treinado:true,margem:20,acao:'comum',municao:-1};
    if(key==='feiticos')return {nome:'Novo feitiço',nivel:1,tipo:'ataque',acao:'comum',duracao:'imediata',dados:'4d8',reducao:0};
    if(key==='defesas')return {tipo:'impacto',efeito:'rd',valor:0,fonte:'Equipamento'};
    return {};
  }
  addSpell(){
    if(!this.editable()||this.rows('feiticos').length>=30)return;
    const level=Number(this.spellLevel);const tables:Record<string,string[]>={ataque:['1d10','4d8','8d8','14d8','16d10','20d12','28d12'],resistencia:['1d10','3d8','7d8','12d8','14d10','18d12','26d12'],area:['','2d8','4d8','5d12','10d10','12d12','22d10'],cura:['','3d6','6d6','7d8','10d10','16d10','24d10']};
    this.draft.fields['feiticos']=[...this.rows('feiticos'),{...this.defaults('feiticos'),nome:this.spellName.trim()||'Novo feitiço',nivel:level,tipo:this.spellKind,dados:tables[this.spellKind]?.[level===6&&this.spellAccess()<5?5:level]||''}];this.changed.emit();
  }
}
