using System.Security.Claims;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

public static class EncounterEndpoints
{
    public static void MapEncounterEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/campaigns/{campaignId:guid}/encounters", async Task<IResult> (Guid campaignId, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var role = await Role(db, campaignId, userId); if (role is null) return Results.NotFound();
            var query = db.Encounters.AsNoTracking().Where(x => x.CampaignId == campaignId);
            if (!Master(role)) query = query.Where(x => x.Status != "planning");
            var encounters = await query.OrderByDescending(x => x.UpdatedAt).Take(50).ToListAsync();
            return Results.Ok(encounters.Select(x => new { x.Id, x.CampaignId, x.Title, x.Status, x.Round, x.TurnIndex, x.UpdatedAt }));
        });

        api.MapPost("/campaigns/{campaignId:guid}/encounters", async Task<IResult> (Guid campaignId, CreateEncounterInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var role = await Role(db, campaignId, userId); if (role is null) return Results.NotFound();
            if (!Master(role)) return Results.Problem("Apenas mestres podem criar encontros.", statusCode: 403);
            if (input.Title?.Trim().Length is not (>= 2 and <= 160)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["title"] = ["Título deve ter entre 2 e 160 caracteres."] });
            var encounter = new Encounter { CampaignId = campaignId, Title = input.Title.Trim() };
            db.Encounters.Add(encounter); await db.SaveChangesAsync();
            return Results.Created($"/api/encounters/{encounter.Id}", View(encounter));
        });

        api.MapGet("/encounters/{id:guid}", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var encounter = await Load(db, id, true); if (encounter is null) return Results.NotFound();
            var role = await Role(db, encounter.CampaignId, userId);
            if (role is null || encounter.Status == "planning" && !Master(role)) return Results.NotFound();
            return Results.Ok(View(encounter));
        });

        api.MapPatch("/encounters/{id:guid}/status", async Task<IResult> (Guid id, StatusInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var encounter = await Load(db, id); if (encounter is null) return Results.NotFound();
            var role = await Role(db, encounter.CampaignId, userId); if (role is null) return Results.NotFound();
            if (!Master(role)) return Results.Problem("Apenas mestres controlam encontros.", statusCode: 403);
            if (input.Status is not ("planning" or "active" or "completed")) return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Status inválido."] });
            if (input.Status == "active" && await db.Encounters.AnyAsync(x => x.CampaignId == encounter.CampaignId && x.Id != id && x.Status == "active"))
                return Results.Conflict(new { message = "Já existe um encontro ativo nesta campanha." });
            encounter.Status = input.Status; encounter.UpdatedAt = DateTime.UtcNow;
            if (input.Status == "active" && encounter.Round < 1) encounter.Round = 1;
            await db.SaveChangesAsync(); return Results.NoContent();
        });

        api.MapPost("/encounters/{id:guid}/participants", async Task<IResult> (Guid id, AddParticipantInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var encounter = await Load(db, id); if (encounter is null) return Results.NotFound();
            var role = await Role(db, encounter.CampaignId, userId); if (role is null) return Results.NotFound();
            if (!Master(role)) return Results.Problem("Apenas mestres alteram participantes.", statusCode: 403);
            if (encounter.Status == "completed") return Results.Conflict(new { message = "Encontro concluído." });
            if (input.Initiative is < -999 or > 999) return Results.ValidationProblem(new Dictionary<string, string[]> { ["initiative"] = ["Iniciativa fora do limite."] });
            var character = await db.Characters.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.CharacterId && x.CampaignId == encounter.CampaignId);
            if (character is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["characterId"] = ["Personagem indisponível."] });
            if (await db.EncounterParticipants.AnyAsync(x => x.EncounterId == id && x.CharacterId == character.Id)) return Results.Conflict(new { message = "Personagem já participa deste encontro." });
            var participant = new EncounterParticipant { EncounterId = id, CharacterId = character.Id, Initiative = input.Initiative, Health = character.Health, MaxHealth = character.MaxHealth };
            db.EncounterParticipants.Add(participant); encounter.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Created($"/api/encounters/{id}", new { participant.Id, participant.CharacterId, character.Name, participant.Initiative, participant.Health, participant.MaxHealth, participant.Conditions });
        });

        api.MapPatch("/encounter-participants/{id:guid}", async Task<IResult> (Guid id, ParticipantInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var participant = await db.EncounterParticipants.Include(x => x.Encounter).Include(x => x.Character).SingleOrDefaultAsync(x => x.Id == id);
            if (participant is null) return Results.NotFound();
            var role = await Role(db, participant.Encounter.CampaignId, userId); if (role is null) return Results.NotFound();
            if (!Master(role)) return Results.Problem("Apenas mestres alteram participantes.", statusCode: 403);
            if (participant.Encounter.Status == "completed") return Results.Conflict(new { message = "Encontro concluído." });
            if (input.MaxHealth is < 1 or > 9999 || input.Health < 0 || input.Health > input.MaxHealth || input.Initiative is < -999 or > 999 || input.Conditions is null || input.Conditions.Length > 1000)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["participant"] = ["Verifique vida, iniciativa e condições."] });
            participant.Health = input.Health; participant.MaxHealth = input.MaxHealth; participant.Initiative = input.Initiative; participant.Conditions = input.Conditions.Trim();
            participant.Character.Health = input.Health; participant.Character.MaxHealth = input.MaxHealth; participant.Character.UpdatedAt = DateTime.UtcNow;
            participant.Encounter.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { participant.Id, participant.CharacterId, name = participant.Character.Name, participant.Initiative, participant.Health, participant.MaxHealth, participant.Conditions });
        });

        api.MapDelete("/encounter-participants/{id:guid}", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var participant = await db.EncounterParticipants.Include(x => x.Encounter).SingleOrDefaultAsync(x => x.Id == id);
            if (participant is null) return Results.NotFound();
            var role = await Role(db, participant.Encounter.CampaignId, userId); if (role is null) return Results.NotFound();
            if (!Master(role)) return Results.Problem("Apenas mestres alteram participantes.", statusCode: 403);
            db.EncounterParticipants.Remove(participant); participant.Encounter.TurnIndex = 0; participant.Encounter.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(); return Results.NoContent();
        });

        api.MapPost("/encounters/{id:guid}/advance", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            await using var transaction = await db.Database.BeginTransactionAsync(http.RequestAborted);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({id.ToString()}))", http.RequestAborted);
            var encounter = await Load(db, id, true); if (encounter is null) return Results.NotFound();
            var role = await Role(db, encounter.CampaignId, userId); if (role is null) return Results.NotFound();
            if (!Master(role)) return Results.Problem("Apenas mestres controlam turnos.", statusCode: 403);
            if (encounter.Status != "active" || encounter.Participants.Count == 0) return Results.Conflict(new { message = "Ative o encontro e adicione participantes." });
            encounter.TurnIndex++;
            if (encounter.TurnIndex >= encounter.Participants.Count) { encounter.TurnIndex = 0; encounter.Round++; }
            encounter.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(http.RequestAborted); await transaction.CommitAsync(http.RequestAborted);
            return Results.Ok(View(encounter));
        });

        api.MapDelete("/encounters/{id:guid}", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var encounter = await Load(db, id); if (encounter is null) return Results.NotFound();
            var role = await Role(db, encounter.CampaignId, userId); if (role is null) return Results.NotFound();
            if (!Master(role)) return Results.Problem("Apenas mestres excluem encontros.", statusCode: 403);
            db.Encounters.Remove(encounter); await db.SaveChangesAsync(); return Results.NoContent();
        });
    }

    private static Guid? UserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static Task<string?> Role(AppDbContext db, Guid campaignId, Guid userId) => db.CampaignMemberships.AsNoTracking().Where(x => x.CampaignId == campaignId && x.UserId == userId).Select(x => x.Role).SingleOrDefaultAsync();
    private static bool Master(string role) => role is "owner" or "gm" or "co-gm";
    private static Task<Encounter?> Load(AppDbContext db, Guid id, bool participants = false) => participants
        ? db.Encounters.Include(x => x.Participants).ThenInclude(x => x.Character).SingleOrDefaultAsync(x => x.Id == id)
        : db.Encounters.SingleOrDefaultAsync(x => x.Id == id);
    private static object View(Encounter x)
    {
        var participants = x.Participants.OrderByDescending(p => p.Initiative).ThenBy(p => p.Id)
            .Select(p => new { p.Id, p.CharacterId, name = p.Character.Name, p.Initiative, p.Health, p.MaxHealth, p.Conditions }).ToList();
        return new { x.Id, x.CampaignId, x.Title, x.Status, x.Round, x.TurnIndex, x.CreatedAt, x.UpdatedAt, participants };
    }
    public sealed record CreateEncounterInput(string Title);
    public sealed record StatusInput(string Status);
    public sealed record AddParticipantInput(Guid CharacterId, int Initiative);
    public sealed record ParticipantInput(int Initiative, int Health, int MaxHealth, string Conditions);
}
