import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { Accent, AccountSession, ApiService, apiError } from './api';

@Component({ standalone: true, imports: [CommonModule, FormsModule, RouterLink], template: `
  <main class="auth-page"><div class="auth-aside"><a routerLink="/" class="brand"><span class="brand-mark">C</span><span>crônicas</span></a><div><span class="eyebrow">ACESSO AO ARQUIVO</span><h1>Encontre o<br><em>caminho de volta.</em></h1><p>Sua história continua de onde parou.</p></div><div class="aside-bottom">CRÔNICAS · RECUPERAÇÃO DE CONTA</div></div><div class="auth-main"><div class="auth-box"><span class="overline">RECUPERAR ACESSO</span><h2>Esqueceu a senha?</h2><p class="muted">Informe seu email. Se houver uma conta, enviaremos um código válido por 15 minutos.</p><form (ngSubmit)="submit()" #form="ngForm" *ngIf="!sent()"><label>Email<input name="email" [(ngModel)]="email" required type="email" autocomplete="email" placeholder="seu@email.com"></label><p class="form-error" role="alert" *ngIf="error()">{{ error() }}</p><button class="button full" [disabled]="form.invalid || busy()">{{ busy() ? 'Enviando...' : 'Enviar código' }} <span aria-hidden="true">→</span></button></form><div *ngIf="sent()" class="notice success" role="status">Se o email estiver cadastrado, o código chegará em instantes.</div><p class="auth-switch"><a routerLink="/reset-password">Já tenho um código</a> · <a routerLink="/login">Voltar ao login</a></p></div></div></main>` })
export class ForgotPasswordPage {
  private api = inject(ApiService); email = ''; busy = signal(false); sent = signal(false); error = signal('');
  submit() {
    this.error.set(''); this.busy.set(true);
    this.api.requestPasswordReset(this.email).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => this.sent.set(true), error: error => this.error.set(apiError(error))
    });
  }
}

@Component({ standalone: true, imports: [CommonModule, FormsModule, RouterLink], template: `
  <main class="auth-page"><div class="auth-aside"><a routerLink="/" class="brand"><span class="brand-mark">C</span><span>crônicas</span></a><div><span class="eyebrow">UM NOVO COMEÇO</span><h1>Retome sua<br><em>história.</em></h1><p>Um passo simples para voltar à mesa.</p></div><div class="aside-bottom">CRÔNICAS · RECUPERAÇÃO DE CONTA</div></div><div class="auth-main"><div class="auth-box"><span class="overline">NOVA SENHA</span><h2>Use seu código</h2><p class="muted">Digite o código recebido por email e escolha uma nova senha.</p><form (ngSubmit)="submit()" #form="ngForm" *ngIf="!done()"><label>Email<input name="email" [(ngModel)]="email" required type="email" autocomplete="email"></label><label>Código<input name="code" [(ngModel)]="code" required pattern="[0-9a-fA-F]{20}" maxlength="20" autocomplete="one-time-code" placeholder="20 caracteres"></label><label>Nova senha<input name="password" [(ngModel)]="password" required type="password" minlength="12" maxlength="128" autocomplete="new-password" placeholder="Mínimo de 12 caracteres"></label><p class="form-error" role="alert" *ngIf="error()">{{ error() }}</p><button class="button full" [disabled]="form.invalid || busy()">{{ busy() ? 'Alterando...' : 'Alterar senha' }} <span aria-hidden="true">→</span></button></form><div *ngIf="done()" class="notice success" role="status">Senha alterada. Entre novamente com sua nova senha.</div><p class="auth-switch"><a routerLink="/login">Voltar ao login</a> · <a routerLink="/forgot-password">Pedir outro código</a></p></div></div></main>` })
export class ResetPasswordPage {
  private api = inject(ApiService); email = ''; code = ''; password = ''; busy = signal(false); done = signal(false); error = signal('');
  submit() {
    this.error.set(''); this.busy.set(true);
    this.api.completePasswordReset(this.email, this.code, this.password).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => { this.password = ''; this.code = ''; this.done.set(true); }, error: error => this.error.set(apiError(error))
    });
  }
}

