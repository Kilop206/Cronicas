// Visual regressions against the production bundle with synthetic API responses, never personal campaigns.
import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {readFile,mkdir,writeFile} from 'node:fs/promises';
import path from 'node:path';
import vm from 'node:vm';
import ts from 'typescript';
import {chromium} from 'playwright';

const root=path.resolve(import.meta.dirname,'dist/cronicas/browser');
const output=path.resolve(import.meta.dirname,'../.quality-local/visual-audit',process.env.AUDIT_CAPTURE||'current');await mkdir(output,{recursive:true});
const templateExports={};vm.runInNewContext(ts.transpileModule(await readFile(new URL('./src/app/system-templates.ts',import.meta.url),'utf8'),{compilerOptions:{module:ts.ModuleKind.CommonJS}}).outputText,{exports:templateExports});
const templates=templateExports.SHEET_TEMPLATES;
const now='2026-11-15T12:30:00Z';const title='A biblioteca dos caminhos esquecidos e os guardiões da última fronteira';
const longText='Registros da campanha, escolhas dos personagens e consequências da última sessão. '+title+'. '+('Uma história compartilhada permanece no arquivo. ').repeat(3);
const user={id:'visual-user',displayName:'Alexandre dos Caminhos da Última Fronteira',email:'personagem.de.ensaio@example.test',bio:longText,avatarUrl:null,accent:'wine',createdAt:now};
const campaign={id:'visual-campaign',name:title,description:longText,status:'active',role:'owner',ownerId:user.id,accentColor:'#a54d52',systemId:'system-ordem_paranormal',worldId:'visual-world',updatedAt:now};
const systems=templates.map(t=>({id:'system-'+t.id,ownerId:user.id,name:t.name,description:longText,fields:t.fields,layout:t.layout,templateId:t.id,templateVersion:t.version,rulesetVersion:t.rulesetVersion,rendererMode:'specialized',revision:1,revisions:[],npcTemplates:[],createdAt:now,updatedAt:now}));
const characters=systems.map(s=>({id:'char-'+s.templateId,name:'Helena dos Caminhos de Valença e da Última Fronteira',campaignId:campaign.id,ownerId:user.id,canEdit:true,portraitUrl:null,backgroundUrl:null,health:28,maxHealth:40,mana:12,maxMana:24,sanity:40,maxSanity:60,version:1,systemVersionId:null,updatedAt:now,fields:Object.fromEntries(s.fields.filter(f=>f.defaultValue!==null&&f.defaultValue!==undefined).map(f=>[f.key,structuredClone(f.defaultValue)]))}));
const freeCharacter={...characters[0],id:'char-free',name:'Personagem de ficha livre',fields:{observacoes:longText}};
const world={id:'visual-world',ownerId:user.id,name:title,description:longText,createdAt:now,updatedAt:now,campaigns:[{campaignId:campaign.id,name:campaign.name,role:'owner'}]};
const article={id:'visual-article',worldId:world.id,authorId:user.id,title,body:longText+'\nhttps://example.test/arquivo/'+('caminho-sem-espacos-'.repeat(12)),category:'location',visibility:'campaign',revision:1,createdAt:now,updatedAt:now};
const npc={id:'visual-npc',campaignId:campaign.id,name:'Guardião da biblioteca dos caminhos e dos mapas esquecidos',portraitUrl:null,appearance:longText,personality:longText,motivation:longText,relationships:longText,location:title,secrets:longText,status:'active',visibility:'gm',health:32,maxHealth:48,version:1,createdAt:now,updatedAt:now};
const encounter={id:'visual-encounter',campaignId:campaign.id,title,status:'active',round:12,turnIndex:0,createdAt:now,updatedAt:now,participants:[{id:'member-one',characterId:characters[0].id,npcId:null,name:characters[0].name,initiative:23,health:28,maxHealth:40,conditions:'Desprevenido, envenenado e sob efeito de uma habilidade prolongada'},{id:'member-two',characterId:null,npcId:npc.id,name:npc.name,initiative:18,health:32,maxHealth:48,conditions:'Sem condições'}]};
const entry=kind=>({id:'entry-'+kind,campaignId:campaign.id,authorId:user.id,kind,title,body:article.body,category:'general',visibility:'campaign',revision:1,eventAt:now,revealedAt:now,createdAt:now,updatedAt:now});
const pageOf=items=>({items,total:items.length,page:1});
const compactName='Personagem'.repeat(8);
const failure={detail:'Não foi possível salvar o registro '+compactName+'. Revise os campos e tente novamente.'};
const unbroken=value=>Array.isArray(value)?value.map(unbroken):value&&typeof value==='object'?Object.fromEntries(Object.entries(value).map(([key,item])=>[key,['name','title','displayName'].includes(key)&&typeof item==='string'?compactName:key==='email'?'arquivo.'+compactName+'@example.test':unbroken(item)])):value;
const openDelete=async p=>{await p.getByRole('button',{name:'Excluir ficha',exact:true}).click();await p.locator('dialog[open] input:not([placeholder=""])').waitFor();};
const extraCases=[
  ...[['campaign','/campaigns/'+campaign.id],['profile','/profile'],['world',`/worlds/${world.id}?article=${article.id}`],['sessions',`/campaigns/${campaign.id}/sessions?entry=entry-sessions`],['characters',`/campaigns/${campaign.id}/characters`],['systems','/systems']].map(([name,url])=>({name:'extra-unbroken-'+name,url,unbroken:true})),
  ...[['campaigns','/campaigns','/api/campaigns'],['records',`/campaigns/${campaign.id}/sessions`,'/api/campaigns/'+campaign.id+'/entries/sessions'],['articles',`/worlds/${world.id}`,'/api/worlds/'+world.id+'/articles']].map(([name,url,endpoint])=>({name:'extra-pagination-'+name,url,reply:(p,m,body)=>p===endpoint?{body:{...body,total:61}}:null})),
  ...[['campaigns','/campaigns','/api/campaigns'],['characters',`/campaigns/${campaign.id}/characters`,'/api/campaigns/'+campaign.id+'/characters'],['worlds','/worlds','/api/worlds']].map(([name,url,endpoint])=>({name:'extra-empty-'+name,url,reply:p=>p===endpoint?{body:name==='campaigns'?pageOf([]):[]}:null})),
  {name:'extra-load-error',url:'/campaigns',reply:p=>p==='/api/campaigns'?{status:400,body:failure}:null},
  {name:'extra-login-error',url:'/login',guest:true,reply:(p,m)=>p==='/api/auth/login'&&m==='POST'?{status:400,body:failure}:null,after:async p=>{await p.locator('input[name=email]').fill('ensaio@example.test');await p.locator('input[name=password]').fill('senha-de-ensaio');await p.getByRole('button',{name:'Entrar',exact:true}).click();await p.locator('.form-error').waitFor();}},
  {name:'extra-campaign-error',url:'/campaigns/new',reply:(p,m)=>p==='/api/campaigns'&&m==='POST'?{status:400,body:failure}:null,after:async p=>{await p.locator('input[name=name]').fill('Uma campanha de ensaio');await p.getByRole('button',{name:'Criar campanha',exact:false}).click();await p.locator('.form-error').waitFor();}},
  {name:'extra-profile-error',url:'/profile',reply:(p,m)=>p==='/api/profile'&&m==='PATCH'?{status:400,body:failure}:null,after:async p=>{await p.getByRole('button',{name:'Salvar perfil',exact:true}).click();await p.locator('.form-error').waitFor();}},
  {name:'extra-record-editor',url:`/campaigns/${campaign.id}/sessions?entry=entry-sessions`,after:async p=>{await p.getByRole('button',{name:'Editar',exact:true}).click();await p.locator('input[name=eventAt]').fill('2026-11-16T19:30');}},
  {name:'extra-article-editor',url:`/worlds/${world.id}?article=${article.id}`,after:async p=>{await p.getByRole('button',{name:'Editar artigo',exact:true}).click();}},
  {name:'extra-inventory-edit',url:'/characters/char-free',unbroken:true,after:async p=>{await p.locator('.sheet-tabs').getByRole('button',{name:'Inventário',exact:true}).click();await p.locator('.inventory-actions').getByRole('button',{name:'Editar',exact:true}).click();}},
  {name:'extra-ability-edit',url:'/characters/char-free',unbroken:true,after:async p=>{await p.locator('.sheet-tabs').getByRole('button',{name:'Habilidades',exact:true}).click();await p.locator('.ability-actions').getByRole('button',{name:'Editar',exact:true}).click();}},
  {name:'extra-delete-dialog',url:'/characters/char-free',unbroken:true,height:560,after:openDelete},
  {name:'extra-delete-error',url:'/characters/char-free',height:560,reply:(p,m)=>p.includes('/characters/')&&m==='DELETE'?{status:409,body:failure}:null,after:async p=>{await openDelete(p);const input=p.locator('dialog input');await input.fill(await input.getAttribute('placeholder'));await p.getByRole('button',{name:'Excluir permanentemente',exact:true}).click();await p.locator('dialog .form-error').waitFor();}},
  {name:'extra-world-links',url:`/worlds/${world.id}?article=${article.id}`,reply:p=>p.endsWith('/links')?{body:[{id:'visual-link',direction:'outgoing',label:compactName,article:{...article,id:'other-article',title:compactName}}]}:null}
];
const server=createServer(async(req,res)=>{const file=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));if(file!==root&&!file.startsWith(root+path.sep)){res.writeHead(404);res.end();return;}try{const target=path.extname(file)?file:path.join(root,'index.html');res.setHeader('Content-Type',target.endsWith('.js')?'application/javascript':target.endsWith('.css')?'text/css':'text/html');res.end(await readFile(target));}catch{res.writeHead(404);res.end();}});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));const base=`http://127.0.0.1:${server.address().port}`;
const cases=[
  {name:'home',url:'/',guest:true},{name:'login',url:'/login',guest:true,after:async p=>{const link=await p.locator('.forgot-link').boundingBox();const submit=await p.locator('.auth-box form button').boundingBox();assert.ok(link.y>=submit.y+submit.height+8,'password recovery stays separated from the login button');}},{name:'register',url:'/register',guest:true},{name:'forgot',url:'/forgot-password',guest:true},{name:'reset',url:'/reset-password?email=ensaio@example.test&code=123456',guest:true},{name:'privacy',url:'/privacy',guest:true},{name:'missing',url:'/nao-existe',guest:true},
  {name:'dashboard',url:'/dashboard'},{name:'campaigns',url:'/campaigns'},{name:'campaign-new',url:'/campaigns/new'},{name:'campaign',url:'/campaigns/'+campaign.id,after:async p=>{await p.getByRole('button',{name:'Gerar convite',exact:true}).click();await p.locator('.invite-result code').waitFor();}},
  {name:'characters',url:`/campaigns/${campaign.id}/characters`},{name:'free-sheet',url:'/characters/char-free',after:async p=>{const input=p.locator('.manual-field input').first();await input.fill('Uma nota');await input.press('End');await input.pressSequentially(' continua');assert.equal(await input.inputValue(),'Uma nota continua');assert.equal(await input.evaluate(e=>e===document.activeElement),true,'typing keeps focus in a free sheet');}},
  {name:'npcs',url:`/campaigns/${campaign.id}/npcs?npc=${npc.id}`},{name:'npc-edit',url:`/campaigns/${campaign.id}/npcs?npc=${npc.id}`,after:async p=>{await p.getByRole('button',{name:'Editar NPC',exact:true}).click();}},
  {name:'threat-create',url:`/campaigns/${campaign.id}/npcs`,after:async p=>{await p.getByRole('button',{name:'Novo inimigo',exact:true}).click();}},
  ...['sessions','documents','wiki'].map(kind=>({name:kind,url:`/campaigns/${campaign.id}/${kind}?entry=entry-${kind}`})),
  {name:'encounters',url:`/campaigns/${campaign.id}/encounters`,after:async p=>{await p.locator('.narrative-row').first().click();await p.locator('.initiative-row').first().waitFor();}},
  {name:'worlds',url:'/worlds'},{name:'world',url:`/worlds/${world.id}?article=${article.id}`},{name:'systems',url:'/systems'},{name:'system-editor',url:'/systems/new?preset=ordem_paranormal'},
  {name:'search',url:'/search?q=caminhos'},{name:'profile',url:'/profile'},{name:'settings',url:'/settings'},{name:'invite',url:'/invites/visual-token'},{name:'room',url:`/campaigns/${campaign.id}/room`},
  {name:'navigation-open',url:'/dashboard',after:async p=>{const menu=p.locator('.mobile-site-menu');if(await menu.isVisible())await menu.locator('summary').click();else await p.locator('.collapse').click();}},
  {name:'sheet-tools',url:'/characters/char-dnd_5e',after:async p=>{await p.locator('.sheet-utilities > summary').click();await p.locator('.sheet-utilities .obs-actions > summary').first().click();assert.equal(await p.locator('input[type=file][hidden]').isVisible(),false,'native import field stays hidden');}},
  ...['Inventário','Habilidades','Mestre'].map((label,index)=>({name:'free-tab-'+index,url:'/characters/char-free',after:async p=>{await p.locator('.sheet-tabs').getByRole('button',{name:label,exact:true}).click();}})),
  ...['Combate','Inventário','Habilidades','Rituais','Anotações'].map((label,index)=>({name:'paranormal-tab-'+index,url:'/characters/char-ordem_paranormal',after:async p=>{await p.locator('.paranormal-controls').getByRole('button',{name:label,exact:true}).click();}})),
  ...[
    ['feiticeiros_maldicoes','.fm-tabs',['Combate','Perfil Amaldiçoado','Progressão','Equipamento','Regras e índice','Crônica']],
    ['dnd_5e','.dnd-tabs',['SPELLS','INVENTORY','FEATURES']],
    ['tormenta20','.t20-navigation',['Combate','Poderes e magias','Equipamento','Crônica']],
    ['call_of_cthulhu_7e','.coc-toolbar',['Combate','Investigação','Pertences','Desenvolvimento']]
  ].flatMap(([id,selector,labels])=>labels.map((label,index)=>({name:id+'-tab-'+index,url:'/characters/char-'+id,after:async p=>{await p.locator(selector).getByRole('button',{name:label,exact:true}).click();}}))),
  ...templates.flatMap(t=>[{name:'sheet-'+t.id,url:'/characters/char-'+t.id},{name:'sheet-'+t.id+'-edit',url:'/characters/char-'+t.id,after:async p=>{await p.getByRole('button',{name:'Editar ficha',exact:true}).click();}}]),...extraCases
].filter(c=>!process.env.AUDIT_FILTER||c.name.includes(process.env.AUDIT_FILTER));
const sizes=(process.env.AUDIT_WIDTHS||'320,390,768,1440').split(',').map(Number);const browser=await chromium.launch({headless:true});const results=[];
try{
  for(const test of cases)for(const width of sizes){
    const page=await browser.newPage({viewport:{width,height:test.height||900},reducedMotion:'reduce'});page.setDefaultTimeout(10000);const errors=[];page.on('pageerror',e=>errors.push(e.message));page.on('dialog',d=>d.accept());
    await page.route('**/api/**',async route=>{
      const u=new URL(route.request().url());const p=u.pathname;let body=[];let status=200;
      const character=[...characters,freeCharacter].find(c=>p.includes('/characters/'+c.id));
      if(p==='/api/auth/me'){status=test.guest?401:200;body=test.guest?{}:user;}
      else if(p==='/api/dashboard')body={totalCampaigns:3,activeCampaigns:2,recentCampaigns:[campaign,{...campaign,id:'other-campaign',name:'Outra história compartilhada'}],favoriteCharacters:[{characterId:characters[0].id,name:characters[0].name,campaignId:campaign.id,campaignName:campaign.name,portraitUrl:null,health:28,maxHealth:40}],upcomingSessions:[{id:'upcoming',campaignId:campaign.id,campaignName:campaign.name,title,eventAt:now}]};
      else if(p==='/api/campaigns')body=pageOf([campaign]);
      else if(p==='/api/campaigns/'+campaign.id)body=campaign;
      else if(p.endsWith('/members'))body=[{userId:user.id,displayName:user.displayName,role:'owner',joinedAt:now},{userId:'member-other',displayName:'Personagem com um nome bastante comprido da campanha',role:'player',joinedAt:now}];
      else if(p.endsWith('/characters'))body=[...characters,freeCharacter];
      else if(character){
        if(p.endsWith('/system'))body=character.id==='char-free'?null:systems.find(s=>'char-'+s.templateId===character.id);
        else if(p.endsWith('/favorite'))body={favorite:false};else if(p.endsWith('/secret'))body={notes:longText.trim(),revision:1,updatedAt:now};
        else if(p.endsWith('/inventory'))body=[{id:'item',characterId:character.id,name:title,category:'equipment',quantity:2,weight:1.5,notes:longText,version:1}];
        else if(p.endsWith('/abilities'))body=[{id:'ability',characterId:character.id,name:title,description:longText,cost:'2 pontos de energia',effect:longText,duration:'Até o fim da próxima rodada',limitations:longText,recharge:'Descanso longo',rollExpression:'1d20+7',version:1}];
        else if(p.endsWith('/revisions'))body=[{id:'revision',version:1,createdAt:now,editedBy:user.displayName}];
        else if(p.endsWith('/rules'))body={supported:true,rules:{edition:'Regras da ficha',actions:[{id:'damage',label:'Receber dano',amountLabel:'Quantidade',options:[{value:'impacto',label:'Impacto físico'}]},{id:'rest',label:'Descanso completo'}],stats:[],settings:[],state:{}}};
        else body=character;
      }
      else if(p==='/api/systems')body=systems;else if(p.startsWith('/api/systems/'))body=systems.find(s=>p.endsWith(s.id))||systems[0];
      else if(p==='/api/worlds')body=[world];else if(p==='/api/worlds/'+world.id)body=world;else if(p.endsWith('/articles'))body=pageOf([article]);else if(p==='/api/world-articles/'+article.id)body=article;
      else if(p.startsWith('/api/entries/entry-'))body=entry(p.slice('/api/entries/entry-'.length));else if(p.includes('/entries/'))body=pageOf([entry(p.split('/').at(-1))]);
      else if(p.endsWith('/npcs'))body=pageOf([npc]);else if(p==='/api/npcs/'+npc.id)body=npc;else if(p.endsWith('/encounters'))body=[encounter];else if(p==='/api/encounters/'+encounter.id)body=encounter;
      else if(p==='/api/search')body={campaigns:[campaign],characters:[{...characters[0],campaignName:title}],npcs:[{...npc,campaignName:title}],worlds:[world],articles:[{...article,worldName:title}],entries:[{...entry('sessions'),campaignName:title}]};
      else if(p==='/api/auth/sessions')body=[{id:'session',createdAt:now,lastSeenAt:now,expiresAt:now,userAgent:'Mozilla/5.0 · Navegador de teste com identificação prolongada',ipAddress:'127.0.0.1',current:true}];
      else if(p.startsWith('/api/invites/'))body={campaignName:title,role:'player',expiresAt:now};else if(p.endsWith('/invites'))body={token:'token-'.repeat(18)};
      else if(p.includes('negotiate')){status=503;body={};}
      const replacement=test.reply?.(p,route.request().method(),body,status);if(replacement){body=replacement.body;status=replacement.status||200;}if(test.unbroken)body=unbroken(body);
      await route.fulfill({status,contentType:'application/json',body:JSON.stringify(body)});
    });
    try{
      await page.goto(base+test.url,{waitUntil:'load'});await page.locator('main:not(.boot-loading)').first().waitFor();await page.waitForFunction(()=>[...document.querySelectorAll('link[rel=stylesheet]')].filter(l=>l.href.includes('/styles-')).every(l=>l.sheet&&l.media!=='print'));await page.locator('.loading').first().waitFor({state:'hidden'});
      if(test.after)await test.after(page);
      await page.evaluate(()=>document.fonts.ready);await page.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));
      const metrics=await page.evaluate(()=>{
        const modal=document.querySelector('dialog[open]');
        const visible=e=>{const r=e.getBoundingClientRect();const s=getComputedStyle(e);return (!modal||modal.contains(e))&&e.checkVisibility({visibilityProperty:true})&&r.width>0&&r.height>0&&s.visibility!=='hidden'&&s.display!=='none'&&!e.closest('[inert],.print-sheet');};
        const label=e=>(e.getAttribute('aria-label')||e.textContent||e.tagName).trim().replace(/\s+/g,' ').slice(0,95);
        const nodes=[...document.querySelectorAll('main *')].filter(visible);
        const overflow=nodes.filter(e=>{const r=e.getBoundingClientRect();if(r.right<=innerWidth+1&&r.left>=-1)return false;let p=e.parentElement;while(p&&p.tagName!=='MAIN'){if(['auto','scroll','hidden','clip'].includes(getComputedStyle(p).overflowX))return false;p=p.parentElement;}return true;}).slice(0,12).map(e=>({tag:e.tagName,class:e.className,label:label(e),width:Math.round(e.getBoundingClientRect().width)}));
        const actions=nodes.filter(e=>e.matches('button,a.button,a.text-link,a.forgot-link,input,select,textarea,summary')&&!e.matches('[type=hidden],[type=checkbox],[type=radio],[type=color]'));
        const navigation=[...document.querySelectorAll('.mobile-bar :is(button,summary),.sidebar button,.topbar button')].filter(visible);
        const small=[...actions,...navigation].filter(e=>e.getBoundingClientRect().height<(innerWidth<=700?43.5:28)).slice(0,15).map(e=>({tag:e.tagName,class:e.className,label:label(e),height:Math.round(e.getBoundingClientRect().height)}));
        const clippedRect=e=>{const r=e.getBoundingClientRect();const rect={left:r.left,right:r.right,top:r.top,bottom:r.bottom};for(let p=e.parentElement;p&&p.tagName!=='MAIN';p=p.parentElement){const s=getComputedStyle(p),b=p.getBoundingClientRect();if(['auto','scroll','hidden','clip'].includes(s.overflowX)){rect.left=Math.max(rect.left,b.left);rect.right=Math.min(rect.right,b.right);}if(['auto','scroll','hidden','clip'].includes(s.overflowY)){rect.top=Math.max(rect.top,b.top);rect.bottom=Math.min(rect.bottom,b.bottom);}}return rect;};
        const collisions=[];const rectangles=actions.map(clippedRect);for(let i=0;i<actions.length;i++)for(let j=i+1;j<actions.length;j++){const a=actions[i],b=actions[j];if(a.contains(b)||b.contains(a))continue;const x=rectangles[i],y=rectangles[j];if(Math.min(x.right,y.right)-Math.max(x.left,y.left)>2&&Math.min(x.bottom,y.bottom)-Math.max(x.top,y.top)>2)collisions.push([label(a),label(b)]);}
        const textOverflow=nodes.filter(e=>e.matches('.campaign-title h1,.panel-heading h2,.narrative-row strong,.inventory-row strong,.ability-head h3,.world-link-row,.notice,.form-error')&&e.clientWidth>0&&getComputedStyle(e).textOverflow!=='ellipsis'&&e.scrollWidth>e.clientWidth+2).map(e=>({label:label(e),class:e.className}));
        const crampedHeadings=[];for(const section of nodes.filter(e=>e.matches('.section-heading'))){const heading=section.querySelector(':scope > div,:scope > h2,:scope > h3');if(!heading||!visible(heading))continue;const h=heading.getBoundingClientRect();for(const action of section.querySelectorAll(':scope > button,:scope > a')){if(!visible(action))continue;const a=action.getBoundingClientRect();if(a.top>=h.bottom-2&&a.top-h.bottom<8)crampedHeadings.push(label(heading));}}
        return {pageOverflow:document.documentElement.scrollWidth>innerWidth+1,overflow,small,collisions,textOverflow,crampedHeadings};
      });
      const result={name:test.name,width,...metrics,errors};results.push(result);
      if(process.env.AUDIT_SCREENSHOTS!=='0'&&(width===390||width===1440)){await page.screenshot({path:path.join(output,test.name+'-'+width+'.png'),fullPage:!test.name.includes('delete-')&&!test.name.includes('sheet')&&!test.name.includes('-tab-')&&!test.name.startsWith('system-editor')&&!test.name.startsWith('threat-'),animations:'disabled'});if(test.name.includes('sheet-')||test.name==='sheet-tools')await page.locator('main').screenshot({path:path.join(output,test.name+'-'+width+'-content.png'),animations:'disabled'});}
      console.log(JSON.stringify({name:test.name,width,overflow:metrics.pageOverflow,small:metrics.small.length,collisions:metrics.collisions.length,textOverflow:metrics.textOverflow.length,crampedHeadings:metrics.crampedHeadings.length,errors:errors.length}));
    }catch(error){await writeFile(path.join(output,test.name+'-'+width+'-failure.html'),await page.content());results.push({name:test.name,width,failure:error.message,errors});console.log(JSON.stringify({name:test.name,width,failure:error.message.split('\n')[0]}));}
    finally{await page.close();}
  }
  await writeFile(path.join(output,'report.json'),JSON.stringify(results,null,2));
  if(process.argv.includes('--strict'))assert.deepEqual(results.filter(r=>r.failure||r.pageOverflow||r.small?.length||r.collisions?.length||r.textOverflow?.length||r.crampedHeadings?.length||r.errors.length).map(r=>({name:r.name,width:r.width,failure:r.failure?.split('\n')[0],overflow:r.pageOverflow,small:r.small,collisions:r.collisions,textOverflow:r.textOverflow,crampedHeadings:r.crampedHeadings,errors:r.errors})),[],'visual audit: usable controls, readable text, spaced headings, no runtime failures, overlapping actions or horizontal page overflow');
}finally{await browser.close();await new Promise(resolve=>server.close(resolve));}
