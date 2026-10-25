import { inject } from '@angular/core';
import { CanActivateFn, Router, RouterStateSnapshot, Routes } from '@angular/router';
import { map } from 'rxjs';
import { ApiService } from './api';
import { ForgotPasswordPage, PreferencesPage, ResetPasswordPage } from './account-pages';
import { AuthPage, CampaignDetailPage, CampaignFormPage, CampaignsPage, DashboardPage, LandingPage, NotFoundPage, PlannedPage, ProfilePage, ShellComponent } from './views';
import { CharacterEditorPage, CharactersPage, ObsOverlayPage } from './studio-pages';
import { InvitePage } from './invite.page';
import { NarrativePage } from './narrative.page';
import { SystemsPage, SystemEditorPage } from './systems.page';
import { WorldsPage, WorldDetailPage } from './worlds.page';
import { SearchPage } from './search.page';
import { EncountersPage } from './encounters.page';
import { NpcsPage } from './npcs.page';

const protectedRoute: CanActivateFn = (_route, state: RouterStateSnapshot) => {
  const api = inject(ApiService), router = inject(Router);
  return api.ensure().pipe(map(ok => ok || router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } })));
};
const guestRoute: CanActivateFn = route => {
  const api = inject(ApiService), router = inject(Router);
  return api.ensure().pipe(map(ok => { const requested = route.queryParamMap.get('returnUrl'); return ok ? router.parseUrl(requested?.startsWith('/') && !requested.startsWith('//') ? requested : '/dashboard') : true; }));
};

export const routes: Routes = [
  { path: '', component: LandingPage },
  { path: 'login', component: AuthPage, data: { mode: 'login' }, canActivate: [guestRoute] },
  { path: 'register', component: AuthPage, data: { mode: 'register' }, canActivate: [guestRoute] },
  { path: 'forgot-password', component: ForgotPasswordPage },
  { path: 'reset-password', component: ResetPasswordPage },
  { path: 'obs/:key', component: ObsOverlayPage },
  { path: 'obs-camera/:campaignId/:characterId', loadComponent: () => import('./obs-camera.page').then(m => m.ObsCameraPage), canActivate: [protectedRoute] },
  { path: '', component: ShellComponent, canActivate: [protectedRoute], children: [
    { path: 'dashboard', component: DashboardPage },
    { path: 'search', component: SearchPage },
    { path: 'campaigns', component: CampaignsPage },
    { path: 'campaigns/new', component: CampaignFormPage },
    { path: 'campaigns/:id', component: CampaignDetailPage },
    { path: 'campaigns/:id/characters', component: CharactersPage },
    { path: 'campaigns/:id/npcs', component: NpcsPage },
    { path: 'campaigns/:id/sessions', component: NarrativePage, data: { section: 'sessions' } },
    { path: 'campaigns/:id/documents', component: NarrativePage, data: { section: 'documents' } },
    { path: 'campaigns/:id/wiki', component: NarrativePage, data: { section: 'wiki' } },
    { path: 'campaigns/:id/encounters', component: EncountersPage },
    { path: 'campaigns/:id/room', loadComponent: () => import('./media-room.page').then(m => m.MediaRoomPage) },
    { path: 'invites/:token', component: InvitePage },
    { path: 'campaigns/:id/:section', component: PlannedPage },
    { path: 'characters/:id', component: CharacterEditorPage },
    { path: 'worlds', component: WorldsPage },
    { path: 'worlds/:id', component: WorldDetailPage },
    { path: 'systems', component: SystemsPage },
    { path: 'systems/:id', component: SystemEditorPage },
    { path: 'profile', component: ProfilePage },
    { path: 'settings', component: PreferencesPage }
  ] },
  { path: '**', component: NotFoundPage }
];
