using System.Security.Claims;
using Chronicles.Application.CharacterSheets;

namespace Chronicles.Api;

public static class PluginEndpoints
{
    public static void MapRpgPluginEndpoints(this RouteGroupBuilder api)
    {
        var v1 = api.MapGroup("/v1").WithTags("RPG plugins v1");
        v1.MapGet("/rpg-plugins", (HttpContext http, RpgPluginRegistry registry) =>
            http.User.Identity?.IsAuthenticated == true ? Results.Ok(registry.Plugins) : Results.Unauthorized())
            .WithName("ListRpgPluginsV1").Produces<RPGSystemPlugin[]>().Produces(401);
        v1.MapGet("/characters/{id:guid}/sheet", async Task<IResult> (Guid id, HttpContext http, ReadCharacterRules query) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Results.Unauthorized();
            var result = await query.Read(id, actor, http.RequestAborted);
            if (result is null) return Results.NotFound();
            var c = result.Character;
            http.Response.Headers.ETag = $"\"{c.State.Version}\"";
            http.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(new CharacterSheetEnvelope(1, c.Id, c.OwnerId, c.CampaignId, c.SystemId,
                c.System?.TemplateId, c.System?.RulesetVersion, c.System?.TemplateVersion ?? 1, c.System?.Revision ?? 1,
                c.State.Version ?? 1, c.UpdatedAt, c.State,
                c.System?.RendererMode == "layout" ? "custom" : result.Plugin?.RendererId ?? "custom",
                CharacterAccessPolicy.CanEdit(c.OwnerId, actor, c.Role), result.Rules is not null));
        }).WithName("ReadCharacterSheetV1").Produces<CharacterSheetEnvelope>().Produces(401).Produces(404);
    }
}
