import { CommonModule } from '@angular/common';
import { Component, Input, OnDestroy, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { filter, Subscription } from 'rxjs';

@Component({
  selector: 'app-workspace-nav', standalone: true, imports: [CommonModule, RouterLink, RouterLinkActive],
  template: `<button *ngIf="mobile && campaignId()" type="button" class="mobile-workspace-trigger" [attr.aria-expanded]="opened()" (click)="opened.set(!opened())">Campanha ▾</button><nav *ngIf="campaignId() as id" class="workspace-menu" [class.mobile-menu]="mobile" [class.mobile-open]="opened()" aria-label="Navegação da campanha">
    <a class="workspace-home" [routerLink]="['/campaigns', id]" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Campanha atual <span aria-hidden="true">↗</span></a>
    <details [open]="activeGroup() === 'mesa'"><summary>Mesa</summary><a [routerLink]="['/campaigns', id, 'room']" routerLinkActive="active">Transmissão</a><a [routerLink]="['/campaigns', id, 'encounters']" routerLinkActive="active">Encontros</a></details>
    <details [open]="activeGroup() === 'elenco'"><summary>Elenco</summary><a [routerLink]="['/campaigns', id, 'characters']" routerLinkActive="active">Personagens</a><a [routerLink]="['/campaigns', id, 'npcs']" routerLinkActive="active">NPCs</a></details>
    <details [open]="activeGroup() === 'arquivo'"><summary>Arquivo</summary><a [routerLink]="['/campaigns', id, 'sessions']" routerLinkActive="active">Sessões</a><a [routerLink]="['/campaigns', id, 'wiki']" routerLinkActive="active">Mundo</a><a [routerLink]="['/campaigns', id, 'documents']" routerLinkActive="active">Documentos</a></details>
  </nav>`,
  styles: [`
    .workspace-menu { margin: 12px 10px; padding: 13px 8px 6px; border-top: 1px solid var(--border); }
    .mobile-workspace-trigger { border: 0; background: none; color: var(--muted); font-size: 12px; padding: 0; }
    .workspace-home { display: flex; justify-content: space-between; color: var(--gold-bright); font-size: 12px; font-weight: 600; padding: 5px 8px 10px; }
    details { margin: 3px 0; }
    summary { cursor: pointer; color: var(--muted); font-size: 10px; font-weight: 700; letter-spacing: .13em; padding: 8px; text-transform: uppercase; }
    details a { display: block; padding: 8px 12px 8px 20px; border-radius: 4px; color: var(--muted); font-size: 12px; }
    details a:hover, details a.active, .workspace-home.active { color: var(--text); background: var(--surface); }
    @media (max-width: 850px) { .mobile-menu { display: none; position: absolute; z-index: 30; top: 100%; right: 12px; min-width: 210px; max-height: 70vh; overflow: auto; margin: 0; padding: 12px; background: #151a20; border: 1px solid var(--border); box-shadow: 0 16px 36px #0008; } .mobile-menu.mobile-open { display: block; } }
  `]
})
export class WorkspaceNavComponent implements OnDestroy {
  @Input() mobile = false;
  private router = inject(Router);
  private subscription: Subscription;
  campaignId = signal(this.fromUrl(this.router.url));
  opened = signal(false);
  constructor() { this.subscription = this.router.events.pipe(filter(event => event instanceof NavigationEnd)).subscribe(event => this.campaignId.set(this.fromUrl(event.urlAfterRedirects))); }
  private fromUrl(url: string) { return /^\/campaigns\/([0-9a-f-]{36})(?:\/|\?|$)/i.exec(url)?.[1] ?? null; }
  activeGroup() { const section = /^\/campaigns\/[0-9a-f-]{36}\/([^/?]+)/i.exec(this.router.url)?.[1]; return section === 'room' || section === 'encounters' ? 'mesa' : section === 'characters' || section === 'npcs' ? 'elenco' : section ? 'arquivo' : 'mesa'; }
  ngOnDestroy() { this.subscription.unsubscribe(); }
}
