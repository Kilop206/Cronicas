using System.Security.Cryptography;
using System.Text.Json;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

public static partial class CharacterEndpoints
{
    private static void MapRuleActions(RouteGroupBuilder api)
    {
        api.MapGet("/characters/{id:guid}/rules", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            if (!await CharacterResourceAccess.CanRead(db, id, userId)) return Results.NotFound();
            var character = await db.Characters.AsNoTracking().SingleAsync(x => x.Id == id);
            var definition = await RulesDefinition(db, character.CampaignId);
            if (definition is null || !SheetRuleActions.Supports(definition)) return Results.Ok(new { supported = false });
            try { return Results.Ok(new { supported = true, rules = SheetRuleActions.Describe(definition, RuleInput(character)) }); }
            catch (ArgumentException error) { return RuleError(error.Message); }
        });
        api.MapPost("/characters/{id:guid}/rules", async Task<IResult> (Guid id, RuleExecutionInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            if (input.Command is null) return RuleError("Informe uma ação válida.");
            var access = await CharacterResourceAccess.CanEdit(db, id, userId);
            if (access is null) return Results.NotFound();
            if (access == false) return Results.Problem("Você não pode alterar este personagem.", statusCode: 403);
            var character = await db.Characters.SingleAsync(x => x.Id == id);
            if (input.ExpectedVersion != character.Version) return Results.Conflict(new { message = "A ficha mudou. Atualize antes de aplicar uma regra." });
            var role = await db.CampaignMemberships.Where(x => x.CampaignId == character.CampaignId && x.UserId == userId).Select(x => x.Role).SingleAsync();
            var master = role is "owner" or "gm" or "co-gm";
            if (input.Command.Visibility == "gm" && !master) return Results.Problem("Rolagem exclusiva do mestre.", statusCode: 403);
            if (input.Command.Action == "configure" && input.Command.Option == "sorte_opcional" && !master)
                return Results.Problem("Apenas o mestre pode ativar a regra opcional de sorte.", statusCode: 403);
            var definition = await RulesDefinition(db, character.CampaignId);
            if (definition is null) return RuleError("A campanha não tem sistema associado.");
            RuleResult result;
            try { result = SheetRuleActions.Apply(definition, RuleInput(character), input.Command, sides => RandomNumberGenerator.GetInt32(1, sides + 1)); }
            catch (ArgumentException error) { return RuleError(error.Message); }
            var errors = Validate(result.Character);
            if (errors is not null) return Results.ValidationProblem(errors);
            var fieldErrors = await ValidateFields(db, character.CampaignId, result.Character.Fields, character.FieldsJson, allowGm: master, authorizationJson: character.FieldsJson);
            if (fieldErrors is not null) return Results.ValidationProblem(fieldErrors);
            CharacterHistory.Capture(db, character, userId);
            Apply(character, result.Character);
            foreach (var roll in result.Rolls) db.DiceRolls.Add(new DiceRoll { CampaignId = character.CampaignId, UserId = userId, RequestId = Guid.NewGuid(),
                Expression = roll.Expression, RollsJson = JsonSerializer.Serialize(roll.Rolls), KeptIndexesJson = JsonSerializer.Serialize(roll.KeptIndexes),
                Modifier = roll.Modifier, Total = roll.Total, Visibility = input.Command.Visibility });
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "A ficha mudou. A ação não foi aplicada; atualize a página." }); }
            return Results.Ok(new { character = View(character, true), message = result.Message, rolls = result.Rolls });
        }).RequireRateLimiting("rolls");
    }
    private static async Task<SystemDefinition?> RulesDefinition(AppDbContext db, Guid campaignId)
    {
        var schema = await db.Campaigns.Where(c => c.Id == campaignId).Join(db.RpgSystems, c => c.SystemId, s => s.Id, (_, s) => s.SchemaJson).SingleOrDefaultAsync();
        return schema is null ? null : SystemDefinition.Read(schema);
    }
    private static CharacterInput RuleInput(Character c) => new(c.Name, c.PortraitUrl, c.BackgroundUrl, c.Health, c.MaxHealth, c.Sanity, c.MaxSanity, c.Mana, c.MaxMana,
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(c.FieldsJson), c.Version);
    private static IResult RuleError(string message) => Results.ValidationProblem(new Dictionary<string, string[]> { ["rules"] = [message] });
    public sealed record RuleExecutionInput(int ExpectedVersion, RuleCommand Command);
}
