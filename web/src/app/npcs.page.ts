import { CommonModule } from '@angular/common';
import { Component, HostListener, OnDestroy, OnInit, ViewChild, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService, Npc, NpcInput, NpcTemplate, Page, RpgSystem, apiError } from './api';
import {ParanormalThreatEditorComponent} from './paranormal-threat-editor.component';
import {ParanormalThreatSheetComponent} from './paranormal-threat-sheet.component';
import {blankThreatSheet,freshThreat,resolveThreatRenderer,threatId} from './paranormal-threat';

const blankNpc = (): NpcInput => ({
  name: '', portraitUrl: null, appearance: '', personality: '', motivation: '', relationships: '',
  location: '', secrets: '', status: 'active', visibility: 'gm', health: 10, maxHealth: 10, version: 1
});

@Component({ standalone: true, imports: [CommonModule, FormsModule, RouterLink,ParanormalThreatEditorComponent,ParanormalThreatSheetComponent], template: `
  <main class="page" [class.enemy-focused]="enemyMode()"><a [routerLink]="['/campaigns', campaignId]" class="back-link">← Voltar à campanha</a>
    <div class="page-heading"><div><span class="overline">ELENCO / MESTRE</span><h1>NPCs e ameaças</h1><p>Personagens da história e fichas de inimigos da campanha.</p></div><div class="form-actions"><button *ngIf="master()" type="button" class="button button-small" [disabled]="busy()" (click)="newNpc()">Novo NPC</button><button *ngIf="master()&&supportsThreats()" type="button" class="button button-small" [disabled]="busy()" (click)="newEnemy()">Novo inimigo</button></div></div>
    <p *ngIf="error()" class="notice error" role="alert">{{ error() }}</p>
    <button *ngIf="enemyMode()" type="button" class="text-link elenco-toggle" (click)="listOpen=!listOpen">{{listOpen?'Recolher elenco':'Mostrar elenco'}}</button>
    <div class="narrative-layout" [class.threat-workspace]="enemyMode()"><section class="narrative-list" [class.mobile-closed]="enemyMode()&&!listOpen"><label class="search-field"><span aria-hidden="true">⌕</span><input type="search" [(ngModel)]="search" (ngModelChange)="searchChanged()" placeholder="Buscar NPCs" aria-label="Buscar NPCs"></label>
      <p *ngIf="loading()" class="loading">Abrindo o elenco...</p>
      <p *ngIf="!loading() && !page()?.items?.length" class="empty-state small">{{ search ? 'Nenhum NPC encontrado.' : master() ? 'Crie o primeiro NPC desta campanha.' : 'Nenhum NPC foi apresentado à campanha.' }}</p>
      <button *ngFor="let npc of page()?.items" type="button" class="narrative-row" [disabled]="busy()||saveState()==='pending'" [class.selected]="selected()?.id === npc.id" (click)="select(npc.id)"><span class="overline">{{npc.sheet?'AMEAÇA · ':''}}{{ statusLabel(npc.status) }} · {{ npc.visibility === 'gm' ? 'MESTRES' : 'CAMPANHA' }}</span><strong>{{ npc.name }}</strong><small>{{ npc.location || 'Localização desconhecida' }}</small></button>
      <div class="pagination" *ngIf="(page()?.total || 0) > 20"><button type="button" [disabled]="pageNumber === 1" (click)="changePage(-1)">Anterior</button><span>Página {{ pageNumber }}</span><button type="button" [disabled]="pageNumber * 20 >= (page()?.total || 0)" (click)="changePage(1)">Próxima</button></div>
    </section><section *ngIf="selected() as npc" class="editor-panel narrative-detail" [class.enemy-detail]="!!npc.sheet" [style.display]="editing() ? 'none' : null"><div class="panel-heading" *ngIf="!npc.sheet"><span>{{ statusLabel(npc.status) }}</span><h2>{{ npc.name }}</h2></div>
      <app-paranormal-threat-sheet *ngIf="threatRenderer(npc)==='paranormal-threat-v1'&&master()" [npc]="npc" (applied)="npcApplied($event)" (working)="busy.set($event)" />
      <p *ngIf="npc.sheet&&!threatRenderer(npc)" role="status">Esta versão da ficha de ameaça ainda não possui um renderizador disponível.</p>
      <div class="npc-portrait" *ngIf="npc.portraitUrl&&!npc.sheet"><img [src]="npc.portraitUrl" [alt]="'Retrato de ' + npc.name"></div>
      <p class="narrative-meta" *ngIf="!npc.sheet">{{ npc.visibility === 'gm' ? 'Apenas mestres' : 'Visível à campanha' }} · Vida {{ npc.health }}/{{ npc.maxHealth }}</p>
      <div *ngIf="master()" class="form-actions enemy-tools"><button type="button" class="button button-small secondary" [disabled]="busy()" (click)="edit(npc)">Editar NPC</button><button *ngIf="!npc.sheet&&supportsThreats()" type="button" class="button button-small secondary" [disabled]="busy()" (click)="convertToThreat(npc)">Criar ficha de inimigo</button><ng-container *ngIf="npc.sheet"><button type="button" class="text-link" [disabled]="busy()" (click)="duplicate(npc)">Criar cópia nova</button><button type="button" class="text-link" (click)="exportNpc(npc)">Exportar JSON</button><button *ngIf="ownsSystem()" type="button" class="text-link" [disabled]="busy()" (click)="saveTemplate(npc)">Salvar como modelo</button></ng-container></div>
      <details class="npc-narrative" [open]="!npc.sheet"><summary>História e segredos</summary><dl class="npc-details"><ng-container *ngFor="let field of details(npc)"><dt>{{ field.label }}</dt><dd>{{ field.value || '—' }}</dd></ng-container></dl>
      <div *ngIf="master()" class="npc-secret"><span class="overline">SOMENTE MESTRES</span><p>{{ npc.secrets || 'Nenhum segredo registrado.' }}</p></div></details>
    </section>
    <form *ngIf="editing()" class="editor-panel narrative-detail" [class.enemy-detail]="!!draft.sheet" [attr.inert]="busy()&&!autoSaving?'':null" (input)="scheduleSave()" (change)="scheduleSave()" (ngSubmit)="save()" #form="ngForm"><div class="panel-heading"><span>SOMENTE MESTRES</span><h2>{{ editingId ? 'Editar NPC' : draft.sheet?'Novo inimigo':'Novo NPC' }}</h2><span role="status" *ngIf="draft.sheet">{{saveLabel()}}</span></div>
      <label *ngIf="!editingId && npcTemplates().length">Modelo do sistema<select name="npcTemplate" [ngModel]="selectedTemplate" (ngModelChange)="applyNpcTemplate($event)"><option value="">Ficha em branco</option><option *ngFor="let template of npcTemplates()" [value]="template.id">{{ template.name }}</option></select></label>
      <label>Nome<input name="name" [(ngModel)]="draft.name" required minlength="2" maxlength="120"></label>
      <label>URL HTTPS do retrato<input name="portraitUrl" [(ngModel)]="draft.portraitUrl" type="url" maxlength="2048" placeholder="https://..."></label>
      <details class="npc-narrative" [open]="!draft.sheet"><summary>História e detalhes narrativos</summary><label>Aparência<textarea name="appearance" [(ngModel)]="draft.appearance" rows="3" maxlength="2000"></textarea></label>
      <label>Personalidade<textarea name="personality" [(ngModel)]="draft.personality" rows="3" maxlength="2000"></textarea></label>
      <label>Motivação<textarea name="motivation" [(ngModel)]="draft.motivation" rows="3" maxlength="2000"></textarea></label>
      <label>Relações<textarea name="relationships" [(ngModel)]="draft.relationships" rows="3" maxlength="2000"></textarea></label>
      <label>Localização<input name="location" [(ngModel)]="draft.location" maxlength="160"></label></details>
      <div class="resource-fields"><label>Vida<input name="health" type="number" [(ngModel)]="draft.health" required min="0" [max]="draft.maxHealth"></label><label>Vida máxima<input name="maxHealth" type="number" [(ngModel)]="draft.maxHealth" required min="1" max="9999"></label></div>
      <app-paranormal-threat-editor *ngIf="draft.sheet as threat" [sheet]="threat" (changed)="scheduleSave()" />
      <div class="form-actions" *ngIf="supportsThreats()"><input #npcFile type="file" accept="application/json,.json" hidden (change)="importNpc($event)"><button type="button" class="text-link" [disabled]="busy()" (click)="npcFile.click()">Importar ficha JSON</button></div>
      <label>Status<select name="status" [(ngModel)]="draft.status"><option value="active">Ativo</option><option value="missing">Desaparecido</option><option value="deceased">Falecido</option><option value="unknown">Desconhecido</option></select></label>
      <label>Visibilidade<select name="visibility" [(ngModel)]="draft.visibility"><option value="gm">Somente mestres</option><option value="campaign">Campanha</option></select></label>
      <label>Segredos do mestre<textarea name="secrets" [(ngModel)]="draft.secrets" rows="5" maxlength="10000"></textarea></label>
      <p *ngIf="saveState()==='conflict'||(saveState()==='error'&&editingId)" role="alert">{{saveState()==='conflict'?'A ficha mudou em outra janela.':'Há alterações que não puderam ser salvas.'}} Seu rascunho foi mantido. <button type="button" class="text-link" (click)="select(editingId!)">Descartar rascunho e recarregar</button></p>
      <div class="form-actions"><button type="button" class="text-link" (click)="cancelEdit()">{{draft.sheet&&editingId?'Concluir edição':'Cancelar'}}</button><button *ngIf="editingId" type="button" class="text-link danger" [disabled]="busy()" (click)="remove()">Excluir</button><button class="button" [disabled]="form.invalid || busy() || saveState()==='conflict'">{{ busy() ? 'Salvando...' : 'Salvar NPC' }}</button></div>
    </form><div *ngIf="!selected() && !editing()" class="narrative-placeholder"><p>Selecione um NPC para consultar sua história.</p></div></div>
  </main>`, styles: [`
    .npc-portrait { width: min(100%, 260px); aspect-ratio: 4 / 3; margin-bottom: 18px; overflow: hidden; background: var(--surface); }
    .npc-portrait img { width: 100%; height: 100%; object-fit: cover; }
    .npc-details { display: grid; grid-template-columns: minmax(100px, 145px) 1fr; gap: 12px 18px; margin: 24px 0; }
    .npc-details dt { color: var(--muted); font-size: 12px; }
    .npc-details dd { margin: 0; white-space: pre-wrap; overflow-wrap: anywhere; }
    .npc-secret { border-top: 1px solid var(--border); padding: 20px 0; white-space: pre-wrap; }
    .threat-workspace{grid-template-columns:210px minmax(0,1fr);align-items:start}.enemy-detail{min-width:0;padding:0;border:0;background:transparent}.enemy-detail>label,.enemy-detail>.resource-fields,.enemy-detail>.panel-heading,.enemy-detail>.form-actions,.enemy-detail>.npc-details,.enemy-detail>.npc-secret{margin:10px 0}.enemy-tools{flex-wrap:wrap}.elenco-toggle{display:none}.enemy-detail form{min-width:0}@media(max-width:1000px){.threat-workspace{grid-template-columns:minmax(0,1fr)}.threat-workspace .mobile-closed{display:none}.elenco-toggle{display:inline-block;margin-bottom:10px}}
    .enemy-focused.page{padding:18px 22px}.enemy-focused .page-heading{margin:10px 0 16px;gap:10px;flex-wrap:wrap;align-items:center}.enemy-focused .page-heading h1{font-size:24px;margin:0}.enemy-focused .page-heading p,.enemy-focused .page-heading .overline{display:none}.enemy-focused .page-heading .form-actions{margin:0;padding:0;border:0}.npc-narrative{margin:14px 0}.npc-narrative>summary{cursor:pointer;font-size:12px}.npc-narrative>label{margin-top:10px}input[type=file][hidden]{display:none!important}@media(max-width:480px){.enemy-focused.page{padding:12px}.enemy-focused .page-heading{align-items:flex-start}.enemy-focused .page-heading h1{font-size:20px}}
    @media (max-width: 620px) { .npc-details { grid-template-columns: 1fr; gap: 4px; } .npc-details dd { margin-bottom: 12px; } }
  `] })
