using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

public static class CharacterInventoryEndpoints
{
    public static void MapCharacterInventoryEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/characters/{characterId:guid}/inventory", async Task<IResult> (Guid characterId, HttpContext http, AppDbContext db) =>
        {
            if (CharacterResourceAccess.UserId(http) is not { } userId) return Results.Unauthorized();
            if (!await CharacterResourceAccess.CanRead(db, characterId, userId)) return Results.NotFound();
            var items = await db.CharacterInventoryItems.AsNoTracking().Where(x => x.CharacterId == characterId)
                .OrderBy(x => x.Category).ThenBy(x => x.Name).ToListAsync();
            return Results.Ok(items.Select(View));
        });

        api.MapPost("/characters/{characterId:guid}/inventory", async Task<IResult> (Guid characterId, InventoryInput input, HttpContext http, AppDbContext db) =>
        {
            if (CharacterResourceAccess.UserId(http) is not { } userId) return Results.Unauthorized();
            var access = await CharacterResourceAccess.CanEdit(db, characterId, userId);
            if (access is null) return Results.NotFound();
            if (!access.Value) return Results.Problem("Você não pode editar este inventário.", statusCode: 403);
            var errors = Validate(input);
            if (errors is not null) return Results.ValidationProblem(errors);
            if (await db.CharacterInventoryItems.CountAsync(x => x.CharacterId == characterId) >= 200)
                return Results.Conflict(new { message = "O inventário atingiu o limite de 200 itens." });
            var item = new CharacterInventoryItem { CharacterId = characterId, Name = input.Name.Trim() };
            Apply(item, input);
            db.CharacterInventoryItems.Add(item);
            await db.SaveChangesAsync();
            return Results.Created($"/api/characters/{characterId}/inventory/{item.Id}", View(item));
        });

        api.MapPut("/characters/{characterId:guid}/inventory/{itemId:guid}", async Task<IResult> (Guid characterId, Guid itemId, InventoryInput input, HttpContext http, AppDbContext db) =>
        {
            if (CharacterResourceAccess.UserId(http) is not { } userId) return Results.Unauthorized();
            var access = await CharacterResourceAccess.CanEdit(db, characterId, userId);
            if (access is null) return Results.NotFound();
            if (!access.Value) return Results.Problem("Você não pode editar este inventário.", statusCode: 403);
            var item = await db.CharacterInventoryItems.SingleOrDefaultAsync(x => x.Id == itemId && x.CharacterId == characterId);
            if (item is null) return Results.NotFound();
            if (input.Version != item.Version) return Results.Conflict(new { message = "O item mudou. Atualize antes de salvar." });
            var errors = Validate(input);
            if (errors is not null) return Results.ValidationProblem(errors);
            Apply(item, input);
            item.Version++;
            item.UpdatedAt = DateTime.UtcNow;
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "O item mudou. Atualize antes de salvar." }); }
            return Results.Ok(View(item));
        });

        api.MapDelete("/characters/{characterId:guid}/inventory/{itemId:guid}", async Task<IResult> (Guid characterId, Guid itemId, int version, HttpContext http, AppDbContext db) =>
        {
            if (CharacterResourceAccess.UserId(http) is not { } userId) return Results.Unauthorized();
            var access = await CharacterResourceAccess.CanEdit(db, characterId, userId);
            if (access is null) return Results.NotFound();
            if (!access.Value) return Results.Problem("Você não pode editar este inventário.", statusCode: 403);
            var item = await db.CharacterInventoryItems.SingleOrDefaultAsync(x => x.Id == itemId && x.CharacterId == characterId);
            if (item is null) return Results.NotFound();
            if (version != item.Version) return Results.Conflict(new { message = "O item mudou. Atualize antes de excluir." });
            db.CharacterInventoryItems.Remove(item);
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "O item mudou. Atualize antes de excluir." }); }
            return Results.NoContent();
        });
    }

    private static Dictionary<string, string[]>? Validate(InventoryInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.Name?.Trim().Length is not (>= 1 and <= 120)) errors["name"] = ["Informe um nome com até 120 caracteres."];
        if (input.Category is not ("item" or "equipment" or "weapon" or "consumable" or "other")) errors["category"] = ["Categoria inválida."];
        if (input.Quantity is < 1 or > 9999) errors["quantity"] = ["Quantidade entre 1 e 9999."];
        if (input.Weight is < 0 or > 9999.99m || decimal.Round(input.Weight, 2) != input.Weight) errors["weight"] = ["Peso entre 0 e 9999,99 com até duas casas decimais."];
        if (input.Notes is null || input.Notes.Length > 1000) errors["notes"] = ["Notas com até 1000 caracteres."];
        return errors.Count == 0 ? null : errors;
    }

    private static void Apply(CharacterInventoryItem item, InventoryInput input)
    {
        item.Name = input.Name.Trim(); item.Category = input.Category; item.Quantity = input.Quantity;
        item.Weight = input.Weight; item.Notes = input.Notes.Trim();
    }

    private static object View(CharacterInventoryItem x) => new { x.Id, x.CharacterId, x.Name, x.Category, x.Quantity, x.Weight, x.Notes, x.Version, x.CreatedAt, x.UpdatedAt };
    public sealed record InventoryInput(string Name, string Category, int Quantity, decimal Weight, string Notes, int Version);
}
