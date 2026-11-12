using System.Security.Claims;
using Chronicles.Application.Characters;

namespace Chronicles.Api;

internal static class CharacterAssetHttpResult
{
    internal static Guid? Actor(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor) ? actor : null;
    internal static IResult Complete<T>(CharacterAssetResult<T> result, Func<T, IResult> success) => result.Failure switch
    {
        CharacterAssetFailure.NotFound => Results.NotFound(),
        CharacterAssetFailure.Forbidden => Results.Problem(result.Message, statusCode: 403),
        CharacterAssetFailure.Conflict => Results.Conflict(new { message = result.Message }),
        CharacterAssetFailure.Invalid => Results.ValidationProblem(result.Errors ?? new() { ["asset"] = ["Dados inválidos."] }),
        _ => success(result.Value!)
    };
}
