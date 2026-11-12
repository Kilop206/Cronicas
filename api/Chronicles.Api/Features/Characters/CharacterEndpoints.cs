using System.Security.Claims;

namespace Chronicles.Api;

public static partial class CharacterEndpoints
{
    public static void MapCharacterEndpoints(this RouteGroupBuilder api)
    {
        MapRuleActions(api);
        MapCharacterWrites(api);
        MapCharacterReads(api);
        MapCharacterLifecycle(api);
        MapCharacterCompanions(api);
        MapCharacterSystems(api);
    }

    private static Guid? UserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
