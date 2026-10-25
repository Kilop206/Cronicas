import { DOCUMENT } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { catchError, map, Observable, of, tap } from 'rxjs';

export type Accent = 'gold' | 'violet' | 'wine' | 'azure';
export interface User { id: string; email: string; displayName: string; bio: string | null; accent: Accent; createdAt: string; }
export interface Campaign { id: string; name: string; description: string; status: string; accentColor: string; role: string; updatedAt: string; systemId: string | null; worldId: string | null; }
export interface Dashboard { totalCampaigns: number; activeCampaigns: number; recentCampaigns: Campaign[]; favoriteCharacters: FavoriteCharacter[]; upcomingSessions: UpcomingSession[]; }
export interface UpcomingSession { id: string; campaignId: string; title: string; eventAt: string; campaignName: string; }
export interface FavoriteCharacter { characterId: string; name: string; portraitUrl: string | null; campaignId: string; campaignName: string; health: number; maxHealth: number; }
export interface Page<T> { items: T[]; total: number; page: number; }
export interface Character { id: string; campaignId: string; ownerId: string; canEdit: boolean; name: string; portraitUrl: string | null; backgroundUrl: string | null; health: number; maxHealth: number; sanity: number; maxSanity: number; mana: number; maxMana: number; fields: Record<string, string | number | boolean>; version: number; updatedAt: string; }
export type CharacterInput = Pick<Character, 'name' | 'portraitUrl' | 'backgroundUrl' | 'health' | 'maxHealth' | 'sanity' | 'maxSanity' | 'mana' | 'maxMana' | 'fields' | 'version'>;
export interface CharacterRevision { id: string; version: number; createdAt: string; editedBy: string; }
export interface CharacterSecret { notes: string; revision: number; updatedAt: string | null; }
export interface SystemField { key: string; label: string; type: 'text' | 'longtext' | 'number' | 'boolean' | 'select'; section: string; min: number | null; max: number | null; options: string[] | null; }
export interface RpgSystem { id: string; ownerId: string | null; name: string; description: string; fields: SystemField[]; createdAt: string; updatedAt: string; }
export interface CampaignMember { userId: string; displayName: string; role: string; joinedAt: string; }
export interface World { id: string; ownerId: string; name: string; description: string; createdAt: string; updatedAt: string; }
export interface WorldDetail extends World { campaigns: { campaignId: string; name: string; role: string }[]; }
export interface WorldArticle { id: string; worldId: string; authorId: string; title: string; body: string; category: string; visibility: 'campaign' | 'private' | 'gm'; revision: number; createdAt: string; updatedAt: string; }
export interface WorldArticleLink { id: string; label: string; direction: 'incoming' | 'outgoing'; article: Pick<WorldArticle, 'id' | 'title' | 'category' | 'visibility'>; }
export interface ChatMessage { id: string; campaignId: string; userId: string; userName: string; body: string; kind: 'chat' | 'announcement'; createdAt: string; }
export interface EncounterParticipant { id: string; characterId: string; name: string; initiative: number; health: number; maxHealth: number; conditions: string; }
export interface Encounter { id: string; campaignId: string; title: string; status: 'planning' | 'active' | 'completed'; round: number; turnIndex: number; createdAt: string; updatedAt: string; participants: EncounterParticipant[]; }
export type EncounterSummary = Omit<Encounter, 'participants' | 'createdAt'>;
export type WorldArticleInput = Pick<WorldArticle, 'title' | 'body' | 'category' | 'visibility' | 'revision'>;
export interface InvitePreview { campaignId: string; campaignName: string; role: string; expiresAt: string; }
export type NarrativeKind = 'sessions' | 'documents' | 'wiki';
export interface NarrativeEntry { id: string; campaignId: string; authorId: string; kind: NarrativeKind; title: string; body: string; category: string; visibility: 'campaign' | 'private' | 'gm'; eventAt: string | null; revealedAt: string | null; createdAt: string; updatedAt: string; revision: number; }
export type NarrativeInput = Pick<NarrativeEntry, 'title' | 'body' | 'category' | 'visibility' | 'eventAt' | 'revision'>;
export interface DiceRoll { id: string; campaignId: string; userId: string; userName: string; expression: string; rolls: number[]; keptIndexes: number[]; modifier: number; total: number; visibility: 'campaign' | 'private' | 'gm'; createdAt: string; }
export interface SearchResults {
  campaigns: { id: string; name: string; status: string; updatedAt: string }[];
  characters: { id: string; name: string; campaignId: string; campaignName: string; updatedAt: string }[];
  worlds: { id: string; name: string; updatedAt: string }[];
  articles: { id: string; worldId: string; title: string; category: string; worldName: string; updatedAt: string }[];
  entries: { id: string; campaignId: string; kind: NarrativeKind; title: string; category: string; campaignName: string; updatedAt: string }[];
}

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
  exportProfile() { return this.http.get('/api/profile/export', { withCredentials: true, responseType: 'blob' }); }
  updatePreferences(accent: Accent) { return this.http.patch<User>('/api/profile/preferences', { accent }, { withCredentials: true }).pipe(tap(user => this.setUser(user))); }
  requestPasswordReset(email: string) { return this.http.post<void>('/api/auth/password-reset/request', { email }, { withCredentials: true }); }
  completePasswordReset(email: string, code: string, password: string) { return this.http.post<void>('/api/auth/password-reset/complete', { email, code, password }, { withCredentials: true }); }
  dashboard() { return this.http.get<Dashboard>('/api/dashboard', { withCredentials: true }); }
  search(q: string) { return this.http.get<SearchResults>('/api/search', { params: { q }, withCredentials: true }); }
  campaigns(page = 1, search = '') { return this.http.get<Page<Campaign>>('/api/campaigns', { params: { page, search }, withCredentials: true }); }
  campaign(id: string) { return this.http.get<Campaign>(`/api/campaigns/${id}`, { withCredentials: true }); }
  createCampaign(data: { name: string; description: string; accentColor: string }) { return this.http.post<Campaign>('/api/campaigns', data, { withCredentials: true }); }
  updateCampaign(id: string, data: { name: string; description: string; accentColor: string }) { return this.http.patch<Campaign>(`/api/campaigns/${id}`, data, { withCredentials: true }); }
  setCampaignStatus(id: string, status: string) { return this.http.patch<void>(`/api/campaigns/${id}/status`, { status }, { withCredentials: true }); }
  setCampaignSystem(id: string, systemId: string) { return this.http.patch<void>(`/api/campaigns/${id}/system`, { systemId }, { withCredentials: true }); }
  members(campaignId: string) { return this.http.get<CampaignMember[]>(`/api/campaigns/${campaignId}/members`, { withCredentials: true }); }
  changeMemberRole(campaignId: string, memberId: string, role: string) { return this.http.patch<void>(`/api/campaigns/${campaignId}/members/${memberId}`, { role }, { withCredentials: true }); }
  removeMember(campaignId: string, memberId: string) { return this.http.delete<void>(`/api/campaigns/${campaignId}/members/${memberId}`, { withCredentials: true }); }
  worlds() { return this.http.get<World[]>('/api/worlds', { withCredentials: true }); }
  world(id: string) { return this.http.get<WorldDetail>(`/api/worlds/${id}`, { withCredentials: true }); }
  createWorld(name: string, description: string) { return this.http.post<World>('/api/worlds', { name, description }, { withCredentials: true }); }
  updateWorld(id: string, name: string, description: string) { return this.http.put<World>(`/api/worlds/${id}`, { name, description }, { withCredentials: true }); }
  deleteWorld(id: string) { return this.http.delete<void>(`/api/worlds/${id}`, { withCredentials: true }); }
  setCampaignWorld(id: string, worldId: string | null) { return this.http.patch<void>(`/api/campaigns/${id}/world`, { worldId }, { withCredentials: true }); }
  worldArticles(worldId: string, page = 1, search = '') { return this.http.get<Page<WorldArticle>>(`/api/worlds/${worldId}/articles`, { params: { page, search }, withCredentials: true }); }
  worldArticle(id: string) { return this.http.get<WorldArticle>(`/api/world-articles/${id}`, { withCredentials: true }); }
  createWorldArticle(worldId: string, input: WorldArticleInput) { return this.http.post<WorldArticle>(`/api/worlds/${worldId}/articles`, input, { withCredentials: true }); }
  updateWorldArticle(id: string, input: WorldArticleInput) { return this.http.put<WorldArticle>(`/api/world-articles/${id}`, input, { withCredentials: true }); }
  deleteWorldArticle(id: string) { return this.http.delete<void>(`/api/world-articles/${id}`, { withCredentials: true }); }
  worldArticleLinks(id: string) { return this.http.get<WorldArticleLink[]>(`/api/world-articles/${id}/links`, { withCredentials: true }); }
  createWorldArticleLink(id: string, targetId: string, label: string) { return this.http.post<WorldArticleLink>(`/api/world-articles/${id}/links`, { targetId, label }, { withCredentials: true }); }
  deleteWorldArticleLink(id: string) { return this.http.delete<void>(`/api/world-article-links/${id}`, { withCredentials: true }); }
  messages(campaignId: string) { return this.http.get<ChatMessage[]>(`/api/campaigns/${campaignId}/messages`, { withCredentials: true }); }
  sendMessage(campaignId: string, body: string, kind: ChatMessage['kind'], clientId: string) { return this.http.post<ChatMessage>(`/api/campaigns/${campaignId}/messages`, { body, kind, clientId }, { withCredentials: true }); }
  encounters(campaignId: string) { return this.http.get<EncounterSummary[]>(`/api/campaigns/${campaignId}/encounters`, { withCredentials: true }); }
  encounter(id: string) { return this.http.get<Encounter>(`/api/encounters/${id}`, { withCredentials: true }); }
  createEncounter(campaignId: string, title: string) { return this.http.post<Encounter>(`/api/campaigns/${campaignId}/encounters`, { title }, { withCredentials: true }); }
  setEncounterStatus(id: string, status: Encounter['status']) { return this.http.patch<void>(`/api/encounters/${id}/status`, { status }, { withCredentials: true }); }
  addEncounterParticipant(id: string, characterId: string, initiative: number) { return this.http.post<EncounterParticipant>(`/api/encounters/${id}/participants`, { characterId, initiative }, { withCredentials: true }); }
  updateEncounterParticipant(id: string, input: Pick<EncounterParticipant, 'initiative' | 'health' | 'maxHealth' | 'conditions'>) { return this.http.patch<EncounterParticipant>(`/api/encounter-participants/${id}`, input, { withCredentials: true }); }
  deleteEncounterParticipant(id: string) { return this.http.delete<void>(`/api/encounter-participants/${id}`, { withCredentials: true }); }
  advanceEncounter(id: string) { return this.http.post<Encounter>(`/api/encounters/${id}/advance`, {}, { withCredentials: true }); }
  deleteEncounter(id: string) { return this.http.delete<void>(`/api/encounters/${id}`, { withCredentials: true }); }
  systems() { return this.http.get<RpgSystem[]>('/api/systems', { withCredentials: true }); }
  system(id: string) { return this.http.get<RpgSystem>(`/api/systems/${id}`, { withCredentials: true }); }
  createSystem(data: Pick<RpgSystem, 'name' | 'description' | 'fields'>) { return this.http.post<RpgSystem>('/api/systems', data, { withCredentials: true }); }
  updateSystem(id: string, data: Pick<RpgSystem, 'name' | 'description' | 'fields'>) { return this.http.put<RpgSystem>(`/api/systems/${id}`, data, { withCredentials: true }); }
  deleteSystem(id: string) { return this.http.delete<void>(`/api/systems/${id}`, { withCredentials: true }); }
  characters(campaignId: string) { return this.http.get<Character[]>(`/api/campaigns/${campaignId}/characters`, { withCredentials: true }); }
  character(id: string) { return this.http.get<Character>(`/api/characters/${id}`, { withCredentials: true }); }
  createCharacter(campaignId: string, data: CharacterInput) { return this.http.post<Character>(`/api/campaigns/${campaignId}/characters`, data, { withCredentials: true }); }
  updateCharacter(id: string, data: CharacterInput) { return this.http.put<Character>(`/api/characters/${id}`, data, { withCredentials: true }); }
  duplicateCharacter(id: string) { return this.http.post<Character>(`/api/characters/${id}/duplicate`, {}, { withCredentials: true }); }
  characterRevisions(id: string) { return this.http.get<CharacterRevision[]>(`/api/characters/${id}/revisions`, { withCredentials: true }); }
  restoreCharacterRevision(id: string, revisionId: string, expectedVersion: number) { return this.http.post<Character>(`/api/characters/${id}/revisions/${revisionId}/restore`, { expectedVersion }, { withCredentials: true }); }
  characterSecret(id: string) { return this.http.get<CharacterSecret>(`/api/characters/${id}/secret`, { withCredentials: true }); }
  updateCharacterSecret(id: string, notes: string, revision: number) { return this.http.put<CharacterSecret>(`/api/characters/${id}/secret`, { notes, revision }, { withCredentials: true }); }
  characterFavorite(id: string) { return this.http.get<{ favorite: boolean }>(`/api/characters/${id}/favorite`, { withCredentials: true }); }
  addCharacterFavorite(id: string) { return this.http.put<void>(`/api/characters/${id}/favorite`, {}, { withCredentials: true }); }
  removeCharacterFavorite(id: string) { return this.http.delete<void>(`/api/characters/${id}/favorite`, { withCredentials: true }); }
  rotateOverlayKey(id: string) { return this.http.post<{ key: string }>(`/api/characters/${id}/overlay-key`, {}, { withCredentials: true }); }
  overlay(key: string) { return this.http.get<Character>(`/api/overlay/${key}`); }
  joinRoom(campaignId: string) { return this.http.post<{ authToken: string }>(`/api/campaigns/${campaignId}/room/join`, {}, { withCredentials: true }); }
  createInvite(campaignId: string, role: string) { return this.http.post<{ token: string; role: string; expiresAt: string }>(`/api/campaigns/${campaignId}/invites`, { role }, { withCredentials: true }); }
  invite(token: string) { return this.http.get<InvitePreview>(`/api/invites/${token}`, { withCredentials: true }); }
  acceptInvite(token: string) { return this.http.post<{ campaignId: string }>(`/api/invites/${token}/accept`, {}, { withCredentials: true }); }
  entries(campaignId: string, kind: NarrativeKind, page = 1, search = '') { return this.http.get<Page<NarrativeEntry>>(`/api/campaigns/${campaignId}/entries/${kind}`, { params: { page, search }, withCredentials: true }); }
  entry(id: string) { return this.http.get<NarrativeEntry>(`/api/entries/${id}`, { withCredentials: true }); }
  createEntry(campaignId: string, kind: NarrativeKind, data: NarrativeInput) { return this.http.post<NarrativeEntry>(`/api/campaigns/${campaignId}/entries/${kind}`, data, { withCredentials: true }); }
  updateEntry(id: string, data: NarrativeInput) { return this.http.put<NarrativeEntry>(`/api/entries/${id}`, data, { withCredentials: true }); }
  deleteEntry(id: string) { return this.http.delete<void>(`/api/entries/${id}`, { withCredentials: true }); }
  revealEntry(id: string) { return this.http.post<NarrativeEntry>(`/api/entries/${id}/reveal`, {}, { withCredentials: true }); }
  rolls(campaignId: string) { return this.http.get<DiceRoll[]>(`/api/campaigns/${campaignId}/rolls`, { withCredentials: true }); }
  roll(campaignId: string, expression: string, visibility: DiceRoll['visibility'], requestId: string) { return this.http.post<DiceRoll>(`/api/campaigns/${campaignId}/rolls`, { expression, visibility, requestId }, { withCredentials: true }); }
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
