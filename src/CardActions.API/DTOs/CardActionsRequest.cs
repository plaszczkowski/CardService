namespace CardActions.API.DTOs;

public record CardActionsRequest(
    string UserId,
    string CardNumber);