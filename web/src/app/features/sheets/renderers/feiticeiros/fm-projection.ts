import type { CharacterInput, SheetCellValue } from '../../../../api';
export const fmNumber=(c:CharacterInput,key:string,fallback=0)=>typeof c.fields[key]==='number'?c.fields[key] as number:fallback;
export const fmMod=(c:CharacterInput,key:string)=>Math.floor((fmNumber(c,key,10)-10)/2);
export const fmRows=(c:CharacterInput,key:string):Record<string,SheetCellValue>[]=>Array.isArray(c.fields[key])?(c.fields[key] as unknown[]).filter((v):v is Record<string,SheetCellValue>=>!!v && typeof v==='object' && !Array.isArray(v)):[];
export const fmState=(c:CharacterInput):Record<string,number>=>Object.fromEntries(fmRows(c,'estado_regras').map(r=>[String(r['chave']),Number(r['valor'])]));
export function fmSoulStage(c:CharacterInput){const s=fmState(c),max=s['alma_max']??c.maxHealth,current=max-(s['alma_dano']||0);return current*4<max?3:current*2<max?2:current*4<max*3?1:0;}
export function fmConditions(c:CharacterInput):Set<string>{
  const s=fmState(c),active=new Set(Object.entries(s).filter(([k,v])=>k.startsWith('cond_')&&v!==0).map(([k])=>k.slice(5)));
  if((s['exaustao']||0)>=2)active.add('desprevenido');
  if((s['exaustao']||0)>=3||fmSoulStage(c)>=2)active.add('exposto');
  if((s['exaustao']||0)>=4){active.add('condenado');active.add('desorientado');}
  if((s['exaustao']||0)>=5)active.add('enjoado');if(fmSoulStage(c)>=3)active.add('fragilizado');
  if(s['morrendo']||s['desmaiado']||s['morto'])active.add('inconsciente');
  if(s['ritual_mode']===2)active.add('desprevenido');
  let old=-1;while(old!==active.size){old=active.size;
    if(active.has('cego')){active.add('surpreso');active.add('lento');}
    if(['surpreso','agarrado','atordoado'].some(v=>active.has(v)))active.add('desprevenido');
    if(active.has('agarrado')||active.has('indefeso'))active.add('imovel');if(active.has('indefeso'))active.add('atordoado');if(active.has('inconsciente'))active.add('caido');
  }if(Array.isArray(c.fields['imunidades_condicao']))for(const condition of c.fields['imunidades_condicao'] as unknown[])if(typeof condition==='string')active.delete(condition);
  if(c.fields['origem']==='corpo_mutante')active.delete('envenenado');return active;
}
function fmClassLevel(c:CharacterInput,id:string){const multi=fmRows(c,'multiclasse');return multi.length?Number(multi.find(r=>r['classe']===id)?.['nivel']||0):c.fields['especializacao']===id?fmNumber(c,'nivel',1):0;}
export function fmDefense(c:CharacterInput,ranged=false){
  const s=fmState(c),conditions=fmConditions(c),restricted=fmClassLevel(c,'restringido'),overloaded=fmNumber(c,'carga_meios')>Math.max(0,8+2*fmMod(c,'forca'))*2;
  let penalty=conditions.has('paralisado')?10:conditions.has('desprevenido')||conditions.has('caido')&&!ranged?3:conditions.has('enredado')?2:0;
  if(overloaded)penalty=Math.max(5,penalty);
  const extra=restricted>0?Math.min(fmNumber(c,'nivel',1),Math.max(0,fmMod(c,c.fields['defesa_restrita']==='constituicao'?'constituicao':'forca')))+(restricted>=3?1:0)+(restricted>=9?1:0)+(restricted>=16?1:0):0;
  return fmNumber(c,'defesa',10)+extra+(s['defesa_empolgacao']||0)+(s['defesa_descendente']||0)-penalty-(s['exaustao']||0)-Math.min(5,Math.floor((s['sequencia']||0)/2))+(conditions.has('caido')&&ranged?3:0);
}
export function fmSpeed(c:CharacterInput){
  const s=fmState(c),conditions=fmConditions(c);if(['imovel','inconsciente','atordoado','paralisado'].some(v=>conditions.has(v)))return 0;
  let speed=fmNumber(c,'deslocamento',9)+(c.fields['origem']==='restringido'?3:0)+(fmClassLevel(c,'restringido')>=10?3:0)-1.5*(s['exaustao']||0)-(fmNumber(c,'carga_meios')>Math.max(0,8+2*fmMod(c,'forca'))*2?4.5:0)-(conditions.has('sofrendo')?3:0);
  if(conditions.has('lento')||conditions.has('enredado'))speed/=2;if(s['dominio']&&!s['confronto'])speed*=2;
  return Math.max(0,conditions.has('caido')?Math.min(4.5,speed):speed);
}
export function fmSkillBonus(c:CharacterInput,key:string,attribute:string,save=false){
  const row=fmRows(c,save?'resistencias':'pericias').find(r=>r['chave']===key),s=fmState(c),active=fmConditions(c);
  const level=fmNumber(c,'nivel',1),bt=2+Math.floor((level-1)/4),multi=fmRows(c,'multiclasse');
  const restricted=multi.length?Number(multi.find(r=>r['classe']==='restringido')?.['nivel']||0):c.fields['especializacao']==='restringido'?level:0;
  const training=restricted>=9&&save&&['fortitude','reflexos'].includes(key)?2:Number(row?.['treino']||0);
  let bonus=fmMod(c,attribute)+Math.floor(level/2)+(training>0?bt:0)+(training===2?restricted>=10&&['forca','destreza','constituicao'].includes(attribute)?bt:Math.floor(bt/2):0)+Number(row?.['outros']||0);
  if(restricted>=2&&!save)bonus+=restricted>=10?2:1;
  if(restricted>=3&&save&&key==='reflexos')bonus+=1+(restricted>=9?1:0)+(restricted>=16?1:0);
  if(save&&key==='fortitude'&&fmClassLevel(c,'lutador')>=5){const real=fmClassLevel(c,'lutador'),effect=real+Math.floor((level-real)/2);bonus+=1+(effect>=9?1:0)+(effect>=13?1:0)+(effect>=17?1:0);}
  bonus-=(s['exaustao']||0)+[0,3,6,8][fmSoulStage(c)]+Math.max(active.has('envenenado')?2:0,!save?(active.has('amedrontado')?3:active.has('abalado')?1:0):0);
  if(save&&key==='reflexos'&&active.has('desprevenido'))bonus-=3;
  if(!save&&attribute==='destreza')bonus-=fmNumber(c,'penalidade_uniforme')+fmNumber(c,'penalidade_escudo');
  if(!save&&key==='percepcao'&&active.has('cego'))bonus-=5;if(!save&&key==='furtividade'&&active.has('invisivel'))bonus+=10;
  return bonus;
}
