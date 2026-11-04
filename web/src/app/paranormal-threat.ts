export interface ThreatAttributes { agilidade:number; forca:number; intelecto:number; presenca:number; vigor:number; }
export interface ThreatSkill { id:string; name:string; attribute:keyof ThreatAttributes; bonus:number; }
export interface ThreatAttack { id:string; name:string; attribute:keyof ThreatAttributes; bonus:number; style:'melee'|'ranged'; damage:string; damageType:string; extraDamage:string; extraType:string; count:number; margin:number; multiplier:number; ammunition:number; cost:number; action:string; range:string; effect:string; }
export interface ThreatAbility { id:string; name:string; action:string; cost:number; roll:string; maximumUses:number; remainingUses:number; recharge:'manual'|'turn'|'scene'; description:string; mentalOnly?:boolean; }
export interface ThreatDefense { id:string; type:string; effect:'reduction'|'immunity'|'vulnerability'; amount:number; sourceKind:string; sourceId:string; source:string; except:string[]|null; disabledByEnigma:boolean; }
export interface ParanormalThreatSheet {
  templateId:'ordem_paranormal';version:1;rulesetVersion:'1.3';systemId:string|null;kind:'paranormal'|'human'|'animal';challenge:number;element:string;size:string;descriptors:string;senses:string;movement:string;speed:number;defense:number;criticalImmune:boolean;
  attributes:ThreatAttributes;skills:ThreatSkill[];attacks:ThreatAttack[];abilities:ThreatAbility[];defenses:ThreatDefense[];conditionImmunities:string[];
  presence:{enabled:boolean;difficulty:number;damage:string;immuneNex:number|null};presenceTargets:string[];fastHealing:{amount:number;except:string[]|null};effort:number;maxEffort:number;enigma:string;enigmaSolved:boolean;state:Record<string,number>;
}
export interface ThreatCommand { action:string;option?:string|null;amount?:number;visibility?:'gm'|'private'|'campaign';half?:boolean;ignoreResistance?:boolean;source?:string;critical?:boolean;normalAmount?:number|null;targetId?:string|null;targetVersion?:number|null;additionalCreatures?:number; }
export const THREAT_ATTRIBUTES=[{key:'agilidade',label:'AGI'},{key:'forca',label:'FOR'},{key:'intelecto',label:'INT'},{key:'presenca',label:'PRE'},{key:'vigor',label:'VIG'}] as const;
export const THREAT_DAMAGE_TYPES=[{value:'balistico',label:'Balístico'},{value:'corte',label:'Corte'},{value:'impacto',label:'Impacto'},{value:'perfuracao',label:'Perfuração'},{value:'fogo',label:'Fogo'},{value:'frio',label:'Frio'},{value:'eletricidade',label:'Eletricidade'},{value:'quimico',label:'Químico'},{value:'mental',label:'Mental'},{value:'sangue',label:'Sangue'},{value:'morte',label:'Morte'},{value:'conhecimento',label:'Conhecimento'},{value:'energia',label:'Energia'},{value:'medo',label:'Medo'}];
export const THREAT_ELEMENTS=[{value:'none',label:'Nenhum'},...THREAT_DAMAGE_TYPES.filter(t=>['sangue','morte','conhecimento','energia','medo'].includes(t.value))];
export const THREAT_ACTIONS=[{value:'standard',label:'Padrão'},{value:'move',label:'Movimento'},{value:'full',label:'Completa'},{value:'reaction',label:'Reação'},{value:'free',label:'Livre'},{value:'passive',label:'Passiva'}];
export const THREAT_SIZES=[{value:'tiny',label:'Minúsculo'},{value:'small',label:'Pequeno'},{value:'medium',label:'Médio'},{value:'large',label:'Grande'},{value:'huge',label:'Enorme'},{value:'colossal',label:'Colossal'}];
export const THREAT_CONDITIONS=['abalado','agarrado','alquebrado','apavorado','asfixiado','atordoado','caido','cego','confuso','debilitado','desprevenido','doente','em_chamas','enjoado','enredado','envenenado','esmorecido','exausto','fascinado','fatigado','fraco','frustrado','imovel','inconsciente','indefeso','lento','ofuscado','paralisado','pasmo','petrificado','sangrando','sobrecarregado','surdo','surpreendido','vulneravel'];
export const threatId=(prefix:string)=>`${prefix}_${crypto.randomUUID().replaceAll('-','').slice(0,12)}`;
export function resolveThreatRenderer(sheet:ParanormalThreatSheet|null|undefined){return sheet?.templateId==='ordem_paranormal'&&sheet.rulesetVersion==='1.3'&&sheet.version===1?'paranormal-threat-v1':null;}
export function blankThreatSheet():ParanormalThreatSheet {return {
  templateId:'ordem_paranormal',version:1,rulesetVersion:'1.3',systemId:null,kind:'paranormal',challenge:0,element:'none',size:'medium',descriptors:'',senses:'',movement:'',speed:0,defense:10,criticalImmune:false,
  attributes:{agilidade:1,forca:1,intelecto:1,presenca:1,vigor:1},skills:[{id:'fortitude',name:'Fortitude',attribute:'vigor',bonus:0},{id:'reflexos',name:'Reflexos',attribute:'agilidade',bonus:0},{id:'vontade',name:'Vontade',attribute:'presenca',bonus:0},{id:'iniciativa',name:'Iniciativa',attribute:'agilidade',bonus:0},{id:'percepcao',name:'Percepção',attribute:'presenca',bonus:0}],
  attacks:[],abilities:[],defenses:[],conditionImmunities:[],presence:{enabled:false,difficulty:10,damage:'1d6',immuneNex:null},presenceTargets:[],fastHealing:{amount:0,except:[]},effort:0,maxEffort:0,enigma:'',enigmaSolved:false,state:{}
};}
export function freshThreat(sheet:ParanormalThreatSheet):ParanormalThreatSheet {const copy=structuredClone(sheet);copy.systemId=null;copy.state={};copy.presenceTargets=[];copy.enigmaSolved=false;copy.effort=copy.maxEffort;copy.abilities=copy.abilities.map(a=>({...a,remainingUses:a.maximumUses}));return copy;}

