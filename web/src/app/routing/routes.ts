import { inject } from '@angular/core';
import { safeReturnUrl } from '../core/return-url';
import { CanActivateFn, Router, RouterStateSnapshot, Routes } from '@angular/router';
import { map } from 'rxjs';
import { ApiService } from '../api';
import { ForgotPasswordPage, PreferencesPage, ResetPasswordPage } from '../features/auth/account-pages';
import { AuthPage, CampaignDetailPage, CampaignFormPage, CampaignsPage, DashboardPage, LandingPage, NotFoundPage, ProfilePage, ShellComponent } from '../views';
import { InvitePage } from '../features/campaigns/invite.page';
import { NarrativePage } from '../features/session/narrative.page';
import { WorldsPage, WorldDetailPage } from '../features/worlds/worlds.page';
import { SearchPage } from '../features/search/search.page';
import { EncountersPage } from '../features/session/encounters.page';
import type { SystemEditorPage } from '../features/rpg-systems/systems.page';
import type { NpcsPage } from '../features/npcs/npcs.page';
import type { CharacterEditorPage } from '../features/characters/studio-pages';

const protectedRoute: CanActivateFn = (_route, state: RouterStateSnapshot) => {
  const api = inject(ApiService), router = inject(Router);
  return api.ensure().pipe(map(ok => ok || router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } })));
};
const guestRoute: CanActivateFn = route => {
  const api = inject(ApiService), router = inject(Router);
  return api.ensure().pipe(map(ok => ok ? router.parseUrl(safeReturnUrl(route.queryParamMap.get('returnUrl'))) : true));
};

export const routes: Routes = [
  { path: '', component: LandingPage },
  { path: 'login', component: AuthPage, data: { mode: 'login' }, canActivate: [guestRoute] },
  { path: 'register', component: AuthPage, data: { mode: 'register' }, canActivate: [guestRoute] },
  { path: 'forgot-password', component: ForgotPasswordPage },
  { path: 'reset-password', component: ResetPasswordPage },
  { path: 'privacy', loadComponent: () => import('../features/legal/legal.page').then(m => m.LegalPage), data: { document: 'privacy' } },
  { path: 'terms', loadComponent: () => import('../features/legal/legal.page').then(m => m.LegalPage), data: { document: 'terms' } },
  { path: 'cookies', loadComponent: () => import('../features/legal/legal.page').then(m => m.LegalPage), data: { document: 'cookies' } },
  { path: 'error', loadComponent: () => import('../features/errors/error.page').then(m => m.ErrorPage) },
  { path: 'obs/:key', loadComponent: () => import('../features/characters/studio-pages').then(m => m.ObsOverlayPage) },
  { path: 'obs-camera/:campaignId/:characterId', loadComponent: () => import('../features/media/obs-camera.page').then(m => m.ObsCameraPage), canActivate: [protectedRoute] },
  { path: '', component: ShellComponent, canActivate: [protectedRoute], children: [
    { path: 'dashboard', component: DashboardPage },
    { path: 'search', component: SearchPage },
    { path: 'campaigns', component: CampaignsPage },
    { path: 'campaigns/new', component: CampaignFormPage, canDeactivate: [(component: CampaignFormPage) => component.canLeave()] },
    { path: 'campaigns/:id', component: CampaignDetailPage, canDeactivate: [(component: CampaignDetailPage) => component.canLeave()] },
    { path: 'campaigns/:id/characters', loadComponent: () => import('../features/characters/studio-pages').then(m => m.CharactersPage) },
    { path: 'campaigns/:id/npcs', loadComponent:()=>import('../features/npcs/npcs.page').then(m=>m.NpcsPage), canDeactivate:[(component:NpcsPage)=>component.canLeave()] },
    { path: 'campaigns/:id/sessions', component: NarrativePage, data: { section: 'sessions' }, canDeactivate: [(component: NarrativePage) => component.canLeave()] },
    { path: 'campaigns/:id/documents', component: NarrativePage, data: { section: 'documents' }, canDeactivate: [(component: NarrativePage) => component.canLeave()] },
    { path: 'campaigns/:id/wiki', component: NarrativePage, data: { section: 'wiki' }, canDeactivate: [(component: NarrativePage) => component.canLeave()] },
    { path: 'campaigns/:id/encounters', component: EncountersPage },
    { path: 'campaigns/:id/room', loadComponent: () => import('../features/media/media-room.page').then(m => m.MediaRoomPage) },
    { path: 'invites/:token', component: InvitePage },
    { path: 'characters/:id', loadComponent: () => import('../features/characters/studio-pages').then(m => m.CharacterEditorPage), canDeactivate: [(component: CharacterEditorPage) => component.canLeave()] },
    { path: 'worlds', component: WorldsPage },
    { path: 'worlds/:id', component: WorldDetailPage, canDeactivate: [(component: WorldDetailPage) => component.canLeave()] },
    { path: 'systems', loadComponent: () => import('../features/rpg-systems/systems.page').then(m => m.SystemsPage) },
    { path: 'systems/:id', loadComponent: () => import('../features/rpg-systems/systems.page').then(m => m.SystemEditorPage), canDeactivate: [(component: SystemEditorPage) => component.canLeave()] },
    { path: 'profile', component: ProfilePage, canDeactivate: [(component: ProfilePage) => component.canLeave()] },
    { path: 'settings', component: PreferencesPage }
  ] },
  { path: '**', component: NotFoundPage }
];
