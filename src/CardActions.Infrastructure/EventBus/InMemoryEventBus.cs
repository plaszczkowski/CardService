using CardActions.Domain.Events;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using CardActions.Domain.Abstractions;

namespace CardActions.Infrastructure.EventBus;

/// <summary>
/// In-memory implementation of event bus for development/testing.
/// </summary>
public class InMemoryEventBus : IEventBus
{
    private readonly ILogger<InMemoryEventBus> _logger;
    private readonly ConcurrentBag<DomainEvent> _publishedEvents;
    //TODO private readonly ICardActionsMetrics? _metrics; (optional)
    // Current: Events published but no metrics/tracing
    // Gap: No visibility into event throughput, failures

    public InMemoryEventBus(ILogger<InMemoryEventBus> logger)
    {
        _logger = logger;
        _publishedEvents = new ConcurrentBag<DomainEvent>();
    }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : DomainEvent
    {
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));

        _publishedEvents.Add(@event);

        _logger.LogInformation(
            "Event published: {EventType}, EventId: {EventId}, OccurredAt: {OccurredAt}",
            @event.EventType, @event.EventId, @event.OccurredAt);

        return Task.CompletedTask;
    }

    public Task PublishBatchAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellationToken = default)
    where TEvent : DomainEvent
    {
        if (events == null)
            throw new ArgumentNullException(nameof(events));

        var eventList = events.ToList();

        if (eventList.Count == 0)
            throw new ArgumentException("Event batch cannot be empty", nameof(events));

        foreach (var @event in eventList)
        {
            _publishedEvents.Add(@event);
        }

        _logger.LogInformation(
            "Batch published: {EventCount} events of type {EventType}",
            eventList.Count, typeof(TEvent).Name);

        return Task.CompletedTask;
    }


    // Replay capability for testing
    public async Task ReplayEventsAsync(Func<DomainEvent, Task> handler, CancellationToken cancellationToken = default)
    {
        foreach (var @event in _publishedEvents)
        {
            await handler(@event);
        }
    }

    // Filter events by type for testing
    public IReadOnlyCollection<TEvent> GetEventsByType<TEvent>() where TEvent : DomainEvent
    {
        return _publishedEvents.OfType<TEvent>().ToArray();
    }

    /// <summary>
    /// Gets all published events (for testing/debugging).
    /// </summary>
    public IReadOnlyCollection<DomainEvent> GetPublishedEvents() => _publishedEvents.ToArray();

    /// <summary>
    /// Clears all published events (for testing).
    /// </summary>
    public void Clear() => _publishedEvents.Clear();
}