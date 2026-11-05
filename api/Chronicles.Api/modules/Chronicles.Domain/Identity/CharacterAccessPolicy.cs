namespace Chronicles.Domain;

public static class CharacterAccessPolicy
{
    public static bool IsMaster(string role) => role is "owner" or "gm" or "co-gm";
    public static bool CanEdit(Guid ownerId, Guid actorId, string role) =>
        role != "spectator" && (ownerId == actorId || IsMaster(role));
}
