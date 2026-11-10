using Chronicles.Domain.Campaigns;

namespace Chronicles.Application.Campaigns;

public enum CampaignFailure { NotFound, Forbidden, Invalid, Conflict }
public sealed record CampaignResult<T>(T? Value = default, CampaignFailure? Failure = null, string? Message = null, Dictionary<string, string[]>? Errors = null);
public sealed record CampaignDocument(Guid Id, string Name, string Description, string Status, string AccentColor, string Role,
    DateTime UpdatedAt, Guid? SystemId, Guid? WorldId);
public sealed record CampaignPage(CampaignDocument[] Items, int Total, int Page);
public sealed record CampaignContext(CampaignDocument Document, Guid OwnerId);
public sealed record CampaignMemberDocument(Guid UserId, string DisplayName, string Role, DateTime JoinedAt);
public sealed record CampaignInviteDocument(string Token, string Role, DateTime ExpiresAt);
public sealed record CampaignInvitePreview(Guid CampaignId, string CampaignName, string Role, DateTime ExpiresAt);
public sealed record CampaignJoined(Guid CampaignId);

public interface ICampaignReader
{
    Task<CampaignContext?> Read(Guid campaign, Guid actor, CancellationToken ct);
}
public interface ICampaignStore : ICampaignReader
{
    Task<CampaignPage> List(Guid actor, int page, string? search, CancellationToken ct);
    Task<CampaignDocument> Create(Guid actor, CampaignDetails details, CancellationToken ct);
    Task<CampaignResult<CampaignDocument>> Save(CampaignContext before, Guid actor, CampaignDetails? details, string? status, CancellationToken ct);
}
public interface ICampaignMembershipStore : ICampaignReader
{
    Task<CampaignMemberDocument[]?> Members(Guid campaign, Guid actor, CancellationToken ct);
    Task<CampaignMemberDocument?> Member(Guid campaign, Guid target, CancellationToken ct);
    Task<CampaignResult<bool>> Change(CampaignContext before, CampaignMemberDocument target, Guid actor, string? role, CancellationToken ct);
}
public interface ICampaignInviteStore : ICampaignReader
{
    Task<CampaignResult<CampaignInviteDocument>> Create(CampaignContext before, Guid actor, string role, string token, DateTime expiresAt, CancellationToken ct);
    Task<CampaignInvitePreview?> Preview(string hash, DateTime now, CancellationToken ct);
    Task<CampaignResult<CampaignJoined>> Accept(string hash, Guid actor, CancellationToken ct);
}
public interface ICampaignInviteTokenGenerator { string Generate(); }
public interface ICampaignRealtimeAccess { Task Invalidate(Guid campaign, Guid member); }

public sealed class ReadCampaigns(ICampaignStore store)
{
    public Task<CampaignPage> List(Guid actor, int? page, string? search, CancellationToken ct = default) => store.List(actor, Math.Clamp(page ?? 1, 1, 100000), search?.Trim(), ct);
    public async Task<CampaignDocument?> Read(Guid campaign, Guid actor, CancellationToken ct = default) => (await store.Read(campaign, actor, ct))?.Document;
}

public sealed class ManageCampaigns(ICampaignStore store)
{
    public async Task<CampaignResult<CampaignDocument>> Create(Guid actor, string? name, string? description, string? color, CancellationToken ct = default)
    {
        var details = CampaignDetails.Parse(name, description, color);
        return details is null ? InvalidDetails() : new(await store.Create(actor, details, ct));
    }
    public async Task<CampaignResult<CampaignDocument>> Edit(Guid campaign, Guid actor, string? name, string? description, string? color, CancellationToken ct = default)
    {
        var before = await store.Read(campaign, actor, ct);
        if (Access(before, actor) is { } failure) return failure;
        var details = CampaignDetails.Parse(name, description, color);
        return details is null ? InvalidDetails() : await store.Save(before!, actor, details, null, ct);
    }
    public async Task<CampaignResult<CampaignDocument>> Status(Guid campaign, Guid actor, string? status, CancellationToken ct = default)
    {
        var before = await store.Read(campaign, actor, ct);
        if (Access(before, actor) is { } failure) return failure;
        return !CampaignPolicy.ValidStatus(status) ? new(Failure: CampaignFailure.Invalid, Errors: new() { ["status"] = ["Status inválido."] }) :
            await store.Save(before!, actor, null, status, ct);
    }
    private static CampaignResult<CampaignDocument>? Access(CampaignContext? context, Guid actor) => context is null ? new(Failure: CampaignFailure.NotFound) :
        !CampaignPolicy.IsOwner(context.OwnerId, actor, context.Document.Role) ? new(Failure: CampaignFailure.Forbidden, Message: "Apenas o proprietário pode alterar a campanha.") : null;
    private static CampaignResult<CampaignDocument> InvalidDetails() => new(Failure: CampaignFailure.Invalid, Errors: new() { ["campaign"] = ["Verifique nome, descrição e cor."] });
}

public sealed class ManageCampaignMembers(ICampaignMembershipStore store, ICampaignRealtimeAccess realtime)
{
    public Task<CampaignMemberDocument[]?> List(Guid campaign, Guid actor, CancellationToken ct = default) => store.Members(campaign, actor, ct);
    public Task<CampaignResult<bool>> Remove(Guid campaign, Guid target, Guid actor, CancellationToken ct = default) => Change(campaign, target, actor, null, ct);
    public Task<CampaignResult<bool>> Role(Guid campaign, Guid target, Guid actor, string? role, CancellationToken ct = default) =>
        Change(campaign, target, actor, role, ct, edit: true);
    private async Task<CampaignResult<bool>> Change(Guid campaign, Guid targetId, Guid actor, string? role, CancellationToken ct, bool edit = false)
    {
        var before = await store.Read(campaign, actor, ct);
        if (before is null || edit && !CampaignPolicy.IsOwner(before.OwnerId, actor, before.Document.Role)) return new(Failure: CampaignFailure.NotFound);
        if (edit && (targetId == actor || targetId == before.OwnerId || !CampaignPolicy.InvitableRole(role)))
            return new(Failure: CampaignFailure.Invalid, Errors: new() { ["role"] = ["Papel inválido."] });
        if (!edit && !CampaignPolicy.MayRemove(before.OwnerId, actor, targetId))
            return new(Failure: CampaignFailure.Forbidden, Message: "Sem permissão para remover este participante.");
        var target = await store.Member(campaign, targetId, ct);
        if (target is null) return new(Failure: CampaignFailure.NotFound);
        var result = await store.Change(before, target, actor, role, ct);
        if (result.Failure is null && result.Value) await realtime.Invalidate(campaign, targetId);
        return result;
    }
}

public sealed class ManageCampaignInvites(ICampaignInviteStore store, ICampaignInviteTokenGenerator tokens)
{
    public async Task<CampaignResult<CampaignInviteDocument>> Create(Guid campaign, Guid actor, string? role, CancellationToken ct = default)
    {
        var before = await store.Read(campaign, actor, ct);
        if (before is null) return new(Failure: CampaignFailure.NotFound);
        if (!CampaignPolicy.IsOwner(before.OwnerId, actor, before.Document.Role))
            return new(Failure: CampaignFailure.Forbidden, Message: "Apenas o proprietário pode convidar participantes.");
        if (!CampaignPolicy.InvitableRole(role)) return new(Failure: CampaignFailure.Invalid, Errors: new() { ["role"] = ["Papel de convite inválido."] });
        var token = tokens.Generate();
        if (!CampaignInviteToken.Valid(token)) throw new InvalidOperationException("Invalid campaign invite token generator.");
        return await store.Create(before, actor, role!, token, DateTime.UtcNow.AddDays(7), ct);
    }
    public Task<CampaignInvitePreview?> Preview(string? token, CancellationToken ct = default) => CampaignInviteToken.Valid(token) ?
        store.Preview(CampaignInviteToken.Hash(token!), DateTime.UtcNow, ct) : Task.FromResult<CampaignInvitePreview?>(null);
    public Task<CampaignResult<CampaignJoined>> Accept(string? token, Guid actor, CancellationToken ct = default) => CampaignInviteToken.Valid(token) ?
        store.Accept(CampaignInviteToken.Hash(token!), actor, ct) : Task.FromResult(new CampaignResult<CampaignJoined>(Failure: CampaignFailure.NotFound));
}
