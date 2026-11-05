import catalog from './builtin-rpg-plugins.json';
import type { RpgSystem } from '../../../api';

export interface RPGSystemManifest { id: string; name: string; version: string; schemaVersion: string; }
export interface RPGSystemPlugin {
  manifest: RPGSystemManifest;
  rulesetId: string;
  rendererId: string;
  schemaVersions: number[];
  executableSchemaVersions: number[];
  requiredFields: string[];
  ruleRequiredFields: string[];
}

export const RPG_PLUGINS: readonly RPGSystemPlugin[] = catalog;

export function resolveRpgPlugin(system: RpgSystem | null): RPGSystemPlugin | null {
  if (!system?.templateId) return null;
  return RPG_PLUGINS.find(plugin => plugin.manifest.id === system.templateId &&
    plugin.manifest.version === system.rulesetVersion && plugin.schemaVersions.includes(system.templateVersion)) ?? null;
}