export class NpcsPage implements OnInit, OnDestroy {
  private api = inject(ApiService); private route = inject(ActivatedRoute);
  campaignId = this.route.snapshot.paramMap.get('id')!;
  page = signal<Page<Npc> | null>(null); selected = signal<Npc | null>(null); master = signal(false);
  npcTemplates = signal<NpcTemplate[]>([]); selectedTemplate = '';
  loading = signal(true); busy = signal(false); editing = signal(false); error = signal('');
  pageNumber = 1; search = ''; draft = blankNpc(); editingId: string | null = null;
  private searchTimer?: ReturnType<typeof setTimeout>;
  private saveTimer?:ReturnType<typeof setTimeout>;
  @ViewChild('form')form?:NgForm;
  system=signal<RpgSystem|null>(null);listOpen=false;saveState=signal<'idle'|'pending'|'saving'|'saved'|'error'|'conflict'>('idle');
  autoSaving=false;private editSequence=0;private lastScheduledFingerprint='';
  private fingerprint(){return JSON.stringify({...this.draft,version:0});}
  ngOnInit() {
    this.api.campaign(this.campaignId).subscribe({ next: campaign => { const master = ['owner', 'gm', 'co-gm'].includes(campaign.role); this.master.set(master); if (master && campaign.systemId) this.api.system(campaign.systemId).subscribe({ next: system => {this.system.set(system);this.npcTemplates.set(system.npcTemplates || []);}, error: error => this.error.set(apiError(error)) }); }, error: error => this.error.set(apiError(error)) });
    this.load();
    const npcId = this.route.snapshot.queryParamMap.get('npc');
    if (npcId) this.select(npcId);
  }
  ngOnDestroy() { clearTimeout(this.searchTimer);clearTimeout(this.saveTimer); }
  supportsThreats(){return this.system()?.templateId==='ordem_paranormal' && this.system()?.rulesetVersion==='1.3';}
  ownsSystem(){return this.supportsThreats() && this.system()?.ownerId===this.api.user()?.id;}
  enemyMode(){return !!(this.editing()?this.draft.sheet:this.selected()?.sheet);}
  threatRenderer(npc:Npc){return resolveThreatRenderer(npc.sheet);}
  saveLabel(){return this.saveState()==='saving'?'Salvando…':this.saveState()==='pending'?'Alterações pendentes':this.saveState()==='saved'?'Alterações salvas':this.saveState()==='error'?'Falha ao salvar; rascunho mantido':this.saveState()==='conflict'?'Conflito; recarregue a ficha':this.editingId?'Salvamento automático':'Salve para criar a ficha';}
  hasUnsaved(){return !!this.editing() && !!this.draft.sheet && (['pending','saving','error','conflict'].includes(this.saveState()) || !this.editingId&&this.draft.name.trim().length>0);}
  canLeave(){return !this.hasUnsaved() || confirm('Há alterações da ameaça que ainda não foram salvas. Sair descartando o rascunho?');}
  @HostListener('window:beforeunload',['$event'])beforeUnload(event:BeforeUnloadEvent){if(this.hasUnsaved()){event.preventDefault();event.returnValue='';}}
  scheduleSave(){if(!this.editingId||!this.draft.sheet||this.busy()&&!this.autoSaving||this.saveState()==='conflict')return;const fingerprint=this.fingerprint();if(fingerprint===this.lastScheduledFingerprint)return;this.lastScheduledFingerprint=fingerprint;this.editSequence++;clearTimeout(this.saveTimer);this.saveState.set('pending');this.saveTimer=setTimeout(()=>this.save(true),700);}
  load() { this.loading.set(true); this.api.npcs(this.campaignId, this.pageNumber, this.search).pipe(finalize(() => this.loading.set(false))).subscribe({ next: page => this.page.set(page), error: error => this.error.set(apiError(error)) }); }
  searchChanged() { clearTimeout(this.searchTimer); this.searchTimer = setTimeout(() => { this.pageNumber = 1; this.load(); }, 250); }
  changePage(step: number) { this.pageNumber += step; this.load(); }
  select(id: string) {clearTimeout(this.saveTimer);this.saveState.set('idle');this.error.set(''); this.editing.set(false); this.api.npc(id).subscribe({ next: npc => this.selected.set(npc), error: error => this.error.set(apiError(error)) }); }
  newNpc() {if(!this.canLeave())return;clearTimeout(this.saveTimer);this.saveState.set('idle');this.editingId = null; this.selected.set(null); this.draft = blankNpc(); this.selectedTemplate = ''; this.editing.set(true); this.error.set(''); }
  newEnemy(){if(!this.canLeave())return false;this.editing.set(false);this.newNpc();this.draft.sheet=blankThreatSheet();return true;}
  applyNpcTemplate(id: string) { this.selectedTemplate = id; const template = this.npcTemplates().find(item => item.id === id); const name = this.draft.name, portraitUrl = this.draft.portraitUrl; this.draft = template ? { ...blankNpc(), appearance: template.appearance || '', personality: template.personality || '', motivation: template.motivation || '', relationships: template.relationships || '', location: template.location || '', status: template.status || 'active', visibility: template.visibility || 'gm', health: template.health ?? 10, maxHealth: template.maxHealth ?? 10, name, portraitUrl,...(template.sheet?{sheet:freshThreat(template.sheet)}:{}) } : { ...blankNpc(), name, portraitUrl }; }
  private npcDraft(npc:Npc):NpcInput{return {name:npc.name,portraitUrl:npc.portraitUrl,appearance:npc.appearance,personality:npc.personality,motivation:npc.motivation,relationships:npc.relationships,location:npc.location,secrets:npc.secrets??'',status:npc.status,visibility:npc.visibility,health:npc.health,maxHealth:npc.maxHealth,version:npc.version,sheet:npc.sheet?structuredClone(npc.sheet):null};}
  edit(npc: Npc) {clearTimeout(this.saveTimer);this.saveState.set('idle');this.editingId = npc.id;this.draft=this.npcDraft(npc);this.editSequence=0;this.lastScheduledFingerprint=this.fingerprint();this.editing.set(true);this.error.set('');}
  convertToThreat(npc:Npc){this.edit(npc);this.draft.sheet=blankThreatSheet();this.scheduleSave();}
  npcApplied(npc:Npc){this.selected.set(npc);this.page.update(p=>p?{...p,items:p.items.map(n=>n.id===npc.id?npc:n)}:p);}
  save(auto=false) {
    if (!this.master()||this.busy()||this.saveState()==='conflict') return;
    clearTimeout(this.saveTimer);if(this.form?.invalid){this.saveState.set('error');this.error.set('Preencha os campos obrigatórios antes de salvar.');return;}
    this.busy.set(true);this.autoSaving=auto;const sequence=this.editSequence;this.saveState.set('saving'); this.error.set('');
    const input=structuredClone(this.draft);const request = this.editingId ? this.api.updateNpc(this.editingId, input) : this.api.createNpc(this.campaignId, input);
    request.pipe(finalize(() => {this.busy.set(false);this.autoSaving=false;if(this.saveState()==='pending'){clearTimeout(this.saveTimer);this.saveTimer=setTimeout(()=>this.save(true),700);}})).subscribe({ next: npc => {this.editingId=npc.id;if(auto){this.draft.version=npc.version;if(this.draft.sheet&&npc.sheet){this.draft.sheet.systemId=npc.sheet.systemId;for(const key of new Set([...Object.keys(input.sheet?.state||{}),...Object.keys(npc.sheet.state)])){if(this.draft.sheet.state[key]===input.sheet?.state[key]){if(key in npc.sheet.state)this.draft.sheet.state[key]=npc.sheet.state[key];else delete this.draft.sheet.state[key];}}}}else this.draft=this.npcDraft(npc);this.lastScheduledFingerprint=this.fingerprint();this.saveState.set(sequence===this.editSequence?'saved':'pending');if(!auto)this.editing.set(false);this.npcApplied(npc);this.load();}, error: error => {this.saveState.set(error.status===409?'conflict':'error');this.error.set(apiError(error));} });
  }
  cancelEdit(){clearTimeout(this.saveTimer);if(this.editingId&&this.draft.sheet&&this.hasUnsaved()){this.save(false);return;}this.editing.set(false);this.saveState.set('idle');}
  duplicate(npc:Npc){if(!this.newEnemy())return;this.draft={...this.npcDraft(npc),name:(npc.name+' (cópia)').slice(0,120),version:1,health:npc.maxHealth,status:'active',visibility:'gm',sheet:npc.sheet?freshThreat(npc.sheet):null};}
  exportNpc(npc:Npc){const blob=new Blob([JSON.stringify({format:'cronicas.npc',version:1,data:this.npcDraft(npc)},null,2)],{type:'application/json'});const url=URL.createObjectURL(blob);const link=document.createElement('a');link.href=url;link.download=`cronicas-ameaca-${npc.id}.json`;link.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}
  async importNpc(event:Event){const element=event.target as HTMLInputElement;const file=element.files?.[0];element.value='';if(!file)return;try{if(file.size>200000)throw Error('Arquivo acima de 200 KB.');const parsed=JSON.parse(await file.text());if(parsed.format!=='cronicas.npc'||parsed.version!==1||!parsed.data?.sheet||typeof parsed.data.name!=='string')throw Error('Arquivo de ameaça inválido.');this.busy.set(true);this.api.validateNpcSheet(this.campaignId,parsed.data.sheet).pipe(finalize(()=>this.busy.set(false))).subscribe({next:sheet=>{const source=parsed.data;const data={...blankNpc(),name:source.name,portraitUrl:typeof source.portraitUrl==='string'?source.portraitUrl:null,health:source.health,maxHealth:source.maxHealth,sheet:{...sheet,systemId:this.draft.sheet?.systemId||null,presenceTargets:[]},version:this.draft.version};for(const key of ['appearance','personality','motivation','relationships','location','secrets'] as const)data[key]=typeof source[key]==='string'?source[key]:'';this.draft=data;this.error.set('');this.busy.set(false);this.scheduleSave();},error:e=>this.error.set(apiError(e))});}catch(e){this.error.set(e instanceof Error?e.message:'Não foi possível importar a ficha.');}}
  saveTemplate(npc:Npc){const system=this.system();if(!system||!npc.sheet)return;if(system.npcTemplates.length>=12){this.error.set('O sistema já possui doze modelos de NPC.');return;}this.busy.set(true);const template:NpcTemplate={id:threatId('ameaca'),name:npc.name.slice(0,80),appearance:npc.appearance,personality:npc.personality,motivation:npc.motivation,relationships:npc.relationships,location:npc.location,status:'active',visibility:'gm',health:npc.maxHealth,maxHealth:npc.maxHealth,sheet:freshThreat(npc.sheet)};this.api.updateSystem(system.id,{...system,npcTemplates:[...system.npcTemplates,template]}).pipe(finalize(()=>this.busy.set(false))).subscribe({next:s=>{this.system.set(s);this.npcTemplates.set(s.npcTemplates);},error:e=>this.error.set(apiError(e))});}
  remove() {
    if (!this.editingId || !confirm(`Excluir ${this.draft.name}?`)) return;
    this.busy.set(true); this.error.set('');
    this.api.deleteNpc(this.editingId, this.draft.version).pipe(finalize(() => this.busy.set(false))).subscribe({ next: () => { this.editing.set(false); this.selected.set(null); this.load(); }, error: error => this.error.set(apiError(error)) });
  }
  statusLabel(status: Npc['status']) { return status === 'missing' ? 'DESAPARECIDO' : status === 'deceased' ? 'FALECIDO' : status === 'unknown' ? 'DESCONHECIDO' : 'ATIVO'; }
  details(npc: Npc) { return [
    { label: 'Aparência', value: npc.appearance }, { label: 'Personalidade', value: npc.personality },
    { label: 'Motivação', value: npc.motivation }, { label: 'Relações', value: npc.relationships },
    { label: 'Localização', value: npc.location }
  ]; }
}
