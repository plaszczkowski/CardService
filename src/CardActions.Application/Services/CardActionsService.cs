using CardActions.Application.DTOs;
using CardActions.Application.Interfaces;
using CardActions.Domain.Events;
using CardActions.Domain.Models;
using CardActions.Domain.Services;
using Microsoft.Extensions.Logging;
using CardActions.Domain.Abstractions;

namespace CardActions.Application.Services;

/// <summary>
/// Orchestrates card action retrieval with event publishing.
/// </summary>
public class CardActionsService
{
    private readonly ICardRepository _cardRepository;
    private readonly ICardActionPolicy _actionPolicy;
    private readonly ILogger<CardActionsService> _logger;
    private readonly ICardActionsMetrics _metrics;
    private readonly IEventBus _eventBus;

    public CardActionsService(
        ICardRepository cardRepository,
        ICardActionPolicy actionPolicy,
        ILogger<CardActionsService> logger,
        ICardActionsMetrics metrics,
        IEventBus eventBus)
    {
        _cardRepository = cardRepository;
        _actionPolicy = actionPolicy;
        _logger = logger;
        _metrics = metrics;
        _eventBus = eventBus;
    }

    public async Task<CardActionsResponse> GetCardActionsAsync(
        string userId,
        string cardNumber,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var success = false;
        string cardType = "unknown";
        string cardStatus = "unknown";

        try
        {
            _logger.LogInformation(
                "Retrieving card actions for User: {UserId}, Card: {CardNumber}, TraceId: {TraceId}",
                userId, cardNumber, traceId);

            // Validate input
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(cardNumber))
            {
                throw new ArgumentException("UserId and CardNumber are required");
            }

            var user = new UserId(userId);

            // Check if card exists
            var cardExists = await _cardRepository.CardExistsAsync(cardNumber, cancellationToken);
            if (!cardExists)
            {
                _logger.LogWarning("Card not found: {CardNumber}, TraceId: {TraceId}", cardNumber, traceId);

                // Publish event for non-existent card
                await PublishEventSafely(new CardNotFoundEvent {
                    UserId = userId, CardNumber = cardNumber, TraceId = traceId }, cancellationToken);

                throw new KeyNotFoundException($"Card {cardNumber} not found");
            }

            // Check authorization
            var card = await _cardRepository.GetCardAsync(user, cardNumber, cancellationToken);
            if (card is null)
            {
                _logger.LogWarning(
                    "User {UserId} attempted to access card {CardNumber} without permission, TraceId: {TraceId}",
                    userId, cardNumber, traceId);

                // Publish event for authorization failure
                await PublishEventSafely(new CardAccessDeniedEvent {
                    UserId = userId, CardNumber = cardNumber, Reason = "Card does not belong to user", TraceId = traceId }, cancellationToken);

                throw new UnauthorizedAccessException("Access to card denied");
            }

            cardType = card.Type.ToString();
            cardStatus = card.Status.ToString();

            var allowedActions = _actionPolicy.GetAllowedActions(card);

            _logger.LogInformation(
                "Retrieved {ActionCount} allowed actions for Card: {CardNumber}, TraceId: {TraceId}",
                allowedActions.Count, cardNumber, traceId);

            var response = new CardActionsResponse(
                CardNumber: card.CardNumber,
                CardType: cardType,
                CardStatus: cardStatus,
                AllowedActions: allowedActions,
                TraceId: traceId,
                GeneratedAt: DateTime.UtcNow);

            // Mark success BEFORE event publishing since Event publishing failure shouldn't affect operation success
            // and Metrics should reflect operation outcome, not event bus health
            success = true;

            await PublishEventSafely(new CardActionsRetrievedEvent(){ 
                UserId = userId, CardNumber = cardNumber, CardType = cardType, CardStatus = cardStatus,
                AllowedActions = allowedActions, TraceId = traceId }, cancellationToken);

            return response;
        }
        finally
        {
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _metrics.RecordRequest(cardType, cardStatus, success, duration);
        }
    }

    private async Task PublishEventSafely<TEvent>(TEvent @event, CancellationToken cancellationToken) where TEvent : DomainEvent
    {
        // Event publishing with try-catch (don't fail operation if event fails)
        try
        {
            await _eventBus.PublishAsync(@event, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType}", @event.EventType);
            // Don't rethrow - event publishing is fire-and-forget
        }
    }
}