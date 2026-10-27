import { CommonModule } from '@angular/common';
import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({ selector: 'app-root', standalone: true, imports: [CommonModule, RouterOutlet], template: `
  <router-outlet (activate)="routeReady.set(true)" />
  <main *ngIf="!routeReady()" class="boot-loading" role="status" aria-live="polite"><span class="brand-mark">C</span><p>Abrindo seu arquivo...</p></main>
`, styles: [`.boot-loading { min-height: 100dvh; display: grid; place-content: center; justify-items: center; gap: 16px; color: var(--text); text-align: center; } .boot-loading .brand-mark { width: 56px; height: 56px; display: grid; place-items: center; font: 34px var(--font-editorial); } .boot-loading p { color: var(--muted); font-size: 14px; }`] })
export class AppComponent { routeReady = signal(false); }
