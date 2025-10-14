namespace CardActions.Domain.Events;

/// <summary>
/// Base class for all domain events.
/// </summary>
public abstract record DomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => GetType().Name;
    public int SchemaVersion { get; init; } = 1;
}