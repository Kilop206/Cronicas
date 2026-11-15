// Browser contract/layout checks against the production bundle with isolated API fixtures.
// This does not assert persistence, authentication or PostgreSQL integration.
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { readFile, mkdir } from 'node:fs/promises';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import ts from 'typescript';
import { chromium } from 'playwright';

const exports={};
vm.runInNewContext(ts.transpileModule(fs.readFileSync(new URL('./src/app/system-templates.ts',import.meta.url),'utf8'),{compilerOptions:{module:ts.ModuleKind.CommonJS}}).outputText,{exports});
const template=exports.SHEET_TEMPLATES.find(t=>t.id==='feiticeiros_maldicoes');
const fields=Object.fromEntries(template.fields.filter(f=>f.defaultValue!==null&&f.defaultValue!==undefined).map(f=>[f.key,structuredClone(f.defaultValue)]));
Object.assign(fields,{nivel:8,forca:16,destreza:14,constituicao:16,sabedoria:14,especializacao:'combate',origem:'inato',defesa:16,iniciativa:2,estado_regras:[{chave:'inicializado',valor:1},{chave:'pv_base',valor:78},{chave:'alma_max',valor:78},{chave:'alma_dano',valor:8}],armas:[{nome:'Espada de ensaio',atributo:'forca',dados:'1d6+1',apos:'1d4',treinado:true,margem:20,acao:'comum',municao:-1}]});
let character={id:'fm-test',campaignId:'fm-campaign',ownerId:'fm-owner',name:'Akari Moriyama',portraitUrl:null,backgroundUrl:null,health:56,maxHealth:70,mana:21,maxMana:32,sanity:0,maxSanity:0,canEdit:true,fields,version:1,systemVersionId:null,updatedAt:new Date().toISOString()};
const system={id:'fm-system',ownerId:'fm-owner',name:template.name,description:'',fields:template.fields,layout:template.layout,templateId:template.id,templateVersion:1,rulesetVersion:'2.5.2',rendererMode:'specialized',revision:1,revisions:[],npcTemplates:[]};
const campaign={id:'fm-campaign',name:'Ecos de Kyoto',description:'',role:'owner',status:'active',accentColor:'#a491e5',systemId:'fm-system'};
const user={id:'fm-owner',email:'fm@example.test',displayName:'Teste local',accent:'wine',bio:null,avatarUrl:null};
const root=path.resolve(import.meta.dirname,'dist/cronicas/browser');
const server=createServer(async(req,res)=>{
  const requested=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));
  if(!requested.startsWith(root+path.sep)){res.writeHead(404);res.end();return;}
  try{const file=path.extname(requested)?requested:path.join(root,'index.html');const body=await readFile(file);res.setHeader('Content-Type',file.endsWith('.js')?'application/javascript':file.endsWith('.css')?'text/css':'text/html');res.end(body);}catch{res.writeHead(404);res.end();}
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const base=`http://127.0.0.1:${server.address().port}`;
const browser=await chromium.launch({headless:true});
const output=path.resolve(import.meta.dirname,'../docs/visual-validation/rules');await mkdir(output,{recursive:true});
const errors=[];const commands=[];let rejectCommand=false;
try{
  const page=await browser.newPage();page.on('pageerror',e=>errors.push(e.message));
  await page.route('**/api/**',async route=>{
    const {pathname}=new URL(route.request().url());const method=route.request().method();let body=[];let status=200;
    if(pathname==='/api/auth/me')body=user;
    else if(pathname==='/api/characters/fm-test'&&method==='PUT'){character={...character,...route.request().postDataJSON(),version:character.version+1};body=character;}
    else if(pathname==='/api/characters/fm-test')body=character;
    else if(pathname==='/api/characters/fm-test/system'||pathname==='/api/systems/fm-system')body=system;
    else if(pathname==='/api/campaigns/fm-campaign')body=campaign;
    else if(pathname.endsWith('/favorite'))body={favorite:false};
    else if(pathname.endsWith('/secret'))body={notes:'',revision:1,updatedAt:null};
    else if(pathname.endsWith('/inventory'))body=[{id:'test-tool',characterId:'fm-test',name:'Ferramenta de ensaio',category:'equipment',quantity:1,weight:2,notes:'Item da fixture de impressão',version:1}];
    else if(pathname.endsWith('/rules')&&method==='GET')body={supported:true,rules:{edition:'Feiticeiros & Maldições · 2.5.2',actions:[{id:'fm-damage',label:'Receber dano por tipo',amountLabel:'Dano',options:[{value:'impacto',label:'Impacto'}]}],stats:[],settings:[],state:{}}};
    else if(pathname.endsWith('/rules')){
      const command=route.request().postDataJSON();commands.push(command);
      if(rejectCommand){status=400;body={errors:{rules:['Recurso insuficiente no teste de contrato.']}};}
      else{
        // UI fixtures expose returned state; the C# and HTTP suites verify the actual mechanics.
        const state=Object.fromEntries(character.fields.estado_regras.map(r=>[r.chave,r.valor]));const action=command.command.action;
        if(action==='fm-barrier')for(let i=1;i<=command.command.amount;i++)state['parede_'+i]=17;
        if(action==='fm-barrier-damage')state[command.command.option]=Math.max(0,state[command.command.option]-command.command.amount);
        if(action==='fm-curtain')state.cortina_1=51;
        if(action==='fm-basket'){state.cesta_durabilidade=4;state.concentrando=1;}
        if(action==='fm-turn-start')state.cesta_selo=command.command.jujutsu?.maintainBasketSeal?1:0;
        character={...character,fields:{...character.fields,estado_regras:Object.entries(state).filter(([,valor])=>valor!==0).map(([chave,valor])=>({chave,valor}))},version:character.version+1};body={character,message:'Ação recebida pela fixture de interface.',rolls:[{expression:'1d20+9',rolls:[15],keptIndexes:[0],modifier:9,total:24}]};
      }
    }
    else if(pathname.includes('negotiate')){status=503;body={};}
    await route.fulfill({status,contentType:'application/json',body:JSON.stringify(body)});
  });
  await page.goto(base+'/characters/fm-test');await page.locator('.custom-sheet-main .fm-sheet').waitFor();
  for(const size of [{name:'desktop',width:1440,height:900},{name:'wide',width:1920,height:1080},{name:'tablet',width:768,height:1024},{name:'mobile',width:390,height:844}]){
    await page.setViewportSize(size);
    await page.locator('.custom-sheet-main .fm-sheet').screenshot({path:path.join(output,`feiticeiros_maldicoes-${size.name}.png`)});
    assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),`${size.name}: page overflow`);
    for(const tab of ['Combate','Perfil Amaldiçoado','Progressão','Regras e índice']){
      await page.locator('.fm-tabs').getByRole('button',{name:tab,exact:true}).click();
      assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),`${size.name}: ${tab} overflow`);
    }
    await page.locator('.fm-tabs').getByRole('button',{name:'Feiticeiro',exact:true}).click();
  }
  await page.setViewportSize({width:1440,height:900});
  await page.getByRole('button',{name:'Editar ficha',exact:true}).click();
  const save=page.waitForResponse(r=>r.url().endsWith('/api/characters/fm-test')&&r.request().method()==='PUT');
  await page.locator('.fm-attributes').getByLabel('Força',{exact:true}).fill('18');await save;
  await page.reload();await page.locator('.custom-sheet-main .fm-sheet').waitFor();assert.equal(character.fields.forca,18);
  await page.getByRole('button',{name:'Editar ficha',exact:true}).click();
  await page.locator('.fm-tabs').getByRole('button',{name:'Perfil Amaldiçoado',exact:true}).click();
  await page.locator('.fm-builder').getByLabel('Nome',{exact:true}).fill('Rajada de ensaio');
  const spellSave=page.waitForResponse(r=>r.url().endsWith('/api/characters/fm-test')&&r.request().method()==='PUT');
  await page.getByRole('button',{name:'Criar pela tabela',exact:true}).click();await spellSave;
  assert.equal(character.fields.feiticos.at(-1).dados,'4d8');
  await page.locator('.fm-builder').getByLabel('Nome',{exact:true}).fill('Máxima de ensaio');
  await page.locator('.custom-sheet-main .fm-builder label').filter({hasText:'Nível'}).locator('select').selectOption({label:'Técnica Máxima'});
  const maximumSave=page.waitForResponse(r=>r.url().endsWith('/api/characters/fm-test')&&r.request().method()==='PUT');
  await page.getByRole('button',{name:'Criar pela tabela',exact:true}).click();await maximumSave;
  assert.equal(character.fields.feiticos.at(-1).dados,'20d12','maximum uses level-five table before access to level five');
  await page.locator('.fm-tabs').getByRole('button',{name:'Progressão',exact:true}).click();
  for(const style of ['Defensivo','Massivo']){
    const savedStyle=page.waitForResponse(r=>r.url().endsWith('/api/characters/fm-test')&&r.request().method()==='PUT');
    await page.locator('.custom-sheet-main .fm-choice-grid').getByRole('checkbox',{name:new RegExp('^'+style)}).check();await savedStyle;
  }
  assert.ok(character.fields.aptidoes.includes('estilo_defensivo')&&character.fields.aptidoes.includes('estilo_massivo'));
  assert.equal(await page.locator('.custom-sheet-main .fm-choice-grid').getByRole('checkbox',{name:/^Duelista/}).isDisabled(),true,'style learning budget');
  await page.getByRole('button',{name:'Concluir edição',exact:true}).click();
  await page.locator('.fm-tabs').getByRole('button',{name:'Combate',exact:true}).click();
  await page.locator('.custom-sheet-main').getByText('Contexto do ataque e do alvo',{exact:true}).click();
  await page.locator('.custom-sheet-main .fm-context select').first().selectOption('melee');
  await page.locator('.custom-sheet-main .fm-context label').filter({hasText:'Arte de combate'}).locator('select').selectOption('golpe_descendente');
  await page.locator('.custom-sheet-main .fm-context').getByRole('checkbox',{name:'Arma com propriedade Pesada',exact:true}).check();
  await page.locator('.custom-sheet-main .fm-special summary').click();
  await page.locator('.custom-sheet-main .fm-special label').filter({hasText:'Atroz'}).locator('select').selectOption({label:'Uma vez'});
  const attackResponse=page.waitForResponse(r=>r.url().endsWith('/rules')&&r.request().method()==='POST');
  await page.getByRole('button',{name:'Atacar · Espada de ensaio',exact:true}).click();
  await attackResponse;
  await page.locator('.fm-feedback[role=status]').waitFor();assert.equal(commands.at(-1).command.jujutsu.attackKind,'melee');
  assert.equal(commands.at(-1).command.jujutsu.combatArt,'golpe_descendente');
  assert.deepEqual(commands.at(-1).command.jujutsu.specialStrike,['atroz']);assert.equal(commands.at(-1).command.jujutsu.weaponHeavy,true);
  await page.locator('.custom-sheet-main .fm-received summary').click();
  await page.locator('.custom-sheet-main .fm-received').getByLabel('Dano antes das defesas',{exact:true}).fill('20');
  await page.locator('.custom-sheet-main .fm-received').getByLabel('RD ignorada pelo efeito',{exact:true}).fill('4');
  await page.locator('.custom-sheet-main .fm-received').getByRole('checkbox',{name:'O efeito ignora resistência (não imunidade)',exact:true}).check();
  const damageResponse=page.waitForResponse(r=>r.url().endsWith('/rules')&&r.request().method()==='POST');
  await page.getByRole('button',{name:'Aplicar dano recebido',exact:true}).click();await damageResponse;
  assert.equal(commands.at(-1).command.jujutsu.ignoreReduction,4);assert.equal(commands.at(-1).command.jujutsu.ignoreResistance,true);
  await page.locator('.custom-sheet-main .fm-sheet').screenshot({path:path.join(output,'feiticeiros_maldicoes-combate-desktop.png')});
  await page.setViewportSize({width:390,height:844});assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'expanded combat options mobile overflow');await page.setViewportSize({width:1440,height:900});
  await page.locator('.fm-tabs').getByRole('button',{name:'Perfil Amaldiçoado',exact:true}).click();
  await page.locator('.custom-sheet-main').getByText('Conjurar como ritual',{exact:true}).click();
  await page.locator('.custom-sheet-main .fm-context label').filter({hasText:'Modo de conjuração'}).locator('select').selectOption('simple');
  await page.locator('.custom-sheet-main .fm-context label').filter({hasText:'Aumento de dano'}).locator('select').selectOption({label:'Uma vez'});
  const ritualResponse=page.waitForResponse(r=>r.url().endsWith('/rules')&&r.request().method()==='POST');
  await page.getByRole('button',{name:'Conjurar · Rajada de ensaio',exact:true}).click();
  await ritualResponse;
  await page.locator('.fm-feedback[role=status]').waitFor();assert.equal(commands.at(-1).command.jujutsu.ritual,'simple');assert.deepEqual(commands.at(-1).command.jujutsu.improvements,['dano']);
  await page.locator('.custom-sheet-main .fm-sheet').screenshot({path:path.join(output,'feiticeiros_maldicoes-perfil-desktop.png')});
  await page.locator('.fm-tabs').getByRole('button',{name:'Feiticeiro',exact:true}).click();
  await page.getByRole('button',{name:'Rolar Atletismo',exact:true}).click();
  await page.locator('.fm-feedback[role=status]').waitFor();assert.equal(commands.at(-1).command.option,'per_atletismo');assert.equal(commands.at(-1).expectedVersion,character.version-1);
  rejectCommand=true;
  await page.getByRole('button',{name:'Rolar Atletismo',exact:true}).click();await page.locator('.fm-feedback[role=alert]').waitFor();
  assert.match(await page.locator('.fm-feedback[role=alert]').textContent(),/insuficiente/);assert.equal(await page.locator('.custom-sheet-main').getAttribute('inert'),null);
  await page.locator('.fm-tabs').getByRole('button',{name:'Regras e índice',exact:true}).click();
  await page.locator('.custom-sheet-main .fm-search input').fill('canalizar');
  await page.locator('.custom-sheet-main .fm-index li').first().waitFor();assert.ok(await page.locator('.custom-sheet-main .fm-index li').count()>0);
  await page.emulateMedia({media:'print'});
  await page.locator('.print-sheet .fm-sheet app-character-inventory').getByText('Ferramenta de ensaio',{exact:true}).waitFor({state:'visible'});
  assert.equal(await page.locator('.print-sheet .inventory-form').isVisible(),false,'print hides inventory editing, preserves items');
  await page.emulateMedia({media:'screen'});
  rejectCommand=false;character={...character,fields:{...character.fields,especializacao:'tecnica',atributo_especializacao:'sabedoria',aptidoes:['fundamento_potente','fundamento_preciso'],pericias:character.fields.pericias.map(r=>r.chave==='feiticaria'?{...r,treino:2}:r)}};
  await page.reload();await page.locator('.custom-sheet-main .fm-sheet').waitFor();
  await page.getByRole('button',{name:'Editar ficha',exact:true}).click();
  await page.locator('.fm-tabs').getByRole('button',{name:'Progressão',exact:true}).click();
  const adeptSave=page.waitForResponse(r=>r.url().endsWith('/api/characters/fm-test')&&r.request().method()==='PUT');
  await page.locator('.custom-sheet-main .fm-choice-grid').getByRole('checkbox',{name:/^Adepto de Feitiçaria/}).check();await adeptSave;
  const grantSave=page.waitForResponse(r=>r.url().endsWith('/api/characters/fm-test')&&r.request().method()==='PUT');
  await page.locator('.custom-sheet-main label').filter({hasText:'Mudança concedida por Adepto'}).locator('select').selectOption('distante');await grantSave;
  assert.ok(character.fields.aptidoes.includes('adepto_fundamento_distante'));
  await page.getByRole('button',{name:'Concluir edição',exact:true}).click();
  await page.locator('.fm-tabs').getByRole('button',{name:'Perfil Amaldiçoado',exact:true}).click();
  await page.locator('.custom-sheet-main .fm-fundamentals summary').click();
  await page.locator('.custom-sheet-main .fm-fundamentals label').filter({hasText:'Feitiço Potente'}).locator('select').selectOption({label:'Uma vez'});
  await page.locator('.custom-sheet-main .fm-fundamentals label').filter({hasText:'Feitiço Preciso'}).locator('select').selectOption({label:'Duas vezes'});
  await page.locator('.custom-sheet-main .fm-fundamentals label').filter({hasText:'Atributo de Feitiço Potente'}).locator('select').selectOption('sabedoria');
  await page.locator('.custom-sheet-main .fm-fundamentals label').filter({hasText:'Feitiço Distante'}).locator('select').selectOption({label:'Uma vez'});
  await page.locator('.custom-sheet-main .fm-fundamentals').getByRole('checkbox',{name:/^Usar desconto de Adepto/}).check();
  const fundamentalResponse=page.waitForResponse(r=>r.url().endsWith('/rules')&&r.request().method()==='POST');
  await page.getByRole('button',{name:'Conjurar · Rajada de ensaio',exact:true}).click();await fundamentalResponse;
  assert.deepEqual(commands.at(-1).command.jujutsu.fundamentals,['distante','potente','preciso','preciso']);assert.equal(commands.at(-1).command.jujutsu.spellPowerAttribute,'sabedoria');assert.equal(commands.at(-1).command.jujutsu.useAdeptDiscount,true);
  await page.locator('.custom-sheet-main .fm-sheet').screenshot({path:path.join(output,'feiticeiros_maldicoes-fundamentos-desktop.png')});
  await page.setViewportSize({width:390,height:844});assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'expanded fundamentals mobile overflow');await page.setViewportSize({width:1440,height:900});
  await page.getByRole('button',{name:'Editar ficha',exact:true}).click();await page.locator('.fm-tabs').getByRole('button',{name:'Progressão',exact:true}).click();
  const revokeSave=page.waitForResponse(r=>r.url().endsWith('/api/characters/fm-test')&&r.request().method()==='PUT');
  await page.locator('.custom-sheet-main .fm-choice-grid').getByRole('checkbox',{name:/^Adepto de Feitiçaria/}).uncheck();await revokeSave;
  assert.equal(character.fields.aptidoes.some(v=>v.startsWith('adepto_fundamento_')),false,'unlearning a grant clears its dependent choice');
  character={...character,fields:{...character.fields,apt_barreira:3,apt_dominio:2,pericias:[...character.fields.pericias.filter(r=>r.chave!=='historia'),{chave:'historia',treino:2,outros:0}]}};
  await page.reload();await page.locator('.custom-sheet-main .fm-sheet').waitFor();await page.getByRole('button',{name:'Editar ficha',exact:true}).click();
  await page.locator('.fm-tabs').getByRole('button',{name:'Perfil Amaldiçoado',exact:true}).click();
  for(const name of ['Técnicas de Barreira','Cortina','Cesta Oca de Vime','Domínio Simples']){
    const save=page.waitForResponse(r=>r.url().endsWith('/api/characters/fm-test')&&r.request().method()==='PUT');await page.locator('.custom-sheet-main').getByRole('checkbox',{name,exact:true}).check();await save;
  }
  await page.getByRole('button',{name:'Concluir edição',exact:true}).click();
  const barriers=page.locator('.custom-sheet-main .fm-barriers');await barriers.getByLabel('Paredes a erguer',{exact:true}).fill('2');
  let received=page.waitForResponse(r=>r.url().endsWith('/rules')&&r.request().method()==='POST');await barriers.getByRole('button',{name:'Erguer paredes',exact:true}).click();await received;
  assert.equal(commands.at(-1).command.amount,2);await barriers.getByRole('list',{name:'Barreiras ativas'}).getByText('Parede 1 · 17 PV',{exact:true}).waitFor();
  await barriers.locator('.fm-barrier-health summary').click();await barriers.getByLabel('Barreira',{exact:true}).selectOption('parede_1');await barriers.getByLabel('Dano final na barreira',{exact:true}).fill('5');
  received=page.waitForResponse(r=>r.url().endsWith('/rules')&&r.request().method()==='POST');await barriers.getByRole('button',{name:'Aplicar dano na barreira',exact:true}).click();await received;
  assert.equal(commands.at(-1).command.option,'parede_1');assert.equal(commands.at(-1).command.amount,5);await barriers.getByRole('list',{name:'Barreiras ativas'}).getByText('Parede 1 · 12 PV',{exact:true}).waitFor();
  await barriers.locator('.fm-curtain summary').click();assert.equal(await barriers.getByRole('button',{name:'Criar Cortina com ação acordada',exact:true}).isDisabled(),true);
  await barriers.getByLabel('Trechos de 9 m de cobertura',{exact:true}).fill('3');await barriers.getByLabel('Ação acordada com o narrador',{exact:true}).selectOption('completa');
  received=page.waitForResponse(r=>r.url().endsWith('/rules')&&r.request().method()==='POST');await barriers.getByRole('button',{name:'Criar Cortina com ação acordada',exact:true}).click();await received;
  assert.equal(commands.at(-1).command.option,'completa');assert.equal(commands.at(-1).command.amount,3);
  await barriers.getByLabel('Ação do anti-domínio',{exact:true}).selectOption('reacao');
  received=page.waitForResponse(r=>r.url().endsWith('/rules')&&r.request().method()==='POST');await barriers.getByRole('button',{name:'Ativar Cesta Oca de Vime · 3 PE',exact:true}).click();await received;assert.equal(commands.at(-1).command.option,'reacao');
  await page.locator('.fm-tabs').getByRole('button',{name:'Combate',exact:true}).click();
  await page.locator('.custom-sheet-main').getByRole('checkbox',{name:'Eu deveria ter sido atingido por acerto garantido',exact:true}).check();await page.locator('.custom-sheet-main').getByRole('checkbox',{name:'Manter selo da Cesta com as duas mãos',exact:true}).check();
  received=page.waitForResponse(r=>r.url().endsWith('/rules')&&r.request().method()==='POST');await page.getByRole('button',{name:'Iniciar meu turno',exact:true}).click();await received;
  assert.deepEqual(commands.at(-1).command.jujutsu,{guaranteedHitExposure:true,maintainBasketSeal:true});await page.locator('.custom-sheet-main').getByRole('checkbox',{name:'Eu deveria ter sido atingido por acerto garantido',exact:true}).waitFor();assert.equal(await page.locator('.custom-sheet-main').getByRole('checkbox',{name:'Eu deveria ter sido atingido por acerto garantido',exact:true}).isChecked(),false);
  await page.locator('.fm-tabs').getByRole('button',{name:'Perfil Amaldiçoado',exact:true}).click();await barriers.getByText(/selo mantido, duas mãos ocupadas/).waitFor();
  await barriers.screenshot({path:path.join(output,'feiticeiros_maldicoes-barreiras-desktop.png')});await page.setViewportSize({width:390,height:844});assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'barrier controls mobile overflow');
  await barriers.screenshot({path:path.join(output,'feiticeiros_maldicoes-barreiras-mobile.png')});await page.setViewportSize({width:1440,height:900});
  character={...character,canEdit:false};campaign.role='spectator';await page.reload();await page.locator('.custom-sheet-main .fm-sheet').waitFor();
  assert.equal(await page.getByRole('button',{name:'Editar ficha',exact:true}).count(),0);assert.equal(await page.getByRole('button',{name:'Rolar Atletismo',exact:true}).count(),0);
  await page.locator('.fm-tabs').getByRole('button',{name:'Perfil Amaldiçoado',exact:true}).click();assert.equal(await page.getByRole('button',{name:'Erguer paredes',exact:true}).count(),0);await page.locator('.custom-sheet-main .fm-barriers').getByText('Parede 1 · 12 PV',{exact:true}).waitFor();
  assert.deepEqual(errors,[]);console.log('F&M browser fixtures: four viewports, autosave, style budget, special strikes, typed incoming damage, fundamentals, rituals, wall/curtain HP, anti-domain reaction and seal, versioned command, error recovery, search, print and readonly passed.');
}finally{await browser.close();await new Promise(resolve=>server.close(resolve));}
