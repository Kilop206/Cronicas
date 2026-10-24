import { DOCUMENT } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { catchError, map, Observable, of, tap } from 'rxjs';

export type Accent = 'gold' | 'violet' | 'wine' | 'azure';
export interface User { id: string; email: string; displayName: string; bio: string | null; accent: Accent; createdAt: string; }
export interface Campaign { id: string; name: string; description: string; status: string; accentColor: string; role: string; updatedAt: string; }
export interface Dashboard { totalCampaigns: number; activeCampaigns: number; recentCampaigns: Campaign[]; }
export interface Page<T> { items: T[]; total: number; page: number; }

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private document = inject(DOCUMENT);
  user = signal<User | null>(null);
  private loaded = false;

  ensure(): Observable<boolean> {
    if (this.loaded) return of(!!this.user());
    return this.http.get<User>('/api/auth/me', { withCredentials: true }).pipe(
      tap(user => { this.setUser(user); this.loaded = true; }),
      map(() => true),
      catchError(() => { this.setUser(null); this.loaded = true; return of(false); })
    );
  }
  login(email: string, password: string) { return this.http.post<User>('/api/auth/login', { email, password }, { withCredentials: true }).pipe(tap(user => { this.setUser(user); this.loaded = true; })); }
  register(displayName: string, email: string, password: string) { return this.http.post<User>('/api/auth/register', { displayName, email, password }, { withCredentials: true }).pipe(tap(user => { this.setUser(user); this.loaded = true; })); }
  logout() { return this.http.post<void>('/api/auth/logout', {}, { withCredentials: true }).pipe(tap(() => { this.setUser(null); this.loaded = true; })); }
  updateProfile(displayName: string, bio: string) { return this.http.patch<User>('/api/profile', { displayName, bio }, { withCredentials: true }).pipe(tap(user => this.setUser(user))); }
  updatePreferences(accent: Accent) { return this.http.patch<User>('/api/profile/preferences', { accent }, { withCredentials: true }).pipe(tap(user => this.setUser(user))); }
  requestPasswordReset(email: string) { return this.http.post<void>('/api/auth/password-reset/request', { email }, { withCredentials: true }); }
  completePasswordReset(email: string, code: string, password: string) { return this.http.post<void>('/api/auth/password-reset/complete', { email, code, password }, { withCredentials: true }); }
  dashboard() { return this.http.get<Dashboard>('/api/dashboard', { withCredentials: true }); }
  campaigns(page = 1, search = '') { return this.http.get<Page<Campaign>>('/api/campaigns', { params: { page, search }, withCredentials: true }); }
  campaign(id: string) { return this.http.get<Campaign>(`/api/campaigns/${id}`, { withCredentials: true }); }
  createCampaign(data: { name: string; description: string; accentColor: string }) { return this.http.post<Campaign>('/api/campaigns', data, { withCredentials: true }); }
  updateCampaign(id: string, data: { name: string; description: string; accentColor: string }) { return this.http.patch<Campaign>(`/api/campaigns/${id}`, data, { withCredentials: true }); }
  setCampaignStatus(id: string, status: string) { return this.http.patch<void>(`/api/campaigns/${id}/status`, { status }, { withCredentials: true }); }
  private setUser(user: User | null) {
    this.user.set(user);
    this.document.documentElement.dataset['accent'] = user?.accent || 'gold';
  }
}

export function apiError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) return 'Não foi possível conectar à API. Verifique se o servidor está em execução.';
    if (typeof error.error?.message === 'string') return error.error.message;
    if (typeof error.error?.detail === 'string') return error.error.detail;
    if (error.error?.errors && typeof error.error.errors === 'object') {
      const first = Object.values(error.error.errors).flat().find(value => typeof value === 'string');
      if (typeof first === 'string') return first;
    }
    if (error.status === 401) return 'Email ou senha incorretos.';
    if (error.status === 403) return 'Você não tem permissão para realizar esta ação.';
    if (error.status === 429) return 'Muitas tentativas. Aguarde alguns minutos.';
  }
  return 'Não foi possível concluir a ação. Tente novamente.';
}
