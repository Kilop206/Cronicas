import { CommonModule } from '@angular/common';
import { Component, Input, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription, interval } from 'rxjs';
import { ApiService, ChatMessage, apiError } from './api';

@Component({ selector: 'app-chat-panel', standalone: true, imports: [CommonModule, FormsModule], template: `
  <section class="chat-panel"><div class="panel-heading"><span>MESA / CONVERSA</span><h2>Chat da campanha</h2></div>
    <div class="chat-history" role="log" aria-label="Mensagens da campanha"><p *ngIf="!messages().length" class="room-note">Nenhuma mensagem ainda. Inicie a conversa da mesa.</p>
      <article *ngFor="let item of messages()" [class.announcement]="item.kind === 'announcement'"><header><strong>{{ item.userName }}</strong><span>{{ item.createdAt | date:'dd/MM HH:mm' }}</span><span *ngIf="item.kind === 'announcement'" class="overline">AVISO DO MESTRE</span></header><p>{{ item.body }}</p></article></div>
    <form (ngSubmit)="send()" #form="ngForm"><label>Mensagem<textarea name="body" [(ngModel)]="body" required maxlength="2000" rows="3" placeholder="Escreva para a mesa..."></textarea></label>
      <div class="form-actions"><label *ngIf="master()" class="checkbox-field"><input type="checkbox" name="announcement" [(ngModel)]="announcement"> Aviso do mestre</label><button class="button button-small" [disabled]="form.invalid || busy() || spectator()">{{ busy() ? 'Enviando...' : 'Enviar' }}</button></div></form>
    <p *ngIf="error()" class="form-error" role="alert">{{ error() }}</p>
  </section>` })
export class ChatPanelComponent implements OnInit, OnDestroy {
  @Input({ required: true }) campaignId = '';
  private api = inject(ApiService); private poll?: Subscription;
  messages = signal<ChatMessage[]>([]); master = signal(false); spectator = signal(false); busy = signal(false); error = signal(''); body = ''; announcement = false;
  ngOnInit() { this.api.campaign(this.campaignId).subscribe({ next: c => { this.master.set(['owner', 'gm', 'co-gm'].includes(c.role)); this.spectator.set(c.role === 'spectator'); }, error: e => this.error.set(apiError(e)) }); this.refresh(); this.poll = interval(3000).subscribe(() => this.refresh(true)); }
  ngOnDestroy() { this.poll?.unsubscribe(); }
  refresh(silent = false) { this.api.messages(this.campaignId).subscribe({ next: items => this.messages.set(items), error: e => { if (!silent) this.error.set(apiError(e)); } }); }
  send() { this.busy.set(true); this.error.set(''); this.api.sendMessage(this.campaignId, this.body, this.announcement ? 'announcement' : 'chat', crypto.randomUUID()).subscribe({ next: () => { this.body = ''; this.busy.set(false); this.refresh(); }, error: e => { this.busy.set(false); this.error.set(apiError(e)); } }); }
}
