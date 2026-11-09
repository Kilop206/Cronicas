import type { Accent, User, AccountSession, Campaign, Dashboard, Page, Character, CharacterInput, OverlayCharacter, CharacterRevision, CharacterSecret, Npc, NpcInput, InventoryItem, InventoryInput, CharacterAbility, AbilityInput, RpgSystem, CampaignMember, World, WorldDetail, WorldArticle, WorldArticleLink, ChatMessage, EncounterParticipant, Encounter, EncounterSummary, WorldArticleInput, InvitePreview, NarrativeKind, NarrativeEntry, NarrativeInput, DiceRoll, DamageEvent, WeaponContext, DamageResolution, SearchResults } from '../contracts/api-models';
import type { SystemMigrationInput, SystemMigrationOptions, SystemMigrationPreview } from '../contracts/api-models';
import { DOCUMENT } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { catchError, map, Observable, of, tap } from 'rxjs';
import type { ParanormalThreatSheet, ThreatManeuverRequest, ThreatManeuverOutcome, ThreatCommand } from '../models/paranormal-threat';

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
  sessions() { return this.http.get<AccountSession[]>('/api/auth/sessions', { withCredentials: true }); }
  revokeSession(id: string) { return this.http.delete<void>(`/api/auth/sessions/${id}`, { withCredentials: true }); }
  updateProfile(displayName: string, bio: string, avatarUrl: string) { return this.http.patch<User>('/api/profile', { displayName, bio, avatarUrl }, { withCredentials: true }).pipe(tap(user => this.setUser(user))); }
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
  setCampaignSystem(id: string, systemId: string | null) { return this.http.patch<void>(`/api/campaigns/${id}/system`, { systemId }, { withCredentials: true }); }
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
  addEncounterParticipant(id: string, characterId: string | null, npcId: string | null, initiative: number) { return this.http.post<EncounterParticipant>(`/api/encounters/${id}/participants`, { characterId, npcId, initiative }, { withCredentials: true }); }
  updateEncounterParticipant(id: string, input: Pick<EncounterParticipant, 'initiative' | 'health' | 'maxHealth' | 'conditions'>) { return this.http.patch<EncounterParticipant>(`/api/encounter-participants/${id}`, input, { withCredentials: true }); }
  deleteEncounterParticipant(id: string) { return this.http.delete<void>(`/api/encounter-participants/${id}`, { withCredentials: true }); }
  advanceEncounter(id: string) { return this.http.post<Encounter>(`/api/encounters/${id}/advance`, {}, { withCredentials: true }); }
  deleteEncounter(id: string) { return this.http.delete<void>(`/api/encounters/${id}`, { withCredentials: true }); }
  systems() { return this.http.get<RpgSystem[]>('/api/systems', { withCredentials: true }); }
  system(id: string) { return this.http.get<RpgSystem>(`/api/systems/${id}`, { withCredentials: true }); }
  createSystem(data: Pick<RpgSystem, 'name' | 'description' | 'fields' | 'layout' | 'templateId' | 'templateVersion' | 'rulesetVersion' | 'rendererMode' | 'npcTemplates'> & Pick<RpgSystem, 'cthulhuOccupations'>) { return this.http.post<RpgSystem>('/api/systems', data, { withCredentials: true }); }
  updateSystem(id: string, data: Pick<RpgSystem, 'name' | 'description' | 'fields' | 'layout' | 'templateId' | 'templateVersion' | 'rulesetVersion' | 'rendererMode' | 'revision' | 'npcTemplates'> & Pick<RpgSystem, 'cthulhuOccupations'> & { fieldRenames?: Record<string, string> }) { return this.http.put<RpgSystem>(`/api/systems/${id}`, data, { withCredentials: true }); }
  restoreSystemRevision(id: string, revision: number, expectedRevision: number) { return this.http.post<RpgSystem>(`/api/systems/${id}/revisions/${revision}/restore`, { expectedRevision }, { withCredentials: true }); }
  deleteSystem(id: string) { return this.http.delete<void>(`/api/systems/${id}`, { withCredentials: true }); }
  characters(campaignId: string) { return this.http.get<Character[]>(`/api/campaigns/${campaignId}/characters`, { withCredentials: true }); }
  npcs(campaignId: string, page = 1, search = '') { return this.http.get<Page<Npc>>(`/api/campaigns/${campaignId}/npcs`, { params: { page, search }, withCredentials: true }); }
  npc(id: string) { return this.http.get<Npc>(`/api/npcs/${id}`, { withCredentials: true }); }
  createNpc(campaignId: string, input: NpcInput) { return this.http.post<Npc>(`/api/campaigns/${campaignId}/npcs`, input, { withCredentials: true }); }
  updateNpc(id: string, input: NpcInput) { return this.http.put<Npc>(`/api/npcs/${id}`, input, { withCredentials: true }); }
  deleteNpc(id: string, version: number) { return this.http.delete<void>(`/api/npcs/${id}`, { params: { version }, withCredentials: true }); }
  npcRevisions(id:string){return this.http.get<{version:number;savedAt:string;editedById:string;name:string}[]>(`/api/npcs/${id}/revisions`,{withCredentials:true});}
  validateNpcSheet(campaignId:string,sheet:unknown){return this.http.post<ParanormalThreatSheet>(`/api/campaigns/${campaignId}/npcs/validate-sheet`,sheet,{withCredentials:true});}
  restoreNpc(id:string,version:number,expectedVersion:number){return this.http.post<Npc>(`/api/npcs/${id}/revisions/${version}/restore`,{expectedVersion},{withCredentials:true});}
  npcManeuver(id:string,expectedVersion:number,input:ThreatManeuverRequest){return this.http.post<{npc:Npc;targetId:string;targetKind:string;targetName:string;targetVersion:number;outcome:ThreatManeuverOutcome;rolls:{expression:string;rolls:number[];total:number}[];attackerRolls:{expression:string;rolls:number[];total:number}[];defenderRolls:{expression:string;rolls:number[];total:number}[]}>(`/api/npcs/${id}/maneuvers`,{expectedVersion,...input},{withCredentials:true});}
  npcRule(id:string,expectedVersion:number,command:ThreatCommand){return this.http.post<{npc:Npc;message:string;rolls:{expression:string;rolls:number[];total:number}[];attacks?:{index:number;total:number;natural:number;hit:boolean|null;critical:boolean;damage:number;damageType:string;extraDamage:number;extraType:string;missChance:number;concealmentRoll:number|null}[];perception?:{missChance:number;perceivesOpponent:boolean;visualCheckAvailable:boolean;protectsUnseen:boolean;applied:string[];warning:string|null};damage?:{raw:number;afterSave:number;multiplier:number;reduction:number;applied:number;immune:boolean;sources:string[];criticalPrevented:boolean;fortificationRoll:number|null;sneakPrevented:boolean};targetId?:string;targetVersion?:number;sanity?:number}>(`/api/npcs/${id}/rules`,{expectedVersion,command},{withCredentials:true});}
  character(id: string) { return this.http.get<Character>(`/api/characters/${id}`, { withCredentials: true }); }
  inventory(id: string) { return this.http.get<InventoryItem[]>(`/api/characters/${id}/inventory`, { withCredentials: true }); }
  createInventoryItem(id: string, input: InventoryInput) { return this.http.post<InventoryItem>(`/api/characters/${id}/inventory`, input, { withCredentials: true }); }
  updateInventoryItem(id: string, itemId: string, input: InventoryInput) { return this.http.put<InventoryItem>(`/api/characters/${id}/inventory/${itemId}`, input, { withCredentials: true }); }
  deleteInventoryItem(id: string, itemId: string, version: number) { return this.http.delete<void>(`/api/characters/${id}/inventory/${itemId}`, { params: { version }, withCredentials: true }); }
  abilities(id: string) { return this.http.get<CharacterAbility[]>(`/api/characters/${id}/abilities`, { withCredentials: true }); }
  createAbility(id: string, input: AbilityInput) { return this.http.post<CharacterAbility>(`/api/characters/${id}/abilities`, input, { withCredentials: true }); }
  updateAbility(id: string, abilityId: string, input: AbilityInput) { return this.http.put<CharacterAbility>(`/api/characters/${id}/abilities/${abilityId}`, input, { withCredentials: true }); }
  deleteAbility(id: string, abilityId: string, version: number) { return this.http.delete<void>(`/api/characters/${id}/abilities/${abilityId}`, { params: { version }, withCredentials: true }); }
  createCharacter(campaignId: string, data: CharacterInput) { return this.http.post<Character>(`/api/campaigns/${campaignId}/characters`, data, { withCredentials: true }); }
  characterRules(id: string) { return this.http.get<{ supported: boolean; rules?: unknown }>(`/api/characters/${id}/rules`, { withCredentials: true }); }
  applyCharacterRule(id: string, expectedVersion: number, command: { action: string; amount: number; option: string | null; visibility: DiceRoll['visibility']; expression?: string; damage?: DamageEvent; weapon?: WeaponContext }) { return this.http.post<{ character: Character; message: string; rolls: { expression: string; rolls: number[]; keptIndexes: number[]; modifier: number; total: number }[]; damage?: DamageResolution | null }>(`/api/characters/${id}/rules`, { expectedVersion, command }, { withCredentials: true }); }
  updateCharacter(id: string, data: CharacterInput) { return this.http.put<Character>(`/api/characters/${id}`, data, { withCredentials: true }); }
  duplicateCharacter(id: string) { return this.http.post<Character>(`/api/characters/${id}/duplicate`, {}, { withCredentials: true }); }
  characterRevisions(id: string) { return this.http.get<CharacterRevision[]>(`/api/characters/${id}/revisions`, { withCredentials: true }); }
  restoreCharacterRevision(id: string, revisionId: string, expectedVersion: number) { return this.http.post<Character>(`/api/characters/${id}/revisions/${revisionId}/restore`, { expectedVersion }, { withCredentials: true }); }
  characterSecret(id: string) { return this.http.get<CharacterSecret>(`/api/characters/${id}/secret`, { withCredentials: true }); }
  characterSystem(id: string) { return this.http.get<RpgSystem | null>(`/api/characters/${id}/system`, { withCredentials: true }); }
  pinCharacterSystem(id: string, expectedVersion: number, registrySystemId: string, templateRevision: number) { return this.http.post<Character>(`/api/v1/characters/${id}/system-version`, { expectedVersion, registrySystemId, templateRevision }, { withCredentials: true }); }
  systemMigrationOptions(id: string) { return this.http.get<SystemMigrationOptions>(`/api/v1/characters/${id}/system-migrations/targets`, { withCredentials: true }); }
  previewSystemMigration(id: string, input: SystemMigrationInput) { return this.http.post<SystemMigrationPreview>(`/api/v1/characters/${id}/system-migrations/preview`, input, { withCredentials: true }); }
  applySystemMigration(id: string, plan: SystemMigrationInput, previewHash: string) { return this.http.post<Character>(`/api/v1/characters/${id}/system-migrations`, { plan, previewHash }, { withCredentials: true }); }
  updateCharacterSecret(id: string, notes: string, revision: number, expectedExists?: boolean) { return this.http.put<CharacterSecret>(`/api/characters/${id}/secret`, { notes, revision, expectedExists }, { withCredentials: true }); }
  characterFavorite(id: string) { return this.http.get<{ favorite: boolean }>(`/api/characters/${id}/favorite`, { withCredentials: true }); }
  addCharacterFavorite(id: string) { return this.http.put<void>(`/api/characters/${id}/favorite`, {}, { withCredentials: true }); }
  removeCharacterFavorite(id: string) { return this.http.delete<void>(`/api/characters/${id}/favorite`, { withCredentials: true }); }
  rotateOverlayKey(id: string) { return this.http.post<{ key: string }>(`/api/characters/${id}/overlay-key`, {}, { withCredentials: true }); }
  overlay(key: string) { return this.http.get<OverlayCharacter>(`/api/overlay/${key}`); }
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
  percentileRoll(campaignId: string, target: number, bonusDice: number, visibility: DiceRoll['visibility'], requestId: string) { return this.http.post<DiceRoll>(`/api/campaigns/${campaignId}/percentile-rolls`, { target, bonusDice, visibility, requestId }, { withCredentials: true }); }
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
    if (error.status === 429) {
      const seconds = Number(error.headers.get('Retry-After'));
      return Number.isFinite(seconds) && seconds > 0 && seconds <= 3600
        ? `Limite temporário de requisições atingido. Tente novamente em ${Math.ceil(seconds)} segundos.`
        : 'Limite temporário de requisições atingido. Aguarde um pouco e tente novamente.';
    }
  }
  return 'Não foi possível concluir a ação. Tente novamente.';
}
