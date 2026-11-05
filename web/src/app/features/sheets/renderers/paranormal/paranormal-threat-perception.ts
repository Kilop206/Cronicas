import type {ParanormalThreatSheet} from './paranormal-threat';
import {blankSensoryAbilities,blankSensoryContext,threatImmunities} from './paranormal-threat';
import {d20ConditionState} from '../../rules/d20-condition-display';

// Read-only preview. The API independently validates and resolves every actual action.
export function threatPerception(sheet:ParanormalThreatSheet,health:number){
  const c=sheet.sensoryContext||blankSensoryContext(),a=sheet.sensoryAbilities||blankSensoryAbilities();
  const active=d20ConditionState({estado_regras:Object.entries(sheet.state).map(([chave,valor])=>({chave,valor}))},health,threatImmunities(sheet));
  const blind=active.has('cego'),concealment=c.active?c.concealment:sheet.state['camuflagem_alvo'];
  const unseen=blind||concealment===2||c.active&&c.opponentUnseen;
  let chance=unseen?50:concealment===1?20:0,visual=!blind&&(!unseen||c.active&&c.cause==='darkness'&&!c.opponentUnseen&&concealment<2),perceives=!unseen,protects=false;
  const applied:string[]=[];
  const inRange=(range:number)=>c.active&&!c.unavailable&&c.distance!==null&&c.distance<=range;
  if(c.active&&!blind&&c.cause==='darkness'&&(a.darkvision&&inRange(18)||a.lowLightVision&&inRange(9)&&concealment<2&&!c.opponentUnseen)){
    chance=0;visual=true;perceives=true;protects=true;applied.push(a.darkvision&&inRange(18)?'Visão no Escuro':'Visão na Penumbra');
  }
  if(c.active&&a.blindsightRange>0&&inRange(a.blindsightRange)&&['darkness','paranormal-darkness','invisibility','blindness'].includes(c.cause)){
    chance=0;perceives=true;protects=true;applied.push('Percepção às Cegas');
  }
  if(a.scent&&inRange(9)&&unseen){chance=Math.min(chance,20);perceives=true;protects=true;applied.push('Faro');}
  if(sheet.state['ignora_camuflagem']){chance=0;applied.push('Efeito confirmado ignora camuflagem');}
  const configured=a.scent||a.blindsightRange>0||a.lowLightVision||a.darkvision;
  return {missChance:chance,perceivesOpponent:perceives,visualCheckAvailable:visual,protectsUnseen:protects,applied,
    warning:c.active&&configured&&c.distance===null?'Distância desconhecida: benefícios de sentidos com alcance não foram aplicados.':null,
    relationExemptions:new Set(c.active&&protects?['cego:desprevenido']:[]),contextConditions:c.active&&!perceives?['desprevenido']:[]};
}
