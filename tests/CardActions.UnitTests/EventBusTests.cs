using CardActions.Domain.Events;
using CardActions.Infrastructure.EventBus;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CardActions.UnitTests;

[Trait("Category", "Unit")]
[Trait("Feature", "InMemoryEventBus")]
public sealed class EventBusTests
{
    private readonly Mock<ILogger<InMemoryEventBus>> _loggerMock = new();

    [Fact]
    public async Task PublishAsync_WithValidEvent_ShouldStoreEvent()
    {
        var eventBus = new InMemoryEventBus(_loggerMock.Object);
        var @event = new CardActionsRetrievedEvent
        {
            UserId = "User1",
            CardNumber = "CARD123",
            CardType = "Debit",
            CardStatus = "Active",
            AllowedActions = ["ACTION1", "ACTION3"],
            TraceId = "trace-123"
        };

        await eventBus.PublishAsync(@event);

        var published = eventBus.GetPublishedEvents().Single() as CardActionsRetrievedEvent;
        published.Should().NotBeNull();
        published!.UserId.Should().Be("User1");
        published.CardNumber.Should().Be("CARD123");
        published.AllowedActions.Should().Contain("ACTION1").And.Contain("ACTION3");
        published.TraceId.Should().Be("trace-123");
    }

    [Fact]
    public async Task PublishBatchAsync_WithMultipleEvents_ShouldStoreAll()
    {
        var eventBus = new InMemoryEventBus(_loggerMock.Object);
        var events = new[]
        {
            new CardAccessDeniedEvent { UserId = "User1", CardNumber = "CARD1", Reason = "Not owner", TraceId = "trace-1" },
            new CardAccessDeniedEvent { UserId = "User2", CardNumber = "CARD2", Reason = "Not owner", TraceId = "trace-2" }
        };

        await eventBus.PublishBatchAsync(events);

        var published = eventBus.GetPublishedEvents().Cast<CardAccessDeniedEvent>().ToList();
        published.Should().ContainSingle(e => e.UserId == "User1" && e.CardNumber == "CARD1" && e.TraceId == "trace-1");
        published.Should().ContainSingle(e => e.UserId == "User2" && e.CardNumber == "CARD2" && e.TraceId == "trace-2");
    }

    [Fact]
    public async Task PublishAsync_WithNullEvent_ShouldThrowArgumentNullException()
    {
        var eventBus = new InMemoryEventBus(_loggerMock.Object);
        await Assert.ThrowsAsync<ArgumentNullException>(() => eventBus.PublishAsync<CardActionsRetrievedEvent>(null!));
    }

    [Fact]
    public async Task PublishBatchAsync_WithNullCollection_ShouldThrowArgumentNullException()
    {
        var eventBus = new InMemoryEventBus(_loggerMock.Object);
        await Assert.ThrowsAsync<ArgumentNullException>(() => eventBus.PublishBatchAsync<CardAccessDeniedEvent>(null!));
    }

    [Fact]
    public async Task PublishBatchAsync_WithEmptyCollection_ShouldThrowArgumentException()
    {
        var eventBus = new InMemoryEventBus(_loggerMock.Object);
        await Assert.ThrowsAsync<ArgumentException>(() => eventBus.PublishBatchAsync<CardAccessDeniedEvent>([]));
    }
}
