using CardActions.API.DTOs;
using CardActions.API.Extensions;
using CardActions.API.Validators;
using CardActions.Application.DTOs;
using CardActions.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics;

namespace CardActions.API.Controllers;

/// <summary>
/// API controller for card action operations.
/// Implements REST endpoints for retrieving allowed card actions.
/// </summary>
[EnableRateLimiting("Fixed")]
[ApiController]
[Route("api/v1/[controller]")]
public class CardsController : ControllerBase
{
    private readonly CardActionsService _cardActionsService;
    private readonly ILogger<CardsController> _logger;
    private readonly IValidator<CardActionsRequest> _validator;

    public CardsController(
        CardActionsService cardActionsService,
        ILogger<CardsController> logger,
        IValidator<CardActionsRequest> validator)
    {
        _cardActionsService = cardActionsService;
        _logger = logger;
        _validator = validator;
    }

    /// <summary>
    /// Retrieves allowed actions for a specific card.
    /// </summary>
    /// <param name="request">Card identification request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of allowed actions for the card</returns>
    [HttpGet("actions")]
    [ProducesResponseType(typeof(CardActionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCardActions(
        [FromQuery] CardActionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var traceId = HttpContext.TraceIdentifier;

        using (_logger.BeginScope("TraceId: {TraceId}", traceId))
        {
            try
            {
                var validationResult = await _validator.ValidateAsync(request, cancellationToken);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Validation failed for User: {UserId}, Card: {CardNumber}",
                        request.UserId, request.CardNumber);

                    var errors = validationResult.ToDictionary();

                    // Extension method for consistent traceId injection
                    return this.ValidationProblemWithTraceId(errors);
                }

                var response = await _cardActionsService.GetCardActionsAsync(
                    request.UserId,
                    request.CardNumber,
                    traceId,
                    cancellationToken);

                return Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning("Card not found: {Error}", ex.Message);

                return this.ProblemWithCorrelationId(
                    title: "Card Not Found",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status404NotFound,
                    correlationId: traceId);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Access denied: {Error}", ex.Message);
                return this.ProblemWithCorrelationId(
                    title: "Access Denied",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status403Forbidden,
                    correlationId: traceId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Request was cancelled for User: {UserId}, Card: {CardNumber}",
                    request.UserId, request.CardNumber);
                return StatusCode(499); // Client Closed Request
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception for User: {UserId}, Card: {CardNumber}",
                    request.UserId, request.CardNumber);
                return this.ProblemWithCorrelationId(
                    title: "An unexpected error occurred",
                    detail: "Please try again later",
                    statusCode: StatusCodes.Status500InternalServerError,
                    correlationId: traceId);
            }
        }
    }
}