namespace CardActions.Domain.Events;

/// <summary>
/// Event raised when card access is denied (authorization failure).
/// </summary>
public record CardAccessDeniedEvent : DomainEvent
{
    public required string UserId { get; init; }
    public required string CardNumber { get; init; }
    public required string Reason { get; init; }
    public required string TraceId { get; init; }
    public string AttemptedFrom { get; init; } = "API";
}