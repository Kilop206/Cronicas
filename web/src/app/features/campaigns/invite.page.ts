import { copyLink } from '../../core/clipboard';
import { CommonModule, DOCUMENT } from '@angular/common';
import { Component, ElementRef, inject, Input, OnInit, signal, ViewChild } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService, apiError, CampaignInvite, InvitePreview, Page } from '../../api';

function roleLabel(role: string) { return ({ player: 'Jogador', gm: 'Mestre', 'co-gm': 'Co-mestre', spectator: 'Espectador' } as Record<string, string>)[role] || role; }

@Component({ selector: 'app-invite-panel', standalone: true, imports: [CommonModule, FormsModule], template: `
  <section class="invite-panel editor-panel"><div class="panel-heading"><span>PARTICIPANTES</span><h2>Convidar para a campanha</h2></div>
    <p>Crie um link da campanha, válido por sete dias. Compartilhe o mesmo link com todos os participantes que terão o papel escolhido.</p>
    <div class="invite-controls"><label>Papel na campanha<select name="role" [(ngModel)]="role"><option value="player">Jogador</option><option value="gm">Mestre</option><option value="co-gm">Co-mestre</option><option value="spectator">Espectador</option></select></label><button class="button" type="button" (click)="create()" [disabled]="busy() || loading() || !!revoking()">{{ busy() ? 'Criando...' : 'Gerar convite' }}</button></div>
    <p *ngIf="error()" class="form-error" role="alert">{{ error() }}</p>
    <div *ngIf="url()" class="invite-result" role="status"><span>Vários participantes podem usar este link até ele expirar. Cada pessoa entra com sua própria conta.</span><code>{{ url() }}</code><button type="button" (click)="copy()">Copiar link</button><span role="status">{{ copyMessage() }}</span></div>
    <p class="room-note" role="status">{{ message() }}</p>
    <div class="invite-management">
      <h3 #inviteHeading tabindex="-1">Convites ativos</h3>
      <p class="room-note">Desativar um link impede novas entradas por ele. Quem já entrou continua na campanha. Guarde o link ao gerá-lo; a lista mostra apenas seus dados.</p>
      <p *ngIf="loading()" role="status">Carregando convites...</p>
      <div *ngIf="listError()" class="notice error" role="alert">{{ listError() }} <button type="button" (click)="load()" [disabled]="busy() || !!revoking()">Tentar novamente</button></div>
      <ng-container *ngIf="!loading() && !listError()">
        <p *ngIf="!invites().total" class="room-note">Nenhum convite ativo. Gere um link para compartilhar a campanha.</p>
        <ul class="invite-list" aria-label="Convites ativos">
          <li *ngFor="let item of invites().items; trackBy: trackInvite"><div><strong>{{ roleLabel(item.role) }}</strong><span>Criado em {{ item.createdAt | date:'dd/MM/yyyy HH:mm' }}</span><span>Expira em {{ item.expiresAt | date:'dd/MM/yyyy HH:mm' }}</span></div><button type="button" class="button subtle" (click)="revoke(item)" [disabled]="busy() || !!revoking()" [attr.aria-label]="'Desativar convite de ' + roleLabel(item.role) + ' criado em ' + (item.createdAt | date:'dd/MM/yyyy HH:mm')">{{ revoking() === item.id ? 'Desativando...' : 'Desativar link' }}</button></li>
        </ul>
        <nav *ngIf="invites().total > 20" class="invite-pagination" aria-label="Páginas de convites"><button type="button" (click)="load(invites().page - 1)" [disabled]="invites().page === 1 || busy() || !!revoking()">Anterior</button><span>Página {{ invites().page }}</span><button type="button" (click)="load(invites().page + 1)" [disabled]="invites().page * 20 >= invites().total || busy() || !!revoking()">Próxima</button></nav>
      </ng-container>
    </div>
  </section>` })
export class InvitePanelComponent implements OnInit {
  @Input({ required: true }) campaignId = '';
  @ViewChild('inviteHeading') private inviteHeading?: ElementRef<HTMLElement>;
  private api = inject(ApiService); private document = inject(DOCUMENT);
  role = 'player'; roleLabel = roleLabel;
  busy = signal(false); error = signal(''); url = signal(''); copyMessage = signal('');
  loading = signal(false); listError = signal(''); revoking = signal(''); message = signal('');
  invites = signal<Page<CampaignInvite>>({ items: [], total: 0, page: 1 });
  private generatedId = '';
  ngOnInit() { this.load(); }
  trackInvite(_index: number, item: CampaignInvite) { return item.id; }
  load(page = this.invites().page) {
    if (this.loading()) return;
    this.loading.set(true); this.listError.set('');
    this.api.campaignInvites(this.campaignId, page).pipe(finalize(() => this.loading.set(false))).subscribe({ next: result => this.invites.set(result), error: error => this.listError.set(apiError(error)) });
  }
  create() {
    if (this.busy() || this.loading() || this.revoking()) return;
    this.copyMessage.set(''); this.busy.set(true); this.error.set(''); this.message.set('');
    this.api.createInvite(this.campaignId, this.role).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: invite => { this.generatedId = invite.id; this.url.set(`${this.document.location?.origin}/invites/${invite.token}`); this.load(1); },
      error: error => this.error.set(apiError(error))
    });
  }
  revoke(item: CampaignInvite) {
    if (this.busy() || this.loading() || this.revoking()) return;
    this.revoking.set(item.id); this.error.set(''); this.message.set('');
    this.api.revokeInvite(this.campaignId, item.id).pipe(finalize(() => this.revoking.set(''))).subscribe({
      next: () => {
        if (item.id === this.generatedId) { this.url.set(''); this.copyMessage.set(''); this.generatedId = ''; }
        this.message.set('Link desativado. Os participantes atuais continuam na campanha.');
        this.inviteHeading?.nativeElement.focus();
        this.load(Math.min(this.invites().page, Math.max(1, Math.ceil((this.invites().total - 1) / 20))));
      }, error: error => this.error.set(apiError(error))
    });
  }
  async copy() { this.copyMessage.set(await copyLink(this.document, this.url())); }
}

@Component({ standalone: true, imports: [CommonModule, RouterLink], template: `
  <main class="page narrow"><a routerLink="/dashboard" class="back-link">← Voltar ao arquivo</a>
    <div class="page-heading"><div><span class="overline">UM NOVO CAPÍTULO</span><h1>Convite de campanha</h1><p>Junte-se à história com sua conta.</p></div></div>
    <div *ngIf="loading()" class="loading" role="status">Verificando convite...</div>
    <div *ngIf="error()" class="notice error" role="alert">{{ error() }}</div>
    <div *ngIf="invite() as item" class="editor-panel invite-accept"><span class="overline">{{ roleLabel(item.role) }}</span><h2>{{ item.campaignName }}</h2><p>Este link da campanha pode ser usado por vários participantes até {{ item.expiresAt | date:'dd/MM/yyyy HH:mm' }}.</p><p class="room-note">Se você já participa, o link abre a campanha e mantém seu papel atual.</p><button class="button" type="button" (click)="accept()" [disabled]="busy()">{{ busy() ? 'Entrando...' : 'Entrar na campanha' }}</button></div>
  </main>` })
export class InvitePage {
  private api = inject(ApiService); private route = inject(ActivatedRoute); private router = inject(Router);
  private token = this.route.snapshot.paramMap.get('token')!;
  invite = signal<InvitePreview | null>(null); loading = signal(true); busy = signal(false); error = signal('');
  constructor() { this.api.invite(this.token).pipe(finalize(() => this.loading.set(false))).subscribe({ next: item => this.invite.set(item), error: error => this.showError(error) }); }
  roleLabel = roleLabel;
  private showError(error: unknown) {
    if (error instanceof HttpErrorResponse && error.status === 404) {
      this.invite.set(null);
      this.error.set('Este convite está indisponível. Ele pode ter expirado ou sido desativado. Peça um novo link ao proprietário da campanha.');
    } else this.error.set(apiError(error));
  }
  accept() { if (this.busy()) return; this.busy.set(true); this.error.set(''); this.api.acceptInvite(this.token).pipe(finalize(() => this.busy.set(false))).subscribe({ next: result => void this.router.navigate(['/campaigns', result.campaignId]), error: error => this.showError(error) }); }
}
