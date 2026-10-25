import { CommonModule, DOCUMENT } from '@angular/common';
import { Component, inject, Input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService, apiError, InvitePreview } from './api';

@Component({ selector: 'app-invite-panel', standalone: true, imports: [CommonModule, FormsModule], template: `
  <section class="invite-panel editor-panel"><div class="panel-heading"><span>PARTICIPANTES</span><h2>Convidar para a campanha</h2></div>
    <p>Crie um link de uso único, válido por sete dias. Envie cada link diretamente à pessoa convidada.</p>
    <div class="invite-controls"><label>Papel na campanha<select name="role" [(ngModel)]="role"><option value="player">Jogador</option><option value="gm">Mestre</option><option value="co-gm">Co-mestre</option><option value="spectator">Espectador</option></select></label><button class="button" type="button" (click)="create()" [disabled]="busy()">{{ busy() ? 'Criando...' : 'Gerar convite' }}</button></div>
    <p *ngIf="error()" class="form-error" role="alert">{{ error() }}</p>
    <div *ngIf="url()" class="invite-result" role="status"><span>Compartilhe este link. Depois que alguém o usar, ele deixará de funcionar.</span><code>{{ url() }}</code><button type="button" (click)="copy()">Copiar link</button></div>
  </section>` })
export class InvitePanelComponent {
  @Input({ required: true }) campaignId = '';
  private api = inject(ApiService); private document = inject(DOCUMENT);
  role = 'player'; busy = signal(false); error = signal(''); url = signal('');
  create() { this.busy.set(true); this.error.set(''); this.url.set(''); this.api.createInvite(this.campaignId, this.role).pipe(finalize(() => this.busy.set(false))).subscribe({ next: invite => this.url.set(`${this.document.location?.origin}/invites/${invite.token}`), error: error => this.error.set(apiError(error)) }); }
  copy() { void this.document.defaultView?.navigator.clipboard.writeText(this.url()); }
}

@Component({ standalone: true, imports: [CommonModule, RouterLink], template: `
  <main class="page narrow"><a routerLink="/dashboard" class="back-link">← Voltar ao arquivo</a>
    <div class="page-heading"><div><span class="overline">UM NOVO CAPÍTULO</span><h1>Convite de campanha</h1><p>Junte-se à história com sua conta.</p></div></div>
    <div *ngIf="loading()" class="loading">Verificando convite...</div>
    <div *ngIf="error()" class="notice error" role="alert">{{ error() }}</div>
    <div *ngIf="invite() as item" class="editor-panel invite-accept"><span class="overline">{{ roleLabel(item.role) }}</span><h2>{{ item.campaignName }}</h2><p>Este convite expira em {{ item.expiresAt | date:'short' }} e pode ser usado uma vez.</p><button class="button" type="button" (click)="accept()" [disabled]="busy()">{{ busy() ? 'Entrando...' : 'Entrar na campanha' }}</button></div>
  </main>` })
export class InvitePage {
  private api = inject(ApiService); private route = inject(ActivatedRoute); private router = inject(Router);
  private token = this.route.snapshot.paramMap.get('token')!;
  invite = signal<InvitePreview | null>(null); loading = signal(true); busy = signal(false); error = signal('');
  constructor() { this.api.invite(this.token).pipe(finalize(() => this.loading.set(false))).subscribe({ next: item => this.invite.set(item), error: error => this.error.set(apiError(error)) }); }
  roleLabel(role: string) { return ({ player: 'JOGADOR', gm: 'MESTRE', 'co-gm': 'CO-MESTRE', spectator: 'ESPECTADOR' } as Record<string, string>)[role] || role; }
  accept() { this.busy.set(true); this.error.set(''); this.api.acceptInvite(this.token).pipe(finalize(() => this.busy.set(false))).subscribe({ next: result => void this.router.navigate(['/campaigns', result.campaignId]), error: error => this.error.set(apiError(error)) }); }
}
