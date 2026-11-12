using System.Text.Json;
using Chronicles.Domain;

namespace Chronicles.Application.CharacterSheets;

public sealed record CharacterDocument(Guid Id, Guid CampaignId, Guid OwnerId, string Name, string? PortraitUrl,
    string? BackgroundUrl, int Health, int MaxHealth, int Sanity, int MaxSanity, int Mana, int MaxMana,
    Dictionary<string, JsonElement>? Fields, int Version, DateTime UpdatedAt, bool CanEdit, Guid? SystemVersionId = null);

public static class CharacterSheetProjection
{
    public static CharacterDocument Document(CharacterSheetSnapshot c, Guid actor)
        => Document(c.Id, c.CampaignId, c.OwnerId, c.State, c.UpdatedAt, c.Role, actor, c.SystemVersionId);

    public static CharacterDocument Document(CharacterListEntry c, string role, Guid actor)
        => Document(c.Id, c.CampaignId, c.OwnerId, c.State, c.UpdatedAt, role, actor, c.SystemVersionId);

    private static CharacterDocument Document(Guid id, Guid campaign, Guid owner, CharacterSheetState s, DateTime updatedAt, string role, Guid actor, Guid? systemVersionId)
        => new(id, campaign, owner, s.Name, s.PortraitUrl, s.BackgroundUrl, s.Health, s.MaxHealth,
            s.Sanity, s.MaxSanity, s.Mana, s.MaxMana, s.Fields, s.Version ?? 1, updatedAt, CharacterAccessPolicy.CanEdit(owner, actor, role), systemVersionId);

    public static CharacterSheetEnvelope Envelope(CharacterSheetSnapshot c, Guid actor, RpgPluginRegistry registry) =>
        new(1, c.Id, c.OwnerId, c.CampaignId, c.SystemId, c.System?.TemplateId, c.System?.RulesetVersion,
            c.System?.TemplateVersion ?? 1, c.System?.Revision ?? 1, c.State.Version ?? 1, c.UpdatedAt, c.State,
            c.System?.RendererMode == "layout" ? "custom" : c.System is null ? "custom" : registry.Resolve(c.System)?.RendererId ?? "custom",
            CharacterAccessPolicy.CanEdit(c.OwnerId, actor, c.Role), c.System is not null && registry.RulesFor(c.System) is not null, c.SystemVersionId);
}
