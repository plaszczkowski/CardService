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
            new object[] { new IbmMqOptions { Host = null!, Port = 1414, QueueManager = "QM1", Channel = "CHANNEL", QueueName = "QUEUE" }, typeof(ArgumentException) },
            new object[] { new IbmMqOptions { Host = "", Port = 1414, QueueManager = "QM1", Channel = "CHANNEL", QueueName = "QUEUE" }, typeof(ArgumentException) },
            new object[] { new IbmMqOptions { Host = "   ", Port = 1414, QueueManager = "QM1", Channel = "CHANNEL", QueueName = "QUEUE" }, typeof(ArgumentException) },
            new object[] { new IbmMqOptions { Host = "Host", Port = 0, QueueManager = "QM1", Channel = "CHANNEL", QueueName = "QUEUE" }, typeof(ArgumentOutOfRangeException) },
            new object[] { new IbmMqOptions { Host = "Host", Port = 70000, QueueManager = "QM1", Channel = "CHANNEL", QueueName = "QUEUE" }, typeof(ArgumentOutOfRangeException) },
            new object[] { new IbmMqOptions { Host = "Host", Port = 1414, QueueManager = null!, Channel = "CHANNEL", QueueName = "QUEUE" }, typeof(ArgumentException) },
            new object[] { new IbmMqOptions { Host = "Host", Port = 1414, QueueManager = "", Channel = "CHANNEL", QueueName = "QUEUE" }, typeof(ArgumentException) },
            new object[] { new IbmMqOptions { Host = "Host", Port = 1414, QueueManager = "QM1", Channel = null!, QueueName = "QUEUE" }, typeof(ArgumentException) },
            new object[] { new IbmMqOptions { Host = "Host", Port = 1414, QueueManager = "QM1", Channel = "", QueueName = "QUEUE" }, typeof(ArgumentException) },
            new object[] { new IbmMqOptions { Host = "Host", Port = 1414, QueueManager = "QM1", Channel = "CHANNEL", QueueName = null! }, typeof(ArgumentException) },
            new object[] { new IbmMqOptions { Host = "Host", Port = 1414, QueueManager = "QM1", Channel = "CHANNEL", QueueName = "" }, typeof(ArgumentException) }
        };

    [Fact]
    public void Constructor_WithValidConfig_ShouldInitializeSuccessfully()
    {
        // Arrange & Act
        var eventBus = new IbmMqEventBus(
            _loggerMock.Object,
            _options.IbmMQ,
            _options);

        // Assert
        eventBus.Should().NotBeNull();
    }

    [Theory(DisplayName = "Constructor throws expected exception for invalid config")]
    [MemberData(nameof(InvalidConfigs))]
    [Trait("Enhancement", "ConstructorValidation")]
    public void Constructor_WithInvalidConfig_ShouldThrowExpectedException(
        IbmMqOptions invalidOptions, Type expectedException)
    {
        // Arrange
        var testOptions = new EventBusOptions { Provider = "IbmMQ", IbmMQ = invalidOptions };

        // Act
        Action act = () => _ = new IbmMqEventBus(
            _loggerMock.Object,
            invalidOptions,
            testOptions);

        // Assert
        var exception = Record.Exception(act);
        exception.Should().NotBeNull();
        exception!.GetType().Should().Be(expectedException);
    }

    [Fact]
    public void Constructor_WithInvalidPort_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var invalidOptions = new IbmMqOptions
        {
            Host = "localhost",
            Port = 0,
            QueueManager = "QM1",
            Channel = "DEV.APP.SVRCONN",
            QueueName = "DEV.QUEUE.1"
        };
        var testOptions = new EventBusOptions { Provider = "IbmMQ", IbmMQ = invalidOptions };

        // Act
        var act = () => _ = new IbmMqEventBus(
            _loggerMock.Object,
            invalidOptions,
            testOptions);

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
            _options.IbmMQ,
            _options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new IbmMqEventBus(
            _loggerMock.Object,
            null!,
            _options));
    }

    [Fact]
    public void Constructor_WithNullEventBusOptions_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new IbmMqEventBus(
            _loggerMock.Object,
            _options.IbmMQ,
            null!));
    }

    [Fact]
    public async Task PublishAsync_WithNullEvent_ShouldThrowArgumentNullException()
    {
        // Arrange
        var eventBus = new IbmMqEventBus(
            _loggerMock.Object,
            _options.IbmMQ,
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
            _options.IbmMQ,
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
            _options.IbmMQ,
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
            _options.IbmMQ,
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
            _options.IbmMQ,
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
            _options.IbmMQ,
            _options);

        // Act & Assert (should not throw)
        eventBus.Dispose();
        eventBus.Dispose(); // Idempotent
    }
}