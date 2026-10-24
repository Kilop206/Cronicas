import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { Accent, ApiService, apiError } from './api';

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
  <main class="page narrow"><div class="page-heading"><div><span class="overline">SEU ESPAÇO</span><h1>Preferências</h1><p>Escolha a cor do seu arquivo pessoal.</p></div></div><div class="editor-panel"><div class="panel-heading"><span>APARÊNCIA</span><h2>Cor de destaque</h2></div><p class="muted">A cor acompanha sua conta nos seus dispositivos. As campanhas mantêm suas próprias cores.</p><div class="palette" role="group" aria-label="Cor de destaque"><button *ngFor="let item of accents" type="button" class="palette-option" [class.selected]="api.user()?.accent === item.id" [attr.aria-pressed]="api.user()?.accent === item.id" [disabled]="busy()" (click)="choose(item.id)"><span class="palette-swatch" [style.background]="item.color"></span><span>{{ item.label }}</span><span class="palette-check" aria-hidden="true">{{ api.user()?.accent === item.id ? '✓' : '' }}</span></button></div><p class="form-error" *ngIf="error()" role="alert">{{ error() }}</p><p class="form-success" *ngIf="saved()" role="status">Preferência salva.</p></div><div class="editor-panel account-panel"><div class="panel-heading"><span>SESSÃO</span><h2>Conta conectada</h2></div><p class="muted">Você está conectado como {{ api.user()?.email }}.</p><button class="button secondary" type="button" [disabled]="busy()" (click)="logout()">{{ busy() ? 'Aguarde...' : 'Sair da conta' }}</button></div></main>` })
export class PreferencesPage {
  api = inject(ApiService); private router = inject(Router);
  error = signal(''); saved = signal(false); busy = signal(false);
  accents: { id: Accent; label: string; color: string }[] = [
    { id: 'gold', label: 'Dourado', color: '#b8a079' },
    { id: 'violet', label: 'Ametista', color: '#a78cba' },
    { id: 'wine', label: 'Vinho', color: '#b67b83' },
    { id: 'azure', label: 'Azul', color: '#81a8c3' }
  ];
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
