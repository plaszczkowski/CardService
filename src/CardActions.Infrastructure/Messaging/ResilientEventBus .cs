using CardActions.Domain.Abstractions;
using CardActions.Domain.Events;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client.Exceptions;

namespace CardActions.Infrastructure.EventBus;

/// <summary>
/// Decorator that adds retry policy around event bus operations.
/// Wraps any IEventBus implementation with resilience patterns.
/// </summary>
public class ResilientEventBus : IEventBus
{
    private readonly IEventBus _innerEventBus;
    private readonly ILogger _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public ResilientEventBus(IEventBus innerEventBus, ILogger logger)
    {
        _innerEventBus = innerEventBus;
        _logger = logger;

        _retryPolicy = Policy
            .Handle<BrokerUnreachableException>()
            .Or<OperationInterruptedException>()
            .Or<AlreadyClosedException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Event publishing retry {RetryCount}/3 after {Delay}s due to {ExceptionType}",
                        retryCount, timeSpan.TotalSeconds, exception.GetType().Name);
                });
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : DomainEvent
    {
        await _retryPolicy.ExecuteAsync(async () =>
            await _innerEventBus.PublishAsync(@event, cancellationToken));
    }

    public async Task PublishBatchAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellationToken = default)
        where TEvent : DomainEvent
    {
        await _retryPolicy.ExecuteAsync(async () =>
            await _innerEventBus.PublishBatchAsync(events, cancellationToken));
    }
}