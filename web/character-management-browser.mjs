// Isolated browser contracts/layout. Persistence and permissions are checked by the HTTP/PostgreSQL smoke.
import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {readFile,mkdir} from 'node:fs/promises';
import path from 'node:path';
import {chromium} from 'playwright';

const root=path.resolve(import.meta.dirname,'dist/cronicas/browser');
const output=path.resolve(import.meta.dirname,'../docs/visual-validation/quality');await mkdir(output,{recursive:true});
const server=createServer(async(req,res)=>{
  const file=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));
  if(!file.startsWith(root+path.sep)){res.writeHead(404);res.end();return;}
  try{const target=path.extname(file)?file:path.join(root,'index.html');const body=await readFile(target);res.setHeader('Content-Type',target.endsWith('.js')?'application/javascript':target.endsWith('.css')?'text/css':'text/html');res.end(body);}catch{res.writeHead(404);res.end();}
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const base=`http://127.0.0.1:${server.address().port}`;const browser=await chromium.launch({headless:true});
const make=(id,name,updatedAt,canEdit=true)=>({id,name,updatedAt,canEdit,campaignId:'campaign-test',ownerId:canEdit?'test-user':'other-user',portraitUrl:null,backgroundUrl:null,health:10,maxHealth:20,mana:0,maxMana:0,sanity:0,maxSanity:0,fields:{},version:1,systemVersionId:null});
let characters=[make('char-one','Ágata','2026-11-14T12:00:00Z'),make('char-two','Bruno','2026-11-15T12:00:00Z',false)];
let role='player';const deletions=[];const errors=[];
try{
  const page=await browser.newPage({viewport:{width:1440,height:900}});page.on('pageerror',error=>errors.push(error.message));
  await page.route('**/api/**',async route=>{
    const url=new URL(route.request().url());const pathname=url.pathname;const method=route.request().method();let status=200;let body=[];
    if(pathname==='/api/auth/me')body={id:'test-user',email:'browser@example.test',displayName:'Fixture',accent:'wine',bio:null,avatarUrl:null};
    else if(pathname==='/api/campaigns/campaign-test')body={id:'campaign-test',name:'Elenco de ensaio',description:'',role,status:'active',systemId:null,accentColor:'#a491e5'};
    else if(pathname==='/api/campaigns/campaign-test/characters'){
      if(method==='POST'){const input=route.request().postDataJSON();const item=make('char-new',input.name,new Date().toISOString());characters.push(item);body=item;status=201;}
      else body=characters.map(c=>({...c,canEdit:role==='spectator'?false:c.canEdit}));
    }
    else if(/\/characters\/[^/]+$/.test(pathname)){
      const id=pathname.split('/').at(-1);const item=characters.find(c=>c.id===id);
      if(!item){status=404;body={message:'Ficha indisponível.'};}
      else if(method==='DELETE'){
        deletions.push({id,version:Number(url.searchParams.get('version'))});
        if(Number(url.searchParams.get('version'))!==item.version){status=409;body={message:'A ficha mudou. Recarregue e confirme novamente antes de excluir.'};}
        else{characters=characters.filter(c=>c.id!==id);status=204;body=null;}
      }else body={...item,canEdit:role==='spectator'?false:item.canEdit};
    }
    else if(pathname.endsWith('/system'))body=null;
    else if(pathname.endsWith('/favorite'))body={favorite:false};
    else if(pathname.endsWith('/rules'))body={supported:false};
    else if(pathname.includes('negotiate')){status=503;body={};}
    await route.fulfill({status,...(status===204?{}:{contentType:'application/json',body:JSON.stringify(body)})});
  });
  await page.goto(base+'/campaigns/campaign-test/characters');
  await page.getByLabel('Buscar personagens',{exact:true}).waitFor();
  await page.getByLabel('Buscar personagens',{exact:true}).fill('agata');
  assert.equal(await page.locator('.character-list-item').count(),1);assert.match(await page.locator('.character-list-item').textContent(),/Ágata/);
  await page.getByRole('button',{name:'Limpar busca',exact:true}).click();
  await page.getByLabel('Ordenar por',{exact:true}).selectOption('recent');
  assert.match(await page.locator('.character-list-item').first().textContent(),/Bruno/);
  await page.getByLabel('Buscar personagens',{exact:true}).fill('inexistente');assert.equal(await page.locator('.character-list-item').count(),0);
  await page.getByRole('button',{name:'Limpar busca',exact:true}).click();
  await page.locator('.character-list-item').filter({hasText:'Ágata'}).click();
  const trigger=page.getByRole('button',{name:'Excluir ficha',exact:true});await trigger.waitFor();await trigger.click();
  const dialog=page.getByRole('dialog');await dialog.waitFor({state:'visible'});
  const confirm=dialog.getByRole('button',{name:'Excluir permanentemente',exact:true});assert.equal(await confirm.isDisabled(),true);
  assert.equal(await dialog.getByRole('button',{name:'Cancelar',exact:true}).evaluate(el=>el===document.activeElement),true,'cancel receives initial focus');
  await dialog.getByLabel('Digite o nome da ficha para confirmar',{exact:true}).fill('outro nome');assert.equal(await confirm.isDisabled(),true);
  await page.screenshot({path:path.join(output,'character-delete-desktop.png'),fullPage:false});
  await page.setViewportSize({width:390,height:844});
  assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'mobile page fits');
  for(const button of await dialog.getByRole('button').all())assert.ok((await button.boundingBox()).height>=44,'mobile dialog has usable touch targets');
  const boxes=await dialog.getByRole('button').evaluateAll(buttons=>buttons.map(b=>{const r=b.getBoundingClientRect();return {top:r.top,bottom:r.bottom,left:r.left,right:r.right};}));
  assert.ok(boxes[1].top>=boxes[0].bottom+8||boxes[1].left>=boxes[0].right+8,'dialog actions are separated');
  await page.screenshot({path:path.join(output,'character-delete-mobile.png'),fullPage:false});
  await dialog.getByRole('button',{name:'Cancelar',exact:true}).click();assert.equal(deletions.length,0);assert.equal(await trigger.evaluate(el=>el===document.activeElement),true);
  await trigger.click();await page.keyboard.press('Escape');await dialog.waitFor({state:'hidden'});assert.equal(deletions.length,0);
  await trigger.click();characters[0]={...characters[0],version:2};
  await dialog.getByLabel('Digite o nome da ficha para confirmar',{exact:true}).fill('Ágata');
  await confirm.click();await dialog.getByRole('alert').waitFor();assert.match(await dialog.getByRole('alert').textContent(),/mudou/);
  assert.equal(deletions.at(-1).version,1,'confirmation keeps reviewed version');assert.equal(characters.length,2,'conflict preserves character');
  await dialog.getByRole('button',{name:'Cancelar',exact:true}).click();await page.reload();await trigger.waitFor();await trigger.click();
  await dialog.getByLabel('Digite o nome da ficha para confirmar',{exact:true}).fill('Ágata');await confirm.click();
  await page.waitForURL('**/campaigns/campaign-test/characters?deleted=1');await page.getByText('Ficha excluída.',{exact:true}).waitFor();
  assert.equal(deletions.at(-1).version,2);assert.equal(await page.locator('.character-list-item').filter({hasText:'Ágata'}).count(),0);
  await page.locator('.character-list-item').filter({hasText:'Bruno'}).click();await page.getByRole('heading',{name:'Bruno',exact:true,level:1}).waitFor();
  assert.equal(await page.getByRole('button',{name:'Excluir ficha',exact:true}).count(),0,'readonly users have no delete action');
  await page.getByRole('link',{name:'← Personagens',exact:true}).click();
  await page.getByLabel('Nome',{exact:true}).fill('Nova ficha');await page.getByRole('button',{name:'Criar personagem',exact:true}).click();
  await page.waitForURL('**/characters/char-new');await page.getByRole('heading',{name:'Nova ficha',exact:true,level:1}).waitFor();
  role='spectator';await page.goto(base+'/campaigns/campaign-test/characters');await page.locator('.character-list-item').first().waitFor();
  assert.equal(await page.getByRole('button',{name:'Criar personagem',exact:true}).count(),0);
  assert.deepEqual(errors,[]);console.log('Character management browser: accent-insensitive search, sorting, empty results, creation opens editor, typed deletion, cancel/Escape/focus, reviewed-version conflict, successful return, readonly and mobile target spacing passed.');
}finally{await browser.close();await new Promise(resolve=>server.close(resolve));}
