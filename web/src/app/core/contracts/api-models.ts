export type Accent = 'gold' | 'violet' | 'wine' | 'azure';
export interface User { id: string; email: string; displayName: string; bio: string | null; avatarUrl: string | null; accent: Accent; createdAt: string; }
export interface AccountSession { id: string; createdAt: string; expiresAt: string; current: boolean; }
export interface Campaign { id: string; name: string; description: string; status: string; accentColor: string; role: string; updatedAt: string; systemId: string | null; worldId: string | null; }
export interface Dashboard { totalCampaigns: number; activeCampaigns: number; recentCampaigns: Campaign[]; favoriteCharacters: FavoriteCharacter[]; upcomingSessions: UpcomingSession[]; }
export interface UpcomingSession { id: string; campaignId: string; title: string; eventAt: string; campaignName: string; }
export interface FavoriteCharacter { characterId: string; name: string; portraitUrl: string | null; campaignId: string; campaignName: string; health: number; maxHealth: number; }
export interface Page<T> { items: T[]; total: number; page: number; }
export type SheetCellValue = string | number | boolean;
export type SheetFieldValue = SheetCellValue | string[] | Record<string, SheetCellValue>[];
export interface Character { id: string; campaignId: string; ownerId: string; canEdit: boolean; name: string; portraitUrl: string | null; backgroundUrl: string | null; health: number; maxHealth: number; sanity: number; maxSanity: number; mana: number; maxMana: number; fields: Record<string, SheetFieldValue>; version: number; updatedAt: string; systemVersionId?: string | null; }
export type CharacterInput = Pick<Character, 'name' | 'portraitUrl' | 'backgroundUrl' | 'health' | 'maxHealth' | 'sanity' | 'maxSanity' | 'mana' | 'maxMana' | 'fields' | 'version'>;
export interface OverlayResource { label: string; current: number; max: number; }
export interface OverlayIndicator { label: string; value: string; }
export type OverlayCharacter = Pick<CharacterInput, 'name' | 'portraitUrl' | 'backgroundUrl' | 'health' | 'maxHealth' | 'sanity' | 'maxSanity' | 'mana' | 'maxMana'> & { templateId: string | null; resources: OverlayResource[]; indicators: OverlayIndicator[]; };
export interface CharacterRevision { id: string; version: number; createdAt: string; editedBy: string; }
export interface CharacterSecret { notes: string; revision: number; updatedAt: string | null; }
export interface SystemMigrationTarget { versionId: string | null; name: string; revision: number; templateId: string | null; templateVersion: number; rulesetVersion: string | null; contentHash: string; fields: SystemField[]; current: boolean; }
export interface SystemMigrationInput { expectedVersion: number; sourceVersionId: string; targetVersionId: string | null; targetRevision: number; targetHash: string; mappings: Record<string, string>; values: Record<string, SheetFieldValue>; }
export interface SystemMigrationOptions { targets: SystemMigrationTarget[]; history: { characterRevision: number; editedBy: string; receipt: { sourceVersionId: string; targetVersionId: string; previewHash: string; mappings: Record<string, string>; adjustedKeys: string[]; appliedAt: string } }[]; }
export interface SystemMigrationPreview { target: SystemMigrationTarget; previewHash: string | null; canApply: boolean; plan: { fields: Record<string, SheetFieldValue>; changes: { key: string; kind: string; before: SystemField | null; after: SystemField | null; previousValue: SheetFieldValue | null; nextValue: SheetFieldValue | null }[]; preservedKeys: string[]; errors: Record<string, string[]> }; }
export interface Npc { id: string; campaignId: string; name: string; portraitUrl: string | null; appearance: string; personality: string; motivation: string; relationships: string; location: string; secrets?: string; status: 'active' | 'missing' | 'deceased' | 'unknown'; visibility: 'gm' | 'campaign'; health: number; maxHealth: number; version: number; createdAt: string; updatedAt: string; sheet?:import('../models/paranormal-threat').ParanormalThreatSheet|null; }
export type NpcInput = Omit<Npc, 'id' | 'campaignId' | 'createdAt' | 'updatedAt' | 'secrets'> & { secrets: string };
export interface InventoryItem { id: string; characterId: string; name: string; category: 'item' | 'equipment' | 'weapon' | 'consumable' | 'other'; quantity: number; weight: number; notes: string; version: number; createdAt: string; updatedAt: string; }
export type InventoryInput = Pick<InventoryItem, 'name' | 'category' | 'quantity' | 'weight' | 'notes' | 'version'>;
export interface CharacterAbility { id: string; characterId: string; name: string; description: string; cost: string; effect: string; duration: string; limitations: string; recharge: string; rollExpression: string | null; version: number; createdAt: string; updatedAt: string; }
export type AbilityInput = Pick<CharacterAbility, 'name' | 'description' | 'cost' | 'effect' | 'duration' | 'limitations' | 'recharge' | 'rollExpression' | 'version'>;
export interface SystemColumn { key: string; label: string; type: 'text' | 'number' | 'boolean' | 'dice'; }
export interface SystemField { key: string; label: string; type: 'text' | 'longtext' | 'number' | 'boolean' | 'select' | 'list' | 'table'; section: string; min: number | null; max: number | null; options: string[] | null; span?: number | null; formula?: string | null; rollExpression?: string | null; defaultValue?: SheetFieldValue | null; columns?: SystemColumn[] | null; editRole?: 'both' | 'gm' | null; }
export interface SheetBlock { id: string; kind: 'identity' | 'portrait' | 'resource' | 'tracker' | 'section' | 'field' | 'note' | 'inventory' | 'abilities' | 'history'; span: number; title: string | null; key: string | null; maxKey?: string | null; text: string | null; style: 'panel' | 'plain' | 'accent' | 'score' | 'compact' | null; x?: number | null; y?: number | null; width?: number | null; height?: number | null; visibleWhenKey?: string | null; visibleWhenValue?: boolean | null; }
export interface SheetPage { id: string; title: string; blocks: SheetBlock[]; layoutMode?: 'grid' | 'canvas' | null; backgroundUrl?: string | null; aspectRatio?: number | null; }
export interface SheetLayout { accentColor: string; pages: SheetPage[]; skin?: 'archive' | 'investigation' | 'fantasy' | 'mythic' | null; headerLabel?: string | null; fontStyle?: 'editorial' | 'modern' | 'typewriter' | null; density?: 'comfortable' | 'compact' | null; borderStyle?: 'soft' | 'sharp' | 'ornate' | null; }
export interface SystemRevision { revision: number; savedAt: string; name: string; }
export interface CthulhuOccupationSlot { id: string; label: string; skills?: string[] | null; }
export interface CthulhuOccupation { id: string; name: string; points: string; creditMin: number; creditMax: number; slots: CthulhuOccupationSlot[]; }
export interface CthulhuOccupationDefinition { skills: { key: string; formula: string }[]; occupations: CthulhuOccupation[]; skillCap?: number | null; personalCredit?: boolean; }
export interface NpcTemplate { id: string; name: string; appearance: string; personality: string; motivation: string; relationships: string; location: string; status: Npc['status']; visibility: Npc['visibility']; health: number; maxHealth: number; sheet?:import('../models/paranormal-threat').ParanormalThreatSheet|null; }
export interface RpgSystem { id: string; ownerId: string | null; name: string; description: string; fields: SystemField[]; layout: SheetLayout | null; templateId: string | null; templateVersion: number; rulesetVersion: string | null; rendererMode: 'specialized' | 'layout' | null; revision: number; revisions: SystemRevision[]; npcTemplates: NpcTemplate[]; cthulhuOccupations?: CthulhuOccupationDefinition | null; createdAt: string; updatedAt: string; }
export interface CampaignMember { userId: string; displayName: string; role: string; joinedAt: string; }
export interface World { id: string; ownerId: string; name: string; description: string; createdAt: string; updatedAt: string; }
export interface WorldDetail extends World { campaigns: { campaignId: string; name: string; role: string }[]; }
export interface WorldArticle { id: string; worldId: string; authorId: string; title: string; body: string; category: string; visibility: 'campaign' | 'private' | 'gm'; revision: number; createdAt: string; updatedAt: string; }
export interface WorldArticleLink { id: string; label: string; direction: 'incoming' | 'outgoing'; article: Pick<WorldArticle, 'id' | 'title' | 'category' | 'visibility'>; }
export interface ChatMessage { id: string; campaignId: string; userId: string; userName: string; body: string; kind: 'chat' | 'announcement'; createdAt: string; }
export interface EncounterParticipant { id: string; characterId: string | null; npcId: string | null; name: string; initiative: number; health: number; maxHealth: number; conditions: string; npcSheet?:boolean; }
export interface Encounter { id: string; campaignId: string; title: string; status: 'planning' | 'active' | 'completed'; round: number; turnIndex: number; createdAt: string; updatedAt: string; participants: EncounterParticipant[]; }
export type EncounterSummary = Omit<Encounter, 'participants' | 'createdAt'>;
export type WorldArticleInput = Pick<WorldArticle, 'title' | 'body' | 'category' | 'visibility' | 'revision'>;
export interface InvitePreview { campaignId: string; campaignName: string; role: string; expiresAt: string; }
export type NarrativeKind = 'sessions' | 'documents' | 'wiki';
export interface NarrativeEntry { id: string; campaignId: string; authorId: string; kind: NarrativeKind; title: string; body: string; category: string; visibility: 'campaign' | 'private' | 'gm'; eventAt: string | null; revealedAt: string | null; createdAt: string; updatedAt: string; revision: number; }
export type NarrativeInput = Pick<NarrativeEntry, 'title' | 'body' | 'category' | 'visibility' | 'eventAt' | 'revision'>;
export interface DiceRoll { id: string; campaignId: string; userId: string; userName: string; expression: string; rolls: number[]; keptIndexes: number[]; modifier: number; total: number; visibility: 'campaign' | 'private' | 'gm'; createdAt: string; }
export interface DamagePart { type: string; amount: number; magical: boolean; silvered: boolean; adamantine: boolean; }
export interface DamageEvent { parts: DamagePart[]; critical: boolean; attack: boolean; }
export interface WeaponContext { reckless: boolean; outsideTurn: boolean; brutalDie: number; }
export interface DamageResolution { raw: number; total: number; groups: {type: string; raw: number; applied: number; immune: boolean; resistant: boolean; vulnerable: boolean; sources: string[]}[]; }
export interface SearchResults {
  campaigns: { id: string; name: string; status: string; updatedAt: string }[];
  characters: { id: string; name: string; campaignId: string; campaignName: string; updatedAt: string }[];
  npcs: { id: string; name: string; campaignId: string; campaignName: string; updatedAt: string }[];
  worlds: { id: string; name: string; updatedAt: string }[];
  articles: { id: string; worldId: string; title: string; category: string; worldName: string; updatedAt: string }[];
  entries: { id: string; campaignId: string; kind: NarrativeKind; title: string; category: string; campaignName: string; updatedAt: string }[];
}
