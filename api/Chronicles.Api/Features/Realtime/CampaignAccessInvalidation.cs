using Chronicles.Api.Services;
using Chronicles.Application.Campaigns;
using Chronicles.Application.Events;
using Chronicles.Domain.Events;

namespace Chronicles.Api;

public sealed class CampaignRealtimeAccess(ICampaignMembershipStore members, RoomAccessService rooms, ILogger<CampaignRealtimeAccess> logger) : ICampaignRealtimeAccess
{
    public async Task Invalidate(Guid campaign, Guid member)
    {
        try
        {
            var current = await members.Member(campaign, member, CancellationToken.None);
            await rooms.ReconcileCampaignMember(campaign, member, current?.Role, current?.JoinedAt);
        }
        catch (Exception error)
        {
            // The membership is already committed. Its durable outbox message retries failed transport delivery.
            logger.LogWarning(error, "Immediate campaign access invalidation failed; durable delivery will retry for campaign {CampaignId}", campaign);
        }
    }
}

public sealed class CampaignAccessInvalidationHandler(ICampaignMembershipStore members, RoomAccessService rooms) : IGameEventHandler
{
    public async Task Handle(GameEvent message, CancellationToken ct)
    {
        if (message.Type is not (GameEventTypes.CampaignMemberRoleChanged or GameEventTypes.CampaignMemberLeft) || message.CampaignId is not { } campaign) return;
        var current = await members.Member(campaign, message.AggregateId, ct);
        await rooms.ReconcileCampaignMember(campaign, message.AggregateId, current?.Role, current?.JoinedAt);
    }
}
