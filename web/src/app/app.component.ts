import { CommonModule } from '@angular/common';
import { Component, HostListener, inject, signal } from '@angular/core';
import { NavigationEnd, NavigationError, Router, RouterLink, RouterOutlet } from '@angular/router';
import { PageMetadataService } from './core/seo/page-metadata.service';
import { SiteFooterComponent } from './layouts/site-footer.component';
import { ApiService } from './api';

@Component({ selector: 'app-root', standalone: true, imports: [CommonModule, RouterOutlet, RouterLink, SiteFooterComponent], template: `
  <a *ngIf="!overlay()" class="skip-link" href="#main-content" (click)="skip($event)">Pular para o conteúdo</a>
  <div *ngIf="offline() && !overlay()" class="connection-notice" role="status">Você está sem conexão. Alterações pendentes precisam ser salvas quando a conexão voltar.</div>
  <div *ngIf="api.sessionExpired() && !overlay()" class="connection-notice" role="alert">Sua sessão expirou. Preserve seus rascunhos antes de sair. <a routerLink="/login" [queryParams]="{ returnUrl: router.url }">Entrar novamente</a></div>
  <div *ngIf="navigationFailed()" class="notice error navigation-error" role="alert">Não foi possível abrir a página. Verifique sua conexão. Se o aplicativo foi atualizado, recarregue após preservar seus rascunhos. <button type="button" (click)="retry()">Tentar novamente</button> <button type="button" (click)="reload()">Recarregar aplicativo</button></div>
  <router-outlet (activate)="routeReady.set(true)" />
  <main *ngIf="!routeReady() && !navigationFailed()" class="boot-loading" role="status" aria-live="polite"><span class="brand-mark">C</span><p>Abrindo seu arquivo...</p></main>
  <app-site-footer *ngIf="!overlay() && routeReady()" />
`, styles: [`.boot-loading { min-height: 100dvh; display: grid; place-content: center; justify-items: center; gap: 16px; color: var(--text); text-align: center; } .boot-loading .brand-mark { width: 56px; height: 56px; display: grid; place-items: center; font: 34px var(--font-editorial); } .boot-loading p { color: var(--muted); font-size: 14px; }`] })
export class AppComponent {
  router = inject(Router); api = inject(ApiService); private metadata = inject(PageMetadataService);
  routeReady = signal(false); navigationFailed = signal(false); overlay = signal(false);
  offline = signal(typeof navigator !== 'undefined' && !navigator.onLine); private failedUrl = '/';
  constructor() {
    this.router.events.subscribe(event => {
      if (event instanceof NavigationError) { this.failedUrl = event.url; this.navigationFailed.set(true); }
      if (event instanceof NavigationEnd) { this.navigationFailed.set(false); this.overlay.set(/^\/obs(?:-camera)?\//.test(event.urlAfterRedirects)); }
    });
  }
  @HostListener('window:online') online() { this.offline.set(false); }
  @HostListener('window:offline') disconnected() { this.offline.set(true); }
  retry() { void this.router.navigateByUrl(this.failedUrl); }
  reload() { if (globalThis.confirm('Recarregar o aplicativo? Alterações ainda não salvas podem ser perdidas.')) globalThis.location.reload(); }
  skip(event: Event) { event.preventDefault(); document.querySelector<HTMLElement>('main')?.focus(); }
}
