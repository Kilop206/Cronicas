import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { map } from 'rxjs';
import { ApiService } from './api';
import { ForgotPasswordPage, PreferencesPage, ResetPasswordPage } from './account-pages';
import { AuthPage, CampaignDetailPage, CampaignFormPage, CampaignsPage, DashboardPage, LandingPage, NotFoundPage, PlannedPage, ProfilePage, ShellComponent } from './views';

const protectedRoute: CanActivateFn = () => {
  const api = inject(ApiService), router = inject(Router);
  return api.ensure().pipe(map(ok => ok || router.createUrlTree(['/login'])));
};
const guestRoute: CanActivateFn = () => {
  const api = inject(ApiService), router = inject(Router);
  return api.ensure().pipe(map(ok => ok ? router.createUrlTree(['/dashboard']) : true));
};

export const routes: Routes = [
  { path: '', component: LandingPage },
  { path: 'login', component: AuthPage, data: { mode: 'login' }, canActivate: [guestRoute] },
  { path: 'register', component: AuthPage, data: { mode: 'register' }, canActivate: [guestRoute] },
  { path: 'forgot-password', component: ForgotPasswordPage },
  { path: 'reset-password', component: ResetPasswordPage },
  { path: '', component: ShellComponent, canActivate: [protectedRoute], children: [
    { path: 'dashboard', component: DashboardPage },
    { path: 'campaigns', component: CampaignsPage },
    { path: 'campaigns/new', component: CampaignFormPage },
    { path: 'campaigns/:id', component: CampaignDetailPage },
    { path: 'campaigns/:id/:section', component: PlannedPage },
    { path: 'characters/:id', component: PlannedPage },
    { path: 'worlds', component: PlannedPage },
    { path: 'worlds/:id', component: PlannedPage },
    { path: 'systems', component: PlannedPage },
    { path: 'systems/:id', component: PlannedPage },
    { path: 'profile', component: ProfilePage },
    { path: 'settings', component: PreferencesPage }
  ] },
  { path: '**', component: NotFoundPage }
];
