using System.Text.Json;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.Campaigns;
using Chronicles.Application.CharacterSheets;
using Chronicles.Domain;
using Chronicles.Domain.Campaigns;
using Chronicles.Domain.Events;
using Chronicles.Infrastructure.Campaigns;
using Chronicles.Infrastructure.Characters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public static class CampaignPersistenceTests
{
    public static async Task Run(Func<AppDbContext> context)
    {
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Campaign PostgreSQL: " + label); }
        static User User(string name) => new() { Email = $"campaign-{name}@example.test", DisplayName = name, PasswordHash = "fixture" };
        static async Task<GameEvent[]> Events(AppDbContext db) => (await db.OutboxMessages.AsNoTracking().Select(e => e.Payload).ToArrayAsync())
            .Select(json => JsonSerializer.Deserialize<GameEvent>(json)!).ToArray();
        var owner = User("owner"); var a = User("first"); var b = User("second"); var c = User("third");
        var tokens = new CryptographicCampaignInviteTokenGenerator();
        CampaignDocument campaign;
        await using (var db = context())
        {
            db.Users.AddRange(owner, a, b, c); await db.SaveChangesAsync();
            campaign = (await new ManageCampaigns(new EfCampaignStore(db)).Create(owner.Id, "  Campaign module  ", " description ", "#aBc123")).Value!;
            Check(campaign.Name == "Campaign module" && campaign.Description == "description" && campaign.Status == "planning", "create returns normalized legacy contract");
            Check(await db.CampaignMemberships.AnyAsync(m => m.CampaignId == campaign.Id && m.UserId == owner.Id && m.Role == "owner"), "owner membership created atomically");
            Check((await Events(db)).Count(e => e.CampaignId == campaign.Id) == 2, "campaign and owner events committed together");
            var reader = new ReadCampaigns(new EfCampaignStore(db));
            Check((await reader.List(a.Id, 1, null)).Total == 0 && await reader.Read(campaign.Id, a.Id) is null, "list and reads isolated by membership");
            Check((await reader.List(owner.Id, -1, " module ")).Items.Single().Id == campaign.Id, "search/page contract retained");
        }
        async Task<CampaignInviteDocument> Invite(string role = "player")
        { await using var db = context(); return (await new ManageCampaignInvites(new EfCampaignInviteStore(db), tokens).Create(campaign.Id, owner.Id, role)).Value!; }
        async Task<CampaignResult<CampaignJoined>> Accept(CampaignInviteDocument invite, Guid user)
        { await using var db = context(); return await new ManageCampaignInvites(new EfCampaignInviteStore(db), tokens).Accept(invite.Token, user); }
        var same = await Invite();
        await using (var db = context())
        {
            var persisted = await db.CampaignInvites.SingleAsync(i => i.TokenHash == CampaignInviteToken.Hash(same.Token));
            Check(persisted.TokenHash.Length == 64 && persisted.TokenHash != same.Token && same.Token.Length == 48, "capability generated with RNG and only hash persisted");
        }
        var competing = await Task.WhenAll(Accept(same, a.Id), Accept(same, b.Id));
        Check(competing.Count(r => r.Failure is null) == 1 && competing.Count(r => r.Failure == CampaignFailure.NotFound) == 1, "same token admits exactly one user");
        var member = competing[0].Failure is null ? a : b;
        var other = member.Id == a.Id ? b : a;
        var distinctA = await Invite(); var distinctB = await Invite();
        var sameUser = await Task.WhenAll(Accept(distinctA, c.Id), Accept(distinctB, c.Id));
        Check(sameUser.Count(r => r.Failure is null) == 1 && sameUser.Count(r => r.Failure == CampaignFailure.Conflict) == 1, "distinct tokens for same user do not cause unique violation or double membership");
        var unused = sameUser[0].Failure is null ? distinctB : distinctA;
        Check((await Accept(unused, other.Id)).Failure is null, "conflict does not consume the unused invitation");
        await using (var db = context())
        {
            var store = new EfCampaignStore(db); var before = (await store.Read(campaign.Id, owner.Id, default))!;
            var changed = await new ManageCampaigns(store).Edit(campaign.Id, owner.Id, "Updated module", "new", "#123456");
            Check(changed.Value?.Name == "Updated module", "owner can edit campaign");
            Check((await store.Save(before, owner.Id, new("Stale overwrite", "", "#123456"), null, default)).Failure == CampaignFailure.Conflict, "stale application snapshot cannot overwrite campaign");
        }
        CampaignContext access; CampaignMemberDocument target;
        await using (var db = context())
        { var store = new EfCampaignStore(db); access = (await store.Read(campaign.Id, owner.Id, default))!; target = (await store.Member(campaign.Id, member.Id, default))!; }
        async Task<CampaignResult<bool>> Role(string role)
        { await using var db = context(); return await new EfCampaignStore(db).Change(access, target, owner.Id, role, default); }
        var roles = await Task.WhenAll(Role("gm"), Role("spectator"));
        Check(roles.Count(r => r.Failure is null) == 1 && roles.Count(r => r.Failure == CampaignFailure.Conflict) == 1, "concurrent role changes have one winner for the same snapshot");
        await using (var db = context())
        {
            var realtime = new Realtime(); var store = new EfCampaignStore(db); var current = (await store.Member(campaign.Id, member.Id, default))!;
            var count = await db.OutboxMessages.CountAsync();
            Check((await new ManageCampaignMembers(store, realtime).Role(campaign.Id, member.Id, owner.Id, current.Role)).Value == false && realtime.Calls == 0 && await db.OutboxMessages.CountAsync() == count,
                "no-op role creates no event and no revocation");
            Check((await Events(db)).Count(e => e.AggregateId == member.Id && e.CampaignId == campaign.Id && e.Type == GameEventTypes.CampaignMemberRoleChanged) == 1, "committed role change has a durable metadata event");
        }
        // Fail after SQL SaveChanges, before the explicit transaction commits.
        await using (var db = context())
        {
            var count = await db.OutboxMessages.CountAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(db.Database.GetConnectionString()).AddInterceptors(new FailAfterSave()).Options;
            var realtime = new Realtime();
            try
            {
                await using var fail = new AppDbContext(options);
                await new ManageCampaignMembers(new EfCampaignStore(fail), realtime).Remove(campaign.Id, other.Id, owner.Id);
                throw new Exception("Expected forced campaign failure.");
            }
            catch (InvalidOperationException error) when (error.Message == "Forced campaign failure") { }
            Check(await db.CampaignMemberships.AnyAsync(m => m.CampaignId == campaign.Id && m.UserId == other.Id) && await db.OutboxMessages.CountAsync() == count && realtime.Calls == 0,
                "failed member removal rolls back membership/event and never revokes live access");
            try
            {
                await using var fail = new AppDbContext(options);
                await new ManageCampaigns(new EfCampaignStore(fail)).Create(owner.Id, "Rolled back campaign", "", "#abcdef");
                throw new Exception("Expected forced campaign failure.");
            }
            catch (InvalidOperationException error) when (error.Message == "Forced campaign failure") { }
            Check(!await db.Campaigns.AnyAsync(x => x.Name == "Rolled back campaign") && await db.OutboxMessages.CountAsync() == count, "failed creation rolls back campaign/owner/outbox");
            var rollbackInvite = await Invite();
            var newcomer = User("rollback"); db.Users.Add(newcomer); await db.SaveChangesAsync();
            try
            {
                await using var fail = new AppDbContext(options);
                await new ManageCampaignInvites(new EfCampaignInviteStore(fail), tokens).Accept(rollbackInvite.Token, newcomer.Id);
                throw new Exception("Expected forced campaign failure.");
            }
            catch (InvalidOperationException error) when (error.Message == "Forced campaign failure") { }
            Check(!await db.CampaignMemberships.AnyAsync(m => m.CampaignId == campaign.Id && m.UserId == newcomer.Id) &&
                (await db.CampaignInvites.SingleAsync(i => i.TokenHash == CampaignInviteToken.Hash(rollbackInvite.Token))).UsedAt is null && await db.OutboxMessages.CountAsync() == count,
                "failed accept rolls back consumption, membership and event");
            Check((await Accept(rollbackInvite with { Token = rollbackInvite.Token.ToUpperInvariant() }, newcomer.Id)).Failure is null, "rolled-back capability remains usable including uppercase links");
        }
        await using (var db = context())
        {
            var expired = new CampaignInvite { CampaignId = campaign.Id, CreatedById = owner.Id, TokenHash = CampaignInviteToken.Hash(tokens.Generate()), ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
            db.CampaignInvites.Add(expired); await db.SaveChangesAsync();
            var invites = new EfCampaignInviteStore(db);
            Check(await invites.Preview(expired.TokenHash, DateTime.UtcNow, default) is null && (await invites.Accept(expired.TokenHash, other.Id, default)).Failure == CampaignFailure.NotFound, "expired invitations stay inaccessible");
            var character = new Character { Name = "Preserved after leave", CampaignId = campaign.Id, OwnerId = other.Id }; db.Characters.Add(character); await db.SaveChangesAsync();
            var writer = new EfCharacterWriteStore(db); var before = (await writer.Load(character.Id, other.Id, default))!;
            var realtime = new Realtime();
            Check((await new ManageCampaignMembers(new EfCampaignStore(db), realtime).Remove(campaign.Id, other.Id, other.Id)).Value && realtime.Calls == 1, "self-leave commits then revokes access");
            Check((await writer.Save(before, before.State with { Health = 1 }, other.Id, default)).Failure == CharacterWriteFailure.NotFound &&
                await db.Characters.AsNoTracking().AnyAsync(ch => ch.Id == character.Id && ch.Health == 10), "leaving preserves characters and blocks stale writes");
        }
        // A role read before revocation cannot authorize a later invite commit.
        await using (var db = context())
        {
            var store = new EfCampaignInviteStore(db); var before = (await store.Read(campaign.Id, owner.Id, default))!;
            var outstanding = await Invite("co-gm"); var outstandingHash = CampaignInviteToken.Hash(outstanding.Token);
            Check(await store.Preview(outstandingHash, DateTime.UtcNow, default) is not null, "unused invitation starts available");
            await using (var revoke = context())
            { (await revoke.CampaignMemberships.SingleAsync(m => m.CampaignId == campaign.Id && m.UserId == owner.Id)).Role = "player"; await revoke.SaveChangesAsync(); }
            Check((await store.Create(before, owner.Id, "gm", tokens.Generate(), DateTime.UtcNow.AddDays(1), default)).Failure == CampaignFailure.Forbidden, "invite creation rechecks owner authority under locks");
            Check(await store.Preview(outstandingHash, DateTime.UtcNow, default) is null &&
                (await store.Accept(outstandingHash, other.Id, default)).Failure == CampaignFailure.NotFound,
                "revoked issuer invalidates an otherwise valid unused capability");
            Check((await new EfCampaignStore(db).Save(before, owner.Id, new("Forbidden change", "", "#abcdef"), null, default)).Failure == CampaignFailure.Forbidden,
                "campaign edits recheck owner role under commit locks");
        }
        Console.WriteLine("Campaign PostgreSQL: scoped CRUD, atomic owner/events, single-use and distinct-token races, stale edits/roles, rollback, revocation, self-leave and preserved characters passed.");
    }
    private sealed class Realtime : ICampaignRealtimeAccess
    { public int Calls; public Task Invalidate(Guid campaign, Guid member) { Calls++; return Task.CompletedTask; } }
    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default) => throw new InvalidOperationException("Forced campaign failure");
    }
}
