using System.Text.Json.Serialization;
using Chronicles.Application.CharacterSheets;

namespace Chronicles.Api;

public static partial class CharacterEndpoints
{
    private static void MapCharacterSystems(RouteGroupBuilder api)
    {
        api.MapGet("/v1/characters/{id:guid}/system-migrations/targets", async Task<IResult> (Guid id, HttpContext http, MigrateCharacterSystem migration) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            http.Response.Headers.CacheControl = "private, no-store";
            var response = await migration.Targets(id, actor, http.RequestAborted);
            return CharacterWriteHttpResult.Error(new(Failure: response.Failure, Message: response.Message)) ?? Results.Ok(response.Options);
        }).Produces<SystemMigrationOptions>().Produces(401).Produces(403).Produces(404).ProducesValidationProblem();
        api.MapPost("/v1/characters/{id:guid}/system-migrations/preview", async Task<IResult> (Guid id, MigrationInput input, HttpContext http, MigrateCharacterSystem migration) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            http.Response.Headers.CacheControl = "private, no-store";
            var response = await migration.Preview(id, actor, input.Command(), http.RequestAborted);
            return CharacterWriteHttpResult.Error(new(Failure: response.Failure, Message: response.Message)) ?? Results.Ok(response.Preview);
        }).Produces<SystemMigrationPreview>().Produces(401).Produces(403).Produces(404).Produces(409).ProducesValidationProblem();
        api.MapPost("/v1/characters/{id:guid}/system-migrations", async Task<IResult> (Guid id, ApplyMigrationInput input, HttpContext http, MigrateCharacterSystem migration) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            http.Response.Headers.CacheControl = "private, no-store";
            if (input.Plan is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["plan"] = ["Informe o plano revisado."] });
            return CharacterWriteHttpResult.Legacy(await migration.Apply(id, actor, input.Plan.Command(), input.PreviewHash, http.RequestAborted), actor);
        }).Produces<CharacterDocument>().Produces(401).Produces(403).Produces(404).Produces(409).ProducesValidationProblem();
        api.MapGet("/characters/{id:guid}/system", async Task<IResult> (Guid id, HttpContext http, ReadCharacterSystem query) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            var result = await query.Read(id, actor, http.RequestAborted);
            http.Response.Headers.CacheControl = "private, no-store";
            return !result.Accessible ? Results.NotFound() : Results.Ok(result.System);
        }).Produces<CharacterSystemDocument>().Produces(401).Produces(404);
        api.MapPost("/v1/characters/{id:guid}/system-version", async Task<IResult> (Guid id, PinSystemInput input,
            HttpContext http, PinCharacterSystem command) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            http.Response.Headers.CacheControl = "private, no-store";
            return CharacterWriteHttpResult.Legacy(await command.Execute(id, actor, input.ExpectedVersion, input.RegistrySystemId, input.TemplateRevision, http.RequestAborted), actor);
        }).Produces<CharacterDocument>().Produces(401).Produces(403).Produces(404).Produces(409).ProducesValidationProblem();
    }
    public sealed record PinSystemInput([property: JsonRequired] int ExpectedVersion, [property: JsonRequired] Guid RegistrySystemId,
        [property: JsonRequired] int TemplateRevision);
    public sealed record MigrationInput([property: JsonRequired] int ExpectedVersion, [property: JsonRequired] Guid SourceVersionId,
        [property: JsonRequired] Guid? TargetVersionId, [property: JsonRequired] int TargetRevision, [property: JsonRequired] string TargetHash,
        Dictionary<string, string>? Mappings = null, Dictionary<string, System.Text.Json.JsonElement>? Values = null)
    {
        public SystemMigrationInput Command() => new(ExpectedVersion, SourceVersionId, TargetVersionId, TargetRevision, TargetHash, Mappings, Values);
    }
    public sealed record ApplyMigrationInput([property: JsonRequired] MigrationInput Plan, [property: JsonRequired] string PreviewHash);
}
