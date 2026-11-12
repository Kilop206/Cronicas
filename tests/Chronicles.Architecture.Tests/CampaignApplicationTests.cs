using Chronicles.Application.Campaigns;
using Chronicles.Domain.Campaigns;

public static class CampaignApplicationTests
{
    public static async Task Run()
    {
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Campaign application: " + label); }
        var owner = Guid.NewGuid(); var player = Guid.NewGuid(); var other = Guid.NewGuid();
        var document = new CampaignDocument(Guid.NewGuid(), "Campaign", "", "planning", "#b39a69", "owner", DateTime.UtcNow, null, null);
        var store = new MemoryStore(new(document, owner)); store.Roles[owner] = "owner"; store.Roles[player] = "player";
        var realtime = new Realtime(); var campaigns = new ManageCampaigns(store); var members = new ManageCampaignMembers(store, realtime);
        var invites = new ManageCampaignInvites(store, new Tokens());
        Check(CampaignDetails.Parse(" ab ", "", "#abcdef") is null && CampaignDetails.Parse("Valid", "", "#abcdef\n") is null, "bounded names and exact color length");
        Check((await campaigns.Create(owner, " x ", "", "#abcdef")).Failure == CampaignFailure.Invalid && store.Creates == 0, "invalid create cannot persist");
        Check((await campaigns.Create(owner, "  Normalized  ", " description ", "#aBc123")).Value is { Name: "Normalized", Description: "description" }, "create normalizes values");
        Check((await campaigns.Edit(document.Id, player, "Changed", "", "#abcdef")).Failure == CampaignFailure.Forbidden, "player cannot edit campaign");
        Check((await campaigns.Edit(document.Id, other, "Changed", "", "#abcdef")).Failure == CampaignFailure.NotFound, "unknown campaign hidden");
        Check((await campaigns.Status(document.Id, owner, "invented")).Failure == CampaignFailure.Invalid && store.Saves == 0, "status allowlist");
        Check((await campaigns.Status(document.Id, owner, "active")).Failure is null && store.Saves == 1, "owner status delegates to persistence");
        var query = new ReadCampaigns(store); await query.List(owner, -9, " Campaign ");
        Check(store.LastPage == 1 && store.LastSearch == "Campaign", "pagination/search normalized before query port");
        Check((await invites.Create(document.Id, player, "player")).Failure == CampaignFailure.Forbidden && store.Invites == 0, "non-owner cannot create invitations");
        Check((await invites.Create(document.Id, owner, "owner")).Failure == CampaignFailure.Invalid && store.Invites == 0, "invitation cannot grant ownership");
        var invite = (await invites.Create(document.Id, owner, "co-gm")).Value!;
        Check(invite.Token.Length == 48 && invite.ExpiresAt > DateTime.UtcNow.AddDays(6) && store.Invites == 1, "legacy token/expiry and authorized creation preserved");
        Check(await invites.Preview("malformed") is null && store.Previews == 0, "invalid capability is rejected before persistence");
        await invites.Preview(invite.Token.ToUpperInvariant());
        Check(store.LastHash == CampaignInviteToken.Hash(invite.Token) && !store.LastHash.Contains(invite.Token, StringComparison.Ordinal), "preview uses canonical hash, not raw capability");
        Check((await invites.Accept("bad", player)).Failure == CampaignFailure.NotFound && store.Accepts == 0, "invalid token never reaches atomic accept");
        Check((await invites.Accept(invite.Token, player)).Value?.CampaignId == document.Id && store.Accepts == 1, "accept delegated to atomic port");
        Check(!CampaignPolicy.AvailableInvite("owner", DateTime.UtcNow.AddDays(1), null, DateTime.UtcNow) &&
            !CampaignPolicy.AvailableInvite("player", DateTime.UtcNow, null, DateTime.UtcNow) &&
            !CampaignPolicy.AvailableInvite("player", DateTime.UtcNow.AddDays(1), DateTime.UtcNow, DateTime.UtcNow), "role/expiry/replay invariants");
        Check((await members.Role(document.Id, owner, owner, "player")).Failure == CampaignFailure.Invalid, "owner cannot demote themselves");
        Check((await members.Role(document.Id, player, player, "gm")).Failure == CampaignFailure.NotFound, "player cannot promote themselves");
        Check((await members.Remove(document.Id, owner, player)).Failure == CampaignFailure.Forbidden, "owner cannot be removed");
        Check((await members.Remove(document.Id, other, player)).Failure == CampaignFailure.Forbidden, "player cannot remove somebody else");
        Check((await members.Role(document.Id, player, owner, "player")).Value == false && realtime.Calls == 0, "no-op role does not revoke connections");
        Check((await members.Role(document.Id, player, owner, "spectator")).Value && realtime.Calls == 1 && realtime.Member == player, "committed role change invalidates only target access");
        store.RejectChange = true;
        Check((await members.Role(document.Id, player, owner, "gm")).Failure == CampaignFailure.Conflict && realtime.Calls == 1, "failed commit never invalidates access");
        store.RejectChange = false;
        Check((await members.Remove(document.Id, player, player)).Value && realtime.Calls == 2, "member may leave with post-commit invalidation");
        Check(await members.List(document.Id, player) is null, "removed member loses list access");
        Console.WriteLine("Campaign application: normalized details, status/role policies, scoped reads, hashed invite capability, expiry/replay, owner protection and post-commit access invalidation passed.");
    }
    private sealed class Tokens : ICampaignInviteTokenGenerator { public string Generate() => "abcdef0123456789abcdef0123456789abcdef0123456789"; }
    private sealed class Realtime : ICampaignRealtimeAccess
    {
        public int Calls; public Guid Member;
        public Task Invalidate(Guid campaign, Guid member) { Calls++; Member = member; return Task.CompletedTask; }
    }
    private sealed class MemoryStore(CampaignContext context) : ICampaignStore, ICampaignMembershipStore, ICampaignInviteStore
    {
        public Dictionary<Guid, string> Roles = []; public int Creates, Saves, Invites, Accepts, Previews, LastPage;
        public string? LastSearch, LastHash; public bool RejectChange;
        public Task<CampaignContext?> Read(Guid campaign, Guid actor, CancellationToken ct) => Task.FromResult(
            Roles.TryGetValue(actor, out var role) ? context with { Document = context.Document with { Role = role } } : null);
        public Task<CampaignPage> List(Guid actor, int page, string? search, CancellationToken ct)
        { LastPage = page; LastSearch = search; return Task.FromResult(new CampaignPage([], 0, page)); }
        public Task<CampaignDocument> Create(Guid actor, CampaignDetails details, CancellationToken ct)
        { Creates++; return Task.FromResult(context.Document with { Name = details.Name, Description = details.Description }); }
        public Task<CampaignResult<CampaignDocument>> Save(CampaignContext before, Guid actor, CampaignDetails? details, string? status, CancellationToken ct)
        { Saves++; return Task.FromResult(new CampaignResult<CampaignDocument>(before.Document)); }
        public Task<CampaignMemberDocument[]?> Members(Guid campaign, Guid actor, CancellationToken ct) =>
            Task.FromResult<CampaignMemberDocument[]?>(Roles.ContainsKey(actor) ? [] : null);
        public Task<CampaignMemberDocument?> Member(Guid campaign, Guid target, CancellationToken ct) => Task.FromResult(
            Roles.TryGetValue(target, out var role) ? new CampaignMemberDocument(target, "Member", role, DateTime.UtcNow) : null);
        public Task<CampaignResult<bool>> Change(CampaignContext before, CampaignMemberDocument target, Guid actor, string? role, CancellationToken ct)
        {
            if (RejectChange) return Task.FromResult(new CampaignResult<bool>(Failure: CampaignFailure.Conflict));
            var changed = Roles[target.UserId] != role;
            if (role is null) Roles.Remove(target.UserId); else Roles[target.UserId] = role;
            return Task.FromResult(new CampaignResult<bool>(changed));
        }
        public Task<CampaignResult<CampaignInviteDocument>> Create(CampaignContext before, Guid actor, string role, string token, DateTime expiresAt, CancellationToken ct)
        { Invites++; return Task.FromResult(new CampaignResult<CampaignInviteDocument>(new(token, role, expiresAt))); }
        public Task<CampaignInvitePreview?> Preview(string hash, DateTime now, CancellationToken ct)
        { Previews++; LastHash = hash; return Task.FromResult<CampaignInvitePreview?>(null); }
        public Task<CampaignResult<CampaignJoined>> Accept(string hash, Guid actor, CancellationToken ct)
        { Accepts++; LastHash = hash; return Task.FromResult(new CampaignResult<CampaignJoined>(new(context.Document.Id))); }
    }
}
