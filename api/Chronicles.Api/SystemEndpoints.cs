using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

public static class SystemEndpoints
{
    private static readonly Regex KeyPattern = new("^[a-z][a-z0-9_]{1,39}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void MapSystemEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/systems", async Task<IResult> (HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var systems = await db.RpgSystems.AsNoTracking().Where(x => x.OwnerId == userId).OrderBy(x => x.Name).ToListAsync();
            return Results.Ok(systems.Select(View));
        });

        api.MapGet("/systems/{id:guid}", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var system = await db.RpgSystems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (system is null || system.OwnerId != userId && !await db.CampaignMemberships.AnyAsync(x => x.UserId == userId && x.Campaign.SystemId == id)) return Results.NotFound();
            return Results.Ok(View(system));
        });

        api.MapPost("/systems", async Task<IResult> (SystemInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var errors = Validate(input);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var system = new RpgSystem { OwnerId = userId, Name = input.Name.Trim(), Description = input.Description.Trim(), SchemaJson = JsonSerializer.Serialize(new SystemDefinition(input.Fields, input.Layout)) };
            db.RpgSystems.Add(system);
            await db.SaveChangesAsync();
            return Results.Created($"/api/systems/{system.Id}", View(system));
        });

        api.MapPut("/systems/{id:guid}", async Task<IResult> (Guid id, SystemInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var system = await db.RpgSystems.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
            if (system is null) return Results.NotFound();
            var errors = Validate(input);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            system.Name = input.Name.Trim(); system.Description = input.Description.Trim(); system.SchemaJson = JsonSerializer.Serialize(new SystemDefinition(input.Fields, input.Layout)); system.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(View(system));
        });

        api.MapDelete("/systems/{id:guid}", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var system = await db.RpgSystems.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
            if (system is null) return Results.NotFound();
            if (await db.Campaigns.AnyAsync(x => x.SystemId == id)) return Results.Conflict(new { message = "Remova este sistema das campanhas antes de excluí-lo." });
            db.RpgSystems.Remove(system); await db.SaveChangesAsync();
            return Results.NoContent();
        });

        api.MapPatch("/campaigns/{campaignId:guid}/system", async Task<IResult> (Guid campaignId, SetSystemInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var campaign = await db.Campaigns.SingleOrDefaultAsync(x => x.Id == campaignId && x.OwnerId == userId);
            if (campaign is null) return Results.NotFound();
            var allowed = input.SystemId is null || await db.RpgSystems.AnyAsync(x => x.Id == input.SystemId && x.OwnerId == userId);
            if (!allowed) return Results.ValidationProblem(new Dictionary<string, string[]> { ["systemId"] = ["Sistema indisponível."] });
            campaign.SystemId = input.SystemId; campaign.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static Guid? UserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static object View(RpgSystem system)
    {
        var definition = SystemDefinition.Read(system.SchemaJson);
        return new { system.Id, system.OwnerId, system.Name, system.Description, fields = definition.Fields, layout = definition.Layout, system.CreatedAt, system.UpdatedAt };
    }
    private static Dictionary<string, string[]> Validate(SystemInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.Name?.Trim().Length is not (>= 3 and <= 120) || input.Description is null || input.Description.Length > 2000) errors["system"] = ["Nome ou descrição inválidos."];
        if (input.Fields is null || input.Fields.Length > 50) { errors["fields"] = ["Defina até 50 campos."]; return errors; }
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in input.Fields)
        {
            if (field is null || field.Key is null || !KeyPattern.IsMatch(field.Key) || !keys.Add(field.Key) || field.Label?.Trim().Length is not (>= 2 and <= 80)
                || field.Section?.Trim().Length is not (>= 2 and <= 60) || field.Type is not ("text" or "longtext" or "number" or "boolean" or "select")
                || field.Span is < 1 or > 12
                || field.Type == "number" && (field.Min is < -999999 or > 999999 || field.Max is < -999999 or > 999999 || field.Min > field.Max)
                || field.Type == "select" && (field.Options is null || field.Options.Length is < 1 or > 30 || field.Options.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 80)))
            { errors["fields"] = ["Campo inválido, chave repetida ou limites fora do intervalo."]; break; }
        }
        var layoutError = SystemDefinition.ValidateLayout(input.Layout, input.Fields);
        if (layoutError is not null) errors["layout"] = [layoutError];
        return errors;
    }
    public sealed record SystemInput(string Name, string Description, SystemField[] Fields, SheetLayout? Layout = null);
    public sealed record SetSystemInput(Guid? SystemId);
}
