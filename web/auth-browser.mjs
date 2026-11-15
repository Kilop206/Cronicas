// Isolated API fixtures against the production build. No accounts or production writes.
import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {readFile} from 'node:fs/promises';
import path from 'node:path';
import {chromium} from 'playwright';

const root=path.resolve(import.meta.dirname,'dist/cronicas/browser');
const server=createServer(async(req,res)=>{
  const requested=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));
  if(!requested.startsWith(root+path.sep)){res.writeHead(404);res.end();return;}
  try{const file=path.extname(requested)?requested:path.join(root,'index.html');res.setHeader('Content-Type',file.endsWith('.js')?'application/javascript':file.endsWith('.css')?'text/css':'text/html');res.end(await readFile(file));}catch{res.writeHead(404);res.end();}
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const base=`http://127.0.0.1:${server.address().port}`;
const browser=await chromium.launch({headless:true});
const user={id:'11111111-1111-4111-8111-111111111111',email:'fixture@example.test',displayName:'Acesso de ensaio',accent:'wine',bio:null,avatarUrl:null};
try{
  for(const mode of ['guest','challenge','rate','unavailable','network','slow','protected','authenticated','failed-login']){
    const context=await browser.newContext();const page=await context.newPage();const errors=[];let checks=0,posts=0,loginUnavailable=mode==='failed-login';
    page.on('pageerror',error=>errors.push(error.message));
    await page.route('**/api/**',async route=>{
      const pathname=new URL(route.request().url()).pathname;let status=200,body=[],headers={'Content-Type':'application/json'};
      if(pathname==='/api/auth/me'){
        checks++;
        if(mode==='network'){await route.abort('failed');return;}
        if(mode==='slow')await new Promise(resolve=>setTimeout(resolve,7500));
        if(mode==='challenge'){await route.fulfill({status:429,headers:{'Content-Type':'text/html','cf-mitigated':'challenge'},body:'<html><title>Just a moment...</title></html>'});return;}
        if(mode==='rate'){status=429;headers['Retry-After']='60';body={message:'Limite temporário de requisições atingido. Tente novamente em 60 segundos.'};}
        else if(['unavailable','protected'].includes(mode)){status=503;body={message:'Unavailable fixture'};}
        else if(mode==='authenticated')body=user;
        else {status=401;body=null;}
      }
      else if(pathname==='/api/auth/login'){posts++;status=loginUnavailable?503:200;body=loginUnavailable?{message:'Unavailable fixture'}:user;}
      else if(pathname==='/api/dashboard')body={totalCampaigns:0,activeCampaigns:0,recentCampaigns:[],upcomingSessions:[],favoriteCharacters:[]};
      else if(pathname==='/api/campaigns')body={items:[],total:0,page:1,pageSize:20};
      // A cancelled slow probe must not update the service after the subsequent login succeeds.
      try{await route.fulfill({status,headers,body:JSON.stringify(body)});}catch(error){if(mode!=='slow')throw error;}
    });
    const destination='/systems';
    await page.goto(base+(mode==='protected'?destination:'/login?returnUrl='+encodeURIComponent(destination)));
    if(mode==='authenticated'){
      await page.waitForURL('**/systems');assert.equal(await page.locator('input[type=email]').count(),0);
    }else{
      await page.getByLabel('Email',{exact:true}).waitFor({timeout:12000});
      assert.equal(await page.locator('.navigation-error').count(),0,mode+': session probe cannot fail the login navigation');
      assert.equal(checks,1,mode+': no guard redirect/probe loop');
      if(mode==='challenge')assert.match(await page.locator('.auth-box [role=status]').textContent(),/proteção do servidor/);
      if(mode==='rate')assert.match(await page.locator('.auth-box [role=status]').textContent(),/60 segundos/);
      await page.getByLabel('Email',{exact:true}).fill(user.email);await page.getByLabel('Senha',{exact:true}).fill('fixture-password-only');
      await page.getByRole('button',{name:'Entrar',exact:true}).click();
      if(mode==='failed-login'){
        await page.locator('.form-error').waitFor();assert.equal(posts,1,'failed login mutations are not retried');
        assert.equal(await page.getByLabel('Email',{exact:true}).inputValue(),user.email);
        assert.equal(await page.getByLabel('Senha',{exact:true}).inputValue(),'fixture-password-only');
        assert.equal(await page.locator('.navigation-error').count(),0);
        loginUnavailable=false;await page.getByRole('button',{name:'Entrar',exact:true}).click();
      }
      await page.waitForURL('**/systems');await page.getByRole('heading',{name:'Seus sistemas',exact:true}).waitFor();
      if(mode==='slow')await page.waitForTimeout(3000);
      assert.equal(await page.locator('.navigation-error').count(),0);
      assert.equal(await page.locator('.connection-notice[role=alert]').count(),0,mode+': old probe cannot expire new session');
      assert.equal(checks,1,mode+': login state avoids a second probe');
    }
    assert.deepEqual(errors,[],mode+': unhandled browser errors');await context.close();
  }
  console.log('Auth browser: guest form, Cloudflare challenge, application 429, 503/network/slow probes, fail-closed protected redirect, existing session, return destination, cancelled-probe race and manual login retry passed.');
}finally{await browser.close();await new Promise(resolve=>server.close(resolve));}
