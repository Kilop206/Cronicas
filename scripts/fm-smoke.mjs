// Runs only against an isolated local API/DB. Never creates fixtures in production.
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';

const base=process.env.SMOKE_BASE_URL||'http://127.0.0.1:8082';
assert.ok(['127.0.0.1','localhost'].includes(new URL(base).hostname),'F&M smoke requires a local server');
const template=JSON.parse(await readFile(new URL('../.quality-local/fm/template.json',import.meta.url),'utf8'));
const email=`fm-${crypto.randomUUID()}@example.test`;
async function request(path,method='GET',body,cookie){
  const response=await fetch(new URL(path,base),{method,headers:{...(cookie?{cookie}:{}),...(body?{'Content-Type':'application/json'}:{})},...(body?{body:JSON.stringify(body)}:{})});
  const text=await response.text();
  return {status:response.status,body:text?JSON.parse(text):null,cookie:response.headers.get('set-cookie')?.split(';')[0]};
}
function expect(result,status){assert.equal(result.status,status,JSON.stringify(result.body));return result.body;}
const user=await request('/api/auth/register','POST',{email,displayName:'F&M local integration',password:'fm-smoke-local-only-123'});
expect(user,201);const cookie=user.cookie;assert.ok(cookie);
const catalog=expect(await request('/api/v1/rpg-plugins','GET',null,cookie),200);
assert.ok(JSON.stringify(catalog).includes('feiticeiros_maldicoes'));
const system=expect(await request('/api/systems','POST',{...template,name:'F&M integration',description:'Isolated CI fixture'},cookie),201);
const campaign=expect(await request('/api/campaigns','POST',{name:'F&M integration',description:'',accentColor:'#a491e5'},cookie),201);
expect(await request(`/api/campaigns/${campaign.id}/system`,'PATCH',{systemId:system.id},cookie),204);
const fields=Object.fromEntries(template.fields.filter(f=>f.defaultValue!==null&&f.defaultValue!==undefined).map(f=>[f.key,structuredClone(f.defaultValue)]));
Object.assign(fields,{nivel:8,especializacao:'combate',origem:'inato',atributo_especializacao:'forca',atributo_tecnica:'sabedoria',forca:16,constituicao:14,sabedoria:14,
  armas:[{nome:'Ensaio',atributo:'forca',dados:'1d6',apos:'',treinado:true,margem:20,acao:'comum',municao:-1}]});
let character=expect(await request(`/api/campaigns/${campaign.id}/characters`,'POST',{name:'F&M fixture',health:1,maxHealth:1,mana:0,maxMana:0,sanity:0,maxSanity:0,fields},cookie),201);
const path=`/api/characters/${character.id}`;
const describe=expect(await request(path+'/rules','GET',null,cookie),200);
assert.equal(describe.supported,true);assert.ok(describe.rules.actions.some(a=>a.id==='fm-preparation-recover'));
async function command(action,extra={}){
  const result=expect(await request(path+'/rules','POST',{expectedVersion:character.version,command:{action,...extra}},cookie),200);
  character=result.character;return result;
}
await command('fm-initialize');assert.equal(character.maxHealth,70);assert.equal(character.maxMana,32);
expect(await request(path+'/rules','POST',{expectedVersion:character.version-1,command:{action:'fm-turn-start'}},cookie),409);
await command('fm-turn-start');
await command('fm-attack',{option:'0',jujutsu:{attackKind:'melee',combatArt:'golpe_descendente'}});
const state=()=>Object.fromEntries(character.fields.estado_regras.map(r=>[r.chave,r.valor]));
assert.equal(state().preparo_gasto,1);assert.equal(state().acao_comum??0,0);
const persisted=expect(await request(path,'GET',null,cookie),200);assert.equal(persisted.version,character.version);assert.deepEqual(persisted.fields.estado_regras,character.fields.estado_regras);
const tampered={...character,fields:{...character.fields,estado_regras:[...character.fields.estado_regras.filter(r=>r.chave!=='preparo_gasto'),{chave:'preparo_gasto',valor:0}]}};
expect(await request(path,'PUT',tampered,cookie),400);
const revision=character.version;
const race=await Promise.all([1,2].map(amount=>request(path+'/rules','POST',{expectedVersion:revision,command:{action:'fm-damage',amount,option:'impacto'}},cookie)));
assert.deepEqual(race.map(r=>r.status).sort(),[200,409]);
character=expect(await request(path,'GET',null,cookie),200);assert.equal(character.version,revision+1);assert.ok([68,69].includes(character.health));
await command('fm-combat-end');await command('fm-short-rest');assert.equal(state().preparo_gasto??0,0);
expect(await request(path+'/rules','GET'),401);
console.log('F&M HTTP/PostgreSQL: real frontend schema, catalog, system/campaign/character creation, resources, martial context, ledger persistence, tamper rejection and atomic revision conflicts passed.');
