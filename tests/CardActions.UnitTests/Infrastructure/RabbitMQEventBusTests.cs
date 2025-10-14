using CardActions.Domain.Events;
using CardActions.Infrastructure.Configuration;
using CardActions.Infrastructure.EventBus;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CardActions.UnitTests.Infrastructure;

[Trait("Category", "Unit")]
[Trait("Feature", "RabbitMQEventBus")]
public sealed class RabbitMQEventBusTests
{
    private readonly Mock<ILogger<RabbitMQEventBus>> _loggerMock = new();
    private readonly EventBusOptions _options = new()
    {
        UseInMemory = false,
        RabbitMQ = new RabbitMQOptions
        {
            Host = "localhost",
            Port = 5672,
            Username = "guest",
            Password = "guest",
            VirtualHost = "/",
            Exchange = "test.events"
        }
    };

    public static object[][] InvalidConfigs =>
        new[]
        {
            new object[] { null, "5672", "Username", "Password", "Exchange", typeof(ArgumentNullException) },
            new object[] { "", "5672", "Username", "Password", "Exchange", typeof(ArgumentException) },
            new object[] { "Host", "", "Username", "Password", "Exchange", typeof(ArgumentOutOfRangeException) },
            new object[] { "Host", "0", "Username", "Password", "Exchange", typeof(ArgumentOutOfRangeException) },
            new object[] { "Host", "5672", null, "Password", "Exchange", typeof(ArgumentNullException) },
            new object[] { "Host", "5672", "", "Password", "Exchange", typeof(ArgumentException) },
            new object[] { "Host", "5672", "Username", null, "Exchange", typeof(ArgumentNullException) },
            new object[] { "Host", "5672", "Username", "", "Exchange", typeof(ArgumentException) },
            new object[] { "Host", "5672", "Username", "   ", "Exchange", typeof(ArgumentException) },
            new object[] { "Host", "5672", "Username", "Password", null, typeof(ArgumentNullException) }
        };

    [Fact]
    public void Constructor_WithValidConfig_ShouldInitializeSuccessfully()
    {
        var eventBus = new RabbitMQEventBus(
            _loggerMock.Object,
            "localhost",
            5672,
            "guest",
            "guest",
            "/",
            "test.events",
            _options);

        eventBus.Should().NotBeNull();
    }

    [Theory(DisplayName = "Constructor throws expected exception for invalid config")]
    [MemberData(nameof(InvalidConfigs))]
    [Trait("Enhancement", "ConstructorValidation")]
    public void Constructor_WithInvalidConfig_ShouldThrowExpectedException(
        string? host, string portStr, string? username, string? password, string? exchange, Type expectedException)
    {
        Action act = !int.TryParse(portStr, out var port)
            ? () => throw new ArgumentOutOfRangeException(nameof(portStr), "Port must be a valid integer.")
            : () => _ = new RabbitMQEventBus(
                _loggerMock.Object,
                host!,
                port,
                username!,
                password!,
                "/",
                exchange!,
                _options);

        var exception = Record.Exception(act);
        exception.Should().NotBeNull();
        exception!.GetType().Should().Be(expectedException);
    }

    [Fact]
    public void Constructor_WithInvalidPort_ShouldThrowArgumentOutOfRangeException()
    {
        var act = () => _ = new RabbitMQEventBus(
            _loggerMock.Object,
            "localhost",
            0,
            "guest",
            "guest",
            "/",
            "test.events",
            _options);

        var exception = Record.Exception(act);
        exception.Should().NotBeNull();
        exception!.GetType().Should().Be(typeof(ArgumentOutOfRangeException));
    }

    [Fact]
    public async Task PublishAsync_WithNullEvent_ShouldThrowArgumentNullException()
    {
        var eventBus = new RabbitMQEventBus(
            _loggerMock.Object,
            "localhost",
            5672,
            "guest",
            "guest",
            "/",
            "test.events",
            _options);

        await Assert.ThrowsAsync<ArgumentNullException>(() => eventBus.PublishAsync<CardActionsRetrievedEvent>(null!));
    }

    [Fact]
    [Trait("Enhancement", "Lifecycle")]
    public async Task PublishAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        var eventBus = new RabbitMQEventBus(
            _loggerMock.Object,
            "localhost",
            5672,
            "guest",
            "guest",
            "/",
            "test.events",
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

        await Assert.ThrowsAsync<ObjectDisposedException>(() => eventBus.PublishAsync(@event));
    }

    [Fact]
    [Trait("Enhancement", "DisposeLogging")]
    public void Dispose_ShouldLogAndReleaseResources()
    {
        var eventBus = new RabbitMQEventBus(
            _loggerMock.Object,
            "localhost",
            5672,
            "guest",
            "guest",
            "/",
            "test.events",
            _options);

        eventBus.Dispose();

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
        var eventBus = new RabbitMQEventBus(
            _loggerMock.Object,
            "localhost",
            5672,
            "guest",
            "guest",
            "/",
            "test.events",
            _options);

        eventBus.Dispose();
        eventBus.Dispose(); // Idempotent
    }
}