@Component({ standalone: true, imports: [CommonModule], template: `
  <main class="page narrow"><div class="page-heading"><div><span class="overline">SEU ESPAÇO</span><h1>Preferências</h1><p>Aparência e acessos à sua conta.</p></div></div>
    <div class="editor-panel"><div class="panel-heading"><span>APARÊNCIA</span><h2>Cor de destaque</h2></div><p class="muted">A cor acompanha sua conta nos seus dispositivos. As campanhas mantêm suas próprias cores.</p><div class="palette" role="group" aria-label="Cor de destaque"><button *ngFor="let item of accents" type="button" class="palette-option" [class.selected]="api.user()?.accent === item.id" [attr.aria-pressed]="api.user()?.accent === item.id" [disabled]="busy()" (click)="choose(item.id)"><span class="palette-swatch" [style.background]="item.color"></span><span>{{ item.label }}</span><span class="palette-check" aria-hidden="true">{{ api.user()?.accent === item.id ? '✓' : '' }}</span></button></div><p class="form-error" *ngIf="error()" role="alert">{{ error() }}</p><p class="form-success" *ngIf="saved()" role="status">Preferência salva.</p></div>
    <section class="editor-panel account-panel"><div class="panel-heading"><span>SEGURANÇA</span><h2>Sessões ativas</h2></div><p class="muted">Encerre acessos que você não reconhece. A sessão deste navegador é encerrada pelo botão Sair da conta.</p><p *ngIf="sessionsLoading()" class="muted">Carregando sessões...</p><ul *ngIf="!sessionsLoading()" class="session-list"><li *ngFor="let item of sessions()"><div><strong>{{ item.current ? 'Este navegador' : 'Outro acesso' }}</strong><small>Iniciado em {{ item.createdAt | date:'dd/MM/yyyy HH:mm' }} · expira em {{ item.expiresAt | date:'dd/MM/yyyy' }}</small></div><button *ngIf="!item.current" type="button" class="text-link danger" [disabled]="busy()" (click)="revoke(item.id)">Encerrar</button></li></ul></section>
    <div class="editor-panel account-panel"><div class="panel-heading"><span>SESSÃO</span><h2>Conta conectada</h2></div><p class="muted">Você está conectado como {{ api.user()?.email }}.</p><button class="button secondary" type="button" [disabled]="busy()" (click)="logout()">{{ busy() ? 'Aguarde...' : 'Sair da conta' }}</button></div>
  </main>`, styles: [`
    .session-list { list-style: none; margin: 20px 0 0; padding: 0; }
    .session-list li { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 14px 0; border-top: 1px solid var(--border); }
    .session-list li div { display: grid; gap: 4px; }
    .session-list small { color: var(--muted); }
  `] })export class PreferencesPage implements OnInit {
  api = inject(ApiService); private router = inject(Router);
  error = signal(''); saved = signal(false); busy = signal(false); sessions = signal<AccountSession[]>([]); sessionsLoading = signal(true);
  accents: { id: Accent; label: string; color: string }[] = [
    { id: 'gold', label: 'Dourado', color: '#b8a079' },
    { id: 'violet', label: 'Ametista', color: '#a78cba' },
    { id: 'wine', label: 'Vinho', color: '#b67b83' },
    { id: 'azure', label: 'Azul', color: '#81a8c3' }
  ];
  ngOnInit() { this.api.sessions().pipe(finalize(() => this.sessionsLoading.set(false))).subscribe({ next: items => this.sessions.set(items), error: error => this.error.set(apiError(error)) }); }
  revoke(id: string) {
    this.error.set(''); this.busy.set(true);
    this.api.revokeSession(id).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => this.sessions.update(items => items.filter(item => item.id !== id)), error: error => this.error.set(apiError(error))
    });
  }
  choose(accent: Accent) {
    this.error.set(''); this.saved.set(false); this.busy.set(true);
    this.api.updatePreferences(accent).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => this.saved.set(true), error: error => this.error.set(apiError(error))
    });
  }
  logout() {
    this.error.set(''); this.busy.set(true);
    this.api.logout().pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => this.router.navigateByUrl('/'), error: error => this.error.set(apiError(error))
    });
  }
}
