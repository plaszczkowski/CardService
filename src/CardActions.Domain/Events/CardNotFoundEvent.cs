namespace CardActions.Domain.Events;

/// <summary>
/// Event raised when requested card does not exist.
/// </summary>
public record CardNotFoundEvent : DomainEvent
{
    public required string UserId { get; init; }
    public required string CardNumber { get; init; }
    public required string TraceId { get; init; }
}