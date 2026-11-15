using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.CharacterSheets;
using Chronicles.Infrastructure.Characters;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public static class CharacterDeletionPersistenceTests
{
    public static async Task Run(Func<AppDbContext> context)
    {
        static void Check(bool value,string label){if(!value)throw new Exception("Character deletion PostgreSQL: "+label);}
        var actor=new User{Email="deletion-db@example.test",PasswordHash="fixture",DisplayName="Deletion fixture"};
        var campaign=new Campaign{Name="Deletion",OwnerId=actor.Id};
        var character=new Character{Name="Delete fixture",OwnerId=actor.Id,CampaignId=campaign.Id};
        await using(var db=context())
        {
            db.Users.Add(actor);db.Campaigns.Add(campaign);db.CampaignMemberships.Add(new(){CampaignId=campaign.Id,UserId=actor.Id,Role="owner"});db.Characters.Add(character);
            db.CharacterSecrets.Add(new(){CharacterId=character.Id,Notes="Private fixture"});db.CharacterFavorites.Add(new(){CharacterId=character.Id,UserId=actor.Id});
            db.CharacterInventoryItems.Add(new(){CharacterId=character.Id,Name="Item"});db.CharacterAbilities.Add(new(){CharacterId=character.Id,Name="Ability"});
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION deletion_failure_fixture() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'forced serialization failure' USING ERRCODE='40001'; END; $$");
            // This database belongs exclusively to this test run; no external identifiers enter DDL.
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER deletion_failure_fixture BEFORE DELETE ON characters FOR EACH ROW EXECUTE FUNCTION deletion_failure_fixture()");
        }
        try
        {
            await using var probe=context();var events=await probe.OutboxMessages.CountAsync();
            await using(var db=context())
            {
                var result=await new DeleteCharacter(new EfCharacterDeletionStore(db)).Execute(character.Id,1,actor.Id);
                Check(result.Failure==CharacterWriteFailure.Conflict,"wrapped SQLSTATE 40001 becomes conflict");
            }
            Check(await probe.Characters.AnyAsync(c=>c.Id==character.Id)&&await probe.CharacterInventoryItems.AnyAsync(i=>i.CharacterId==character.Id)&&await probe.OutboxMessages.CountAsync()==events,"serialization failure rolls back character, children and outbox");
            await probe.Database.ExecuteSqlRawAsync("CREATE OR REPLACE FUNCTION deletion_failure_fixture() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'unrelated failure' USING ERRCODE='P0001'; END; $$");
            try
            {
                await using var db=context();await new DeleteCharacter(new EfCharacterDeletionStore(db)).Execute(character.Id,1,actor.Id);
                throw new Exception("Unrelated database error was swallowed.");
            }
            catch(Exception error) when(error.GetBaseException() is PostgresException {SqlState:"P0001"}){}
            Check(await probe.Characters.AnyAsync(c=>c.Id==character.Id)&&await probe.OutboxMessages.CountAsync()==events,"unknown error is not disguised as concurrency and leaves no partial event");
        }
        finally
        {
            await using var db=context();await db.Database.ExecuteSqlRawAsync("DROP TRIGGER deletion_failure_fixture ON characters");await db.Database.ExecuteSqlRawAsync("DROP FUNCTION deletion_failure_fixture()");
        }
        async Task<CharacterWriteResponse> Delete(){await using var db=context();return await new DeleteCharacter(new EfCharacterDeletionStore(db)).Execute(character.Id,1,actor.Id);}
        var race=await Task.WhenAll(Delete(),Delete());
        Check(race.Count(r=>r.Failure is null)==1&&race.All(r=>r.Failure is null or CharacterWriteFailure.Conflict or CharacterWriteFailure.NotFound),"exactly one concurrent deletion succeeds without unexpected exceptions");
        await using(var db=context())
        {
            Check(!await db.Characters.AnyAsync(c=>c.Id==character.Id)&&!await db.CharacterSecrets.AnyAsync(s=>s.CharacterId==character.Id)&&!await db.CharacterFavorites.AnyAsync(f=>f.CharacterId==character.Id)&&!await db.CharacterInventoryItems.AnyAsync(i=>i.CharacterId==character.Id)&&!await db.CharacterAbilities.AnyAsync(a=>a.CharacterId==character.Id),"successful deletion removes dependent private/public data");
            var payloads=await db.OutboxMessages.Select(m=>m.Payload).ToListAsync();
            Check(payloads.Count(payload=>payload.Contains("CharacterDeleted",StringComparison.Ordinal)&&payload.Contains(character.Id.ToString(),StringComparison.Ordinal))==1,"one durable deletion event");
        }
        Console.WriteLine("Character deletion PostgreSQL: wrapped serialization conflict, unrelated errors, transactional rollback, concurrent deletion, child cascades and one durable event passed.");
    }
}
