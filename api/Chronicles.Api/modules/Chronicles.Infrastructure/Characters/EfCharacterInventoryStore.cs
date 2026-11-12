using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.Characters;
using Chronicles.Domain;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Infrastructure.Characters;

public sealed class EfCharacterInventoryStore(AppDbContext db) : ICharacterInventoryStore
{
    private const string Denied = "Você não pode editar este inventário.";
    public Task<CharacterAssetAccess?> LoadAccess(Guid character, Guid actor, CancellationToken ct) => CharacterAssetPersistence.LoadAccess(db, character, actor, ct);
    private IQueryable<CharacterInventoryItem> Visible(Guid character, Guid actor) => db.CharacterInventoryItems.AsNoTracking()
        .Where(i => i.CharacterId == character && i.Character.Campaign.Memberships.Any(m => m.UserId == actor));

    public async Task<InventoryItemDocument[]?> List(Guid character, Guid actor, CancellationToken ct)
    {
        if (await LoadAccess(character, actor, ct) is null) return null;
        return (await Visible(character, actor).OrderBy(i => i.Category).ThenBy(i => i.Name).ToArrayAsync(ct)).Select(Document).ToArray();
    }

    public async Task<InventoryItemDocument?> Find(Guid character, Guid item, Guid actor, CancellationToken ct) =>
        await Visible(character, actor).SingleOrDefaultAsync(i => i.Id == item, ct) is { } found ? Document(found) : null;

    public Task<CharacterAssetResult<InventoryItemDocument>> Create(CharacterAssetAccess access, InventoryItemValues values, Guid actor, CancellationToken ct) =>
        CharacterAssetPersistence.Write(db, access, actor, Denied, "O inventário mudou. Atualize antes de salvar.", async () =>
        {
            if (await db.CharacterInventoryItems.CountAsync(i => i.CharacterId == access.CharacterId, ct) >= InventoryItemValues.Capacity)
                return new(Failure: CharacterAssetFailure.Conflict, Message: "O inventário atingiu o limite de 200 itens.");
            var item = new CharacterInventoryItem { CharacterId = access.CharacterId, Name = values.Name };
            Apply(item, values); db.CharacterInventoryItems.Add(item);
            return new CharacterAssetResult<InventoryItemDocument>(Document(item));
        }, ct);

    public Task<CharacterAssetResult<InventoryItemDocument>> Update(CharacterAssetAccess access, InventoryItemDocument before, InventoryItemValues values, Guid actor, CancellationToken ct) =>
        Change(access, before, values, actor, ct);
    public Task<CharacterAssetResult<InventoryItemDocument>> Delete(CharacterAssetAccess access, InventoryItemDocument before, Guid actor, CancellationToken ct) =>
        Change(access, before, null, actor, ct);

    private Task<CharacterAssetResult<InventoryItemDocument>> Change(CharacterAssetAccess access, InventoryItemDocument before, InventoryItemValues? values, Guid actor, CancellationToken ct) =>
        CharacterAssetPersistence.Write(db, access, actor, Denied, values is null ? "O item mudou. Atualize antes de excluir." : "O item mudou. Atualize antes de salvar.", async () =>
        {
            var item = await db.CharacterInventoryItems.SingleOrDefaultAsync(i => i.Id == before.Id && i.CharacterId == access.CharacterId, ct);
            if (item is null) return new(Failure: CharacterAssetFailure.NotFound);
            if (item.Version != before.Version) return new(Failure: CharacterAssetFailure.Conflict, Message: "O item mudou. Atualize antes de salvar.");
            if (values is null) db.CharacterInventoryItems.Remove(item);
            else { Apply(item, values); item.Version++; item.UpdatedAt = DateTime.UtcNow; }
            return new CharacterAssetResult<InventoryItemDocument>(Document(item));
        }, ct);

    private static void Apply(CharacterInventoryItem item, InventoryItemValues v)
    { item.Name = v.Name; item.Category = v.Category; item.Quantity = v.Quantity; item.Weight = v.Weight; item.Notes = v.Notes; }
    private static InventoryItemDocument Document(CharacterInventoryItem i) => new(i.Id, i.CharacterId, i.Name, i.Category, i.Quantity, i.Weight, i.Notes, i.Version, i.CreatedAt, i.UpdatedAt);
}
