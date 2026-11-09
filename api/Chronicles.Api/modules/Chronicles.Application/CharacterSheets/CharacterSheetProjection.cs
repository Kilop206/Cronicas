using System.Text.Json;
using Chronicles.Domain;

namespace Chronicles.Application.CharacterSheets;

public sealed record CharacterDocument(Guid Id, Guid CampaignId, Guid OwnerId, string Name, string? PortraitUrl,
    string? BackgroundUrl, int Health, int MaxHealth, int Sanity, int MaxSanity, int Mana, int MaxMana,
    Dictionary<string, JsonElement>? Fields, int Version, DateTime UpdatedAt, bool CanEdit);

public static class CharacterSheetProjection
{
    public static CharacterDocument Document(CharacterSheetSnapshot c, Guid actor)
    {
        var s = c.State;
        return new(c.Id, c.CampaignId, c.OwnerId, s.Name, s.PortraitUrl, s.BackgroundUrl, s.Health, s.MaxHealth,
            s.Sanity, s.MaxSanity, s.Mana, s.MaxMana, s.Fields, s.Version ?? 1, c.UpdatedAt,
            CharacterAccessPolicy.CanEdit(c.OwnerId, actor, c.Role));
    }

    public static CharacterSheetEnvelope Envelope(CharacterSheetSnapshot c, Guid actor, RpgPluginRegistry registry) =>
        new(1, c.Id, c.OwnerId, c.CampaignId, c.SystemId, c.System?.TemplateId, c.System?.RulesetVersion,
            c.System?.TemplateVersion ?? 1, c.System?.Revision ?? 1, c.State.Version ?? 1, c.UpdatedAt, c.State,
            c.System?.RendererMode == "layout" ? "custom" : c.System is null ? "custom" : registry.Resolve(c.System)?.RendererId ?? "custom",
            CharacterAccessPolicy.CanEdit(c.OwnerId, actor, c.Role), c.System is not null && registry.RulesFor(c.System) is not null);
}
