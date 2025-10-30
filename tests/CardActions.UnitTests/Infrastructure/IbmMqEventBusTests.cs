using CardActions.Domain.Events;
using CardActions.Infrastructure.Configuration;
using CardActions.Infrastructure.EventBus;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CardActions.UnitTests.Infrastructure;

[Trait("Category", "Unit")]
[Trait("Feature", "IbmMqEventBus")]
public sealed class IbmMqEventBusTests
{
    private readonly Mock<ILogger<IbmMqEventBus>> _loggerMock = new();
    private readonly EventBusOptions _options = new()
    {
        Provider = "IbmMQ",
        IbmMQ = new IbmMqOptions
        {
            Host = "localhost",
            Port = 1414,
            QueueManager = "QM1",
            Channel = "DEV.APP.SVRCONN",
            QueueName = "DEV.QUEUE.1"
        }
    };

    public static object[][] InvalidConfigs =>
        new[]
        {
            new object[] { null, "1414", "QM1", "CHANNEL", "QUEUE", typeof(ArgumentException) },
            new object[] { "", "1414", "QM1", "CHANNEL", "QUEUE", typeof(ArgumentException) },
            new object[] { "   ", "1414", "QM1", "CHANNEL", "QUEUE", typeof(ArgumentException) },
            new object[] { "Host", "", "QM1", "CHANNEL", "QUEUE", typeof(ArgumentOutOfRangeException) },
            new object[] { "Host", "0", "QM1", "CHANNEL", "QUEUE", typeof(ArgumentOutOfRangeException) },
            new object[] { "Host", "70000", "QM1", "CHANNEL", "QUEUE", typeof(ArgumentOutOfRangeException) },
            new object[] { "Host", "1414", null, "CHANNEL", "QUEUE", typeof(ArgumentException) },
            new object[] { "Host", "1414", "", "CHANNEL", "QUEUE", typeof(ArgumentException) },
            new object[] { "Host", "1414", "QM1", null, "QUEUE", typeof(ArgumentException) },
            new object[] { "Host", "1414", "QM1", "", "QUEUE", typeof(ArgumentException) },
            new object[] { "Host", "1414", "QM1", "CHANNEL", null, typeof(ArgumentException) },
            new object[] { "Host", "1414", "QM1", "CHANNEL", "", typeof(ArgumentException) }
        };

    [Fact]
    public void Constructor_WithValidConfig_ShouldInitializeSuccessfully()
    {
        // Arrange & Act
        var eventBus = new IbmMqEventBus(
            _loggerMock.Object,
            "localhost",
            1414,
            "QM1",
            "DEV.APP.SVRCONN",
            "DEV.QUEUE.1",
            _options);

        // Assert
        eventBus.Should().NotBeNull();
    }

    [Theory(DisplayName = "Constructor throws expected exception for invalid config")]
    [MemberData(nameof(InvalidConfigs))]
    [Trait("Enhancement", "ConstructorValidation")]
    public void Constructor_WithInvalidConfig_ShouldThrowExpectedException(
        string? host, string portStr, string? queueManager, string? channel, string? queue, Type expectedException)
    {
        // Arrange
        Action act = !int.TryParse(portStr, out var port)
            ? () => throw new ArgumentOutOfRangeException(nameof(portStr), "Port must be a valid integer.")
            : () => _ = new IbmMqEventBus(
                _loggerMock.Object,
                host!,
                port,
                queueManager!,
                channel!,
                queue!,
                _options);

        // Act
        var exception = Record.Exception(act);

        // Assert
        exception.Should().NotBeNull();
        exception!.GetType().Should().Be(expectedException);
    }

    [Fact]
    public void Constructor_WithInvalidPort_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange & Act
        var act = () => _ = new IbmMqEventBus(
            _loggerMock.Object,
            "localhost",
            0,
            "QM1",
            "DEV.APP.SVRCONN",
            "DEV.QUEUE.1",
            _options);

        // Assert
        var exception = Record.Exception(act);
        exception.Should().NotBeNull();
        exception!.GetType().Should().Be(typeof(ArgumentOutOfRangeException));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new IbmMqEventBus(
            null!,
            "localhost",
            1414,
            "QM1",
            "DEV.APP.SVRCONN",
            "DEV.QUEUE.1",
            _options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new IbmMqEventBus(
            _loggerMock.Object,
            "localhost",
            1414,
            "QM1",
            "DEV.APP.SVRCONN",
            "DEV.QUEUE.1",
            null!));
    }

    [Fact]
    public async Task PublishAsync_WithNullEvent_ShouldThrowArgumentNullException()
    {
        // Arrange
        var eventBus = new IbmMqEventBus(
            _loggerMock.Object,
            "localhost",
            1414,
            "QM1",
            "DEV.APP.SVRCONN",
            "DEV.QUEUE.1",
            _options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => eventBus.PublishAsync<CardActionsRetrievedEvent>(null!));
    }

    [Fact]
    public async Task PublishBatchAsync_WithNullCollection_ShouldThrowArgumentNullException()
    {
        // Arrange
        var eventBus = new IbmMqEventBus(
            _loggerMock.Object,
            "localhost",
            1414,
            "QM1",
            "DEV.APP.SVRCONN",
            "DEV.QUEUE.1",
            _options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => eventBus.PublishBatchAsync<CardActionsRetrievedEvent>(null!));
    }

    [Fact]
    public async Task PublishBatchAsync_WithEmptyCollection_ShouldThrowArgumentException()
    {
        // Arrange
        var eventBus = new IbmMqEventBus(
            _loggerMock.Object,
            "localhost",
            1414,
            "QM1",
            "DEV.APP.SVRCONN",
            "DEV.QUEUE.1",
            _options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => eventBus.PublishBatchAsync<CardActionsRetrievedEvent>(Array.Empty<CardActionsRetrievedEvent>()));
    }

    [Fact]
    [Trait("Enhancement", "Lifecycle")]
    public async Task PublishAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var eventBus = new IbmMqEventBus(
            _loggerMock.Object,
            "localhost",
            1414,
            "QM1",
            "DEV.APP.SVRCONN",
            "DEV.QUEUE.1",
            _options);

        eventBus.Dispose();

        var @event = new CardActionsRetrievedEvent
        {
            UserId = "User1",
            CardNumber = "CARD123",
            CardType = "Debit",
            CardStatus = "Active",
            AllowedActions = new[] { "ACTION1" },
            TraceId = "test-trace"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => eventBus.PublishAsync(@event));
    }

    [Fact]
    [Trait("Enhancement", "DisposeLogging")]
    public void Dispose_ShouldLogAndReleaseResources()
    {
        // Arrange
        var eventBus = new IbmMqEventBus(
            _loggerMock.Object,
            "localhost",
            1414,
            "QM1",
            "DEV.APP.SVRCONN",
            "DEV.QUEUE.1",
            _options);

        // Act
        eventBus.Dispose();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("disposed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    [Trait("Enhancement", "DisposeIdempotency")]
    public void Dispose_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var eventBus = new IbmMqEventBus(
            _loggerMock.Object,
            "localhost",
            1414,
            "QM1",
            "DEV.APP.SVRCONN",
            "DEV.QUEUE.1",
            _options);

        // Act & Assert (should not throw)
        eventBus.Dispose();
        eventBus.Dispose(); // Idempotent
    }
}