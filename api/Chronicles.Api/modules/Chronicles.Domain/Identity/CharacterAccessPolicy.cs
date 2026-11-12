namespace Chronicles.Domain;

public enum CharacterAccessRequirement { Edit, Member, Master, Owner }

public static class CharacterAccessPolicy
{
    public static bool IsMaster(string role) => role is "owner" or "gm" or "co-gm";
    public static bool CanEdit(Guid ownerId, Guid actorId, string role) =>
        role != "spectator" && (ownerId == actorId || IsMaster(role));
    public static bool Allows(CharacterAccessRequirement requirement, Guid ownerId, Guid actorId, string role) => requirement switch
    {
        CharacterAccessRequirement.Member => true, // A context is only issued to an authenticated campaign member.
        CharacterAccessRequirement.Master => IsMaster(role),
        CharacterAccessRequirement.Owner => ownerId == actorId && role != "spectator",
        CharacterAccessRequirement.Edit => CanEdit(ownerId, actorId, role),
        _ => false
    };
}
