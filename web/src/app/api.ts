import { DOCUMENT } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { catchError, map, Observable, of, tap } from 'rxjs';

export type Accent = 'gold' | 'violet' | 'wine' | 'azure';
export interface User { id: string; email: string; displayName: string; bio: string | null; accent: Accent; createdAt: string; }
export interface Campaign { id: string; name: string; description: string; status: string; accentColor: string; role: string; updatedAt: string; }
export interface Dashboard { totalCampaigns: number; activeCampaigns: number; recentCampaigns: Campaign[]; }
export interface Page<T> { items: T[]; total: number; page: number; }
export interface Character { id: string; campaignId: string; ownerId: string; canEdit: boolean; name: string; portraitUrl: string | null; backgroundUrl: string | null; health: number; maxHealth: number; sanity: number; maxSanity: number; mana: number; maxMana: number; updatedAt: string; }
export type CharacterInput = Pick<Character, 'name' | 'portraitUrl' | 'backgroundUrl' | 'health' | 'maxHealth' | 'sanity' | 'maxSanity' | 'mana' | 'maxMana'>;
export interface InvitePreview { campaignId: string; campaignName: string; role: string; expiresAt: string; }
export type NarrativeKind = 'sessions' | 'documents' | 'wiki';
export interface NarrativeEntry { id: string; campaignId: string; authorId: string; kind: NarrativeKind; title: string; body: string; category: string; visibility: 'campaign' | 'private' | 'gm'; eventAt: string | null; revealedAt: string | null; createdAt: string; updatedAt: string; revision: number; }
export type NarrativeInput = Pick<NarrativeEntry, 'title' | 'body' | 'category' | 'visibility' | 'eventAt' | 'revision'>;

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
  characters(campaignId: string) { return this.http.get<Character[]>(`/api/campaigns/${campaignId}/characters`, { withCredentials: true }); }
  character(id: string) { return this.http.get<Character>(`/api/characters/${id}`, { withCredentials: true }); }
  createCharacter(campaignId: string, data: CharacterInput) { return this.http.post<Character>(`/api/campaigns/${campaignId}/characters`, data, { withCredentials: true }); }
  updateCharacter(id: string, data: CharacterInput) { return this.http.put<Character>(`/api/characters/${id}`, data, { withCredentials: true }); }
  rotateOverlayKey(id: string) { return this.http.post<{ key: string }>(`/api/characters/${id}/overlay-key`, {}, { withCredentials: true }); }
  overlay(key: string) { return this.http.get<Character>(`/api/overlay/${key}`); }
  joinRoom(campaignId: string) { return this.http.post<{ authToken: string }>(`/api/campaigns/${campaignId}/room/join`, {}, { withCredentials: true }); }
  createInvite(campaignId: string, role: string) { return this.http.post<{ token: string; role: string; expiresAt: string }>(`/api/campaigns/${campaignId}/invites`, { role }, { withCredentials: true }); }
  invite(token: string) { return this.http.get<InvitePreview>(`/api/invites/${token}`, { withCredentials: true }); }
  acceptInvite(token: string) { return this.http.post<{ campaignId: string }>(`/api/invites/${token}/accept`, {}, { withCredentials: true }); }
  entries(campaignId: string, kind: NarrativeKind, page = 1, search = '') { return this.http.get<Page<NarrativeEntry>>(`/api/campaigns/${campaignId}/entries/${kind}`, { params: { page, search }, withCredentials: true }); }
  createEntry(campaignId: string, kind: NarrativeKind, data: NarrativeInput) { return this.http.post<NarrativeEntry>(`/api/campaigns/${campaignId}/entries/${kind}`, data, { withCredentials: true }); }
  updateEntry(id: string, data: NarrativeInput) { return this.http.put<NarrativeEntry>(`/api/entries/${id}`, data, { withCredentials: true }); }
  deleteEntry(id: string) { return this.http.delete<void>(`/api/entries/${id}`, { withCredentials: true }); }
  revealEntry(id: string) { return this.http.post<NarrativeEntry>(`/api/entries/${id}/reveal`, {}, { withCredentials: true }); }
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
