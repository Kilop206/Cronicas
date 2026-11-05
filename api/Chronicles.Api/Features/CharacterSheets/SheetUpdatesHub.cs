using System.Collections.Concurrent;
using System.Security.Claims;
using Chronicles.Api.Data;
using Chronicles.Application.Events;
using Chronicles.Domain.Events;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

public sealed record SheetWatcher(string ConnectionId, Guid UserId, Guid SessionId, Guid CharacterId);
public sealed class SheetWatchers
{
    private readonly ConcurrentDictionary<string, SheetWatcher> watchers = new(StringComparer.Ordinal);
    public void Watch(SheetWatcher watcher) => watchers[watcher.ConnectionId] = watcher; // One active sheet per connection.
    public void Remove(string connectionId) => watchers.TryRemove(connectionId, out _);
    public SheetWatcher[] For(Guid characterId) => watchers.Values.Where(w => w.CharacterId == characterId).ToArray();
}

public sealed class SheetUpdatesHub(AppDbContext db, SheetWatchers watchers) : Hub
{
    public async Task Subscribe(Guid characterId)
    {
        if (!Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var actor) ||
            Context.GetHttpContext()?.Items["sessionId"] is not Guid session ||
            !await db.UserSessions.AnyAsync(s => s.Id == session && s.UserId == actor && s.RevokedAt == null && s.ExpiresAt > DateTime.UtcNow) ||
            !await CharacterResourceAccess.CanRead(db, characterId, actor))
            throw new HubException("Acesso à ficha negado.");
        watchers.Watch(new(Context.ConnectionId, actor, session, characterId));
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        watchers.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

public sealed class SheetInvalidationHandler(AppDbContext db, SheetWatchers watchers, IHubContext<SheetUpdatesHub> hub) : IGameEventHandler
{
    public async Task Handle(GameEvent message, CancellationToken ct)
    {
        if (message.Type is not (GameEventTypes.CharacterSheetUpdated or GameEventTypes.CharacterDeleted)) return;
        foreach (var watcher in watchers.For(message.AggregateId))
        {
            // Revalidate every delivery, including sessions revoked after the WebSocket handshake.
            var allowed = await db.UserSessions.AnyAsync(s => s.Id == watcher.SessionId && s.UserId == watcher.UserId &&
                s.RevokedAt == null && s.ExpiresAt > DateTime.UtcNow, ct) &&
                await db.CampaignMemberships.AnyAsync(m => m.CampaignId == message.CampaignId && m.UserId == watcher.UserId, ct);
            if (allowed) await hub.Clients.Client(watcher.ConnectionId).SendAsync("SheetInvalidated", new
                { message.EventId, message.Type, characterId = message.AggregateId, message.Revision }, ct);
            else watchers.Remove(watcher.ConnectionId);
        }
    }
}
