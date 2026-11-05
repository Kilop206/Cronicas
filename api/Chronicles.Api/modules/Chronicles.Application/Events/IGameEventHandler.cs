using Chronicles.Domain.Events;

namespace Chronicles.Application.Events;

// Delivery is at least once. Implementations must tolerate repeated EventId values.
public interface IGameEventHandler
{
    Task Handle(GameEvent message, CancellationToken cancellationToken);
}