// Renderer safety for imported system files. The API remains authoritative for rule validation.
export function isThreatSheet(value:unknown):value is ParanormalThreatSheet {
  const object=(v:unknown):v is Record<string,unknown>=>!!v&&typeof v==='object'&&!Array.isArray(v);
  const numbers=(v:Record<string,unknown>,keys:string[])=>keys.every(k=>typeof v[k]==='number'&&Number.isFinite(v[k]));
  const strings=(v:Record<string,unknown>,keys:string[])=>keys.every(k=>typeof v[k]==='string');
  const stringArray=(v:unknown):v is string[]=>Array.isArray(v)&&v.every(i=>typeof i==='string');
  if(!object(value)||value['templateId']!=='ordem_paranormal'||value['version']!==1||value['rulesetVersion']!=='1.3'||!strings(value,['kind','element','size','descriptors','senses','movement','enigma'])||!numbers(value,['challenge','speed','defense','effort','maxEffort'])||typeof value['criticalImmune']!=='boolean'||typeof value['enigmaSolved']!=='boolean')return false;
  const attrs=value['attributes'],presence=value['presence'],healing=value['fastHealing'],state=value['state'];
  if(!object(attrs)||!numbers(attrs,THREAT_ATTRIBUTES.map(a=>a.key))||!object(state)||!Object.values(state).every(v=>typeof v==='number'&&Number.isInteger(v))||!stringArray(value['conditionImmunities'])||!stringArray(value['presenceTargets']))return false;
  if(!object(presence)||typeof presence['enabled']!=='boolean'||!numbers(presence,['difficulty'])||typeof presence['damage']!=='string'||!(presence['immuneNex']===null||typeof presence['immuneNex']==='number'))return false;
  if(!object(healing)||!numbers(healing,['amount'])||!(healing['except']==null||stringArray(healing['except'])))return false;
  const list=(key:string,check:(row:Record<string,unknown>)=>boolean)=>Array.isArray(value[key])&&value[key].every(row=>object(row)&&check(row));
  return list('skills',r=>strings(r,['id','name','attribute'])&&numbers(r,['bonus']))&&list('attacks',r=>strings(r,['id','name','attribute','style','damage','damageType','extraDamage','extraType','action','range','effect'])&&numbers(r,['bonus','count','margin','multiplier','ammunition','cost']))&&list('abilities',r=>strings(r,['id','name','action','roll','recharge','description'])&&numbers(r,['cost','maximumUses','remainingUses']))&&list('defenses',r=>strings(r,['id','type','effect','sourceKind','sourceId','source'])&&numbers(r,['amount'])&&typeof r['disabledByEnigma']==='boolean'&&(r['except']==null||stringArray(r['except'])));
}
