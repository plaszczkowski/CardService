using CardActions.Domain.Events;

namespace CardActions.Domain.Abstractions;

/// <summary>
/// Abstraction for publishing domain events to message bus.
/// </summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : DomainEvent;

    Task PublishBatchAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellationToken = default)
        where TEvent : DomainEvent;
}