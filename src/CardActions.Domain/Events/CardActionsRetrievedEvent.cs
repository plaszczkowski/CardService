namespace CardActions.Domain.Events;

/// <summary>
/// Event raised when card actions are successfully retrieved.
/// </summary>
public record CardActionsRetrievedEvent : DomainEvent
{
    public CardActionsRetrievedEvent()
    {
        SchemaVersion = 2; // Explicit versioning
    }

    public required string UserId { get; init; }
    public required string CardNumber { get; init; }
    public required string CardType { get; init; }
    public required string CardStatus { get; init; }
    public required IReadOnlyList<string> AllowedActions { get; init; }
    public required string TraceId { get; init; }
}