using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.Characters;
using Chronicles.Domain;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Infrastructure.Characters;

public sealed class EfCharacterAbilityStore(AppDbContext db) : ICharacterAbilityStore
{
    private const string Denied = "Você não pode editar as habilidades deste personagem.";
    public Task<CharacterAssetAccess?> LoadAccess(Guid character, Guid actor, CancellationToken ct) => CharacterAssetPersistence.LoadAccess(db, character, actor, ct);
    private IQueryable<CharacterAbility> Visible(Guid character, Guid actor) => db.CharacterAbilities.AsNoTracking()
        .Where(a => a.CharacterId == character && a.Character.Campaign.Memberships.Any(m => m.UserId == actor));

    public async Task<CharacterAbilityDocument[]?> List(Guid character, Guid actor, CancellationToken ct)
    {
        if (await LoadAccess(character, actor, ct) is null) return null;
        return (await Visible(character, actor).OrderBy(a => a.Name).ToArrayAsync(ct)).Select(Document).ToArray();
    }

    public async Task<CharacterAbilityDocument?> Find(Guid character, Guid ability, Guid actor, CancellationToken ct) =>
        await Visible(character, actor).SingleOrDefaultAsync(a => a.Id == ability, ct) is { } found ? Document(found) : null;

    public Task<CharacterAssetResult<CharacterAbilityDocument>> Create(CharacterAssetAccess access, CharacterAbilityValues values, Guid actor, CancellationToken ct) =>
        CharacterAssetPersistence.Write(db, access, actor, Denied, "As habilidades mudaram. Atualize antes de salvar.", async () =>
        {
            if (await db.CharacterAbilities.CountAsync(a => a.CharacterId == access.CharacterId, ct) >= CharacterAbilityValues.Capacity)
                return new(Failure: CharacterAssetFailure.Conflict, Message: "A ficha atingiu o limite de 100 habilidades.");
            var ability = new CharacterAbility { CharacterId = access.CharacterId, Name = values.Name };
            Apply(ability, values); db.CharacterAbilities.Add(ability);
            return new CharacterAssetResult<CharacterAbilityDocument>(Document(ability));
        }, ct);

    public Task<CharacterAssetResult<CharacterAbilityDocument>> Update(CharacterAssetAccess access, CharacterAbilityDocument before, CharacterAbilityValues values, Guid actor, CancellationToken ct) =>
        Change(access, before, values, actor, ct);
    public Task<CharacterAssetResult<CharacterAbilityDocument>> Delete(CharacterAssetAccess access, CharacterAbilityDocument before, Guid actor, CancellationToken ct) =>
        Change(access, before, null, actor, ct);

    private Task<CharacterAssetResult<CharacterAbilityDocument>> Change(CharacterAssetAccess access, CharacterAbilityDocument before, CharacterAbilityValues? values, Guid actor, CancellationToken ct) =>
        CharacterAssetPersistence.Write(db, access, actor, Denied, values is null ? "A habilidade mudou. Atualize antes de excluir." : "A habilidade mudou. Atualize antes de salvar.", async () =>
        {
            var ability = await db.CharacterAbilities.SingleOrDefaultAsync(a => a.Id == before.Id && a.CharacterId == access.CharacterId, ct);
            if (ability is null) return new(Failure: CharacterAssetFailure.NotFound);
            if (ability.Version != before.Version) return new(Failure: CharacterAssetFailure.Conflict, Message: "A habilidade mudou. Atualize antes de salvar.");
            if (values is null) db.CharacterAbilities.Remove(ability);
            else { Apply(ability, values); ability.Version++; ability.UpdatedAt = DateTime.UtcNow; }
            return new CharacterAssetResult<CharacterAbilityDocument>(Document(ability));
        }, ct);

    private static void Apply(CharacterAbility a, CharacterAbilityValues v)
    { a.Name = v.Name; a.Description = v.Description; a.Cost = v.Cost; a.Effect = v.Effect; a.Duration = v.Duration;
      a.Limitations = v.Limitations; a.Recharge = v.Recharge; a.RollExpression = v.RollExpression; }
    private static CharacterAbilityDocument Document(CharacterAbility a) => new(a.Id, a.CharacterId, a.Name, a.Description, a.Cost, a.Effect,
        a.Duration, a.Limitations, a.Recharge, a.RollExpression, a.Version, a.CreatedAt, a.UpdatedAt);
}
