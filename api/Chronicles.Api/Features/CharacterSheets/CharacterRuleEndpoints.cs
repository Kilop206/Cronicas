using Chronicles.Application.CharacterSheets;

namespace Chronicles.Api;

public static partial class CharacterEndpoints
{
    private static void MapRuleActions(RouteGroupBuilder api)
    {
        api.MapGet("/characters/{id:guid}/rules", async Task<IResult> (Guid id, HttpContext http, ReadCharacterRules query) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            try
            {
                var result = await query.Read(id, actor, http.RequestAborted);
                if (result is null) return Results.NotFound();
                return Results.Ok(new { supported = result.Rules is not null, rules = result.Rules });
            }
            catch (ArgumentException error) { return RuleError(error.Message); }
        });

        api.MapPost("/characters/{id:guid}/rules", async Task<IResult> (Guid id, RuleExecutionInput input,
            HttpContext http, ExecuteCharacterRule command) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            var response = await command.Execute(id, actor, input.ExpectedVersion, input.Command, http.RequestAborted);
            if (response.Failure is { } failure) return failure switch
            {
                RuleFailure.NotFound => Results.NotFound(),
                RuleFailure.Forbidden => Results.Problem(response.Message, statusCode: 403),
                RuleFailure.Conflict => Results.Conflict(new { message = response.Message }),
                _ => Results.ValidationProblem(response.Errors ?? new() { ["rules"] = [response.Message ?? "Ação inválida."] })
            };
            var c = response.Character!;
            return Results.Ok(new { character = CharacterSheetProjection.Document(c, actor),
                response.Result!.Message, response.Result.Rolls, response.Result.Damage });
        }).RequireRateLimiting("rolls");
    }

    private static IResult RuleError(string message) => Results.ValidationProblem(new Dictionary<string, string[]> { ["rules"] = [message] });
    public sealed record RuleExecutionInput(int ExpectedVersion, RuleCommand? Command);
}
