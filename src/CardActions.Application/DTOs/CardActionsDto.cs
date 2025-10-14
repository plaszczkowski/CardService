namespace CardActions.Application.DTOs;

public record CardActionsResponse(
    string CardNumber,
    string CardType,
    string CardStatus,
    IReadOnlyList<string> AllowedActions,
    string TraceId,
    DateTime GeneratedAt);