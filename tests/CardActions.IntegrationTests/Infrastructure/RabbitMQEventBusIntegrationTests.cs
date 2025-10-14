using CardActions.Domain.Events;
using CardActions.Infrastructure.Configuration;
using CardActions.Infrastructure.EventBus;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CardActions.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for RabbitMQEventBus with real RabbitMQ instance.
/// Requires RabbitMQ running with user "cardactions" configured.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Requires", "RabbitMQ")]
public class RabbitMQEventBusIntegrationTests : IAsyncLifetime
{
    private readonly RabbitMQEventBus _eventBus;
    private readonly Mock<ILogger<RabbitMQEventBus>> _loggerMock;
    private readonly EventBusOptions _options;

    public RabbitMQEventBusIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<RabbitMQEventBus>>();

        // Load configuration from appsettings.Test.json or environment variables
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Test.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        _options = new EventBusOptions
        {
            UseInMemory = false,
            RabbitMQ = new RabbitMQOptions
            {
                Host = configuration["EventBus:RabbitMQ:Host"] ??
                       Environment.GetEnvironmentVariable("RABBITMQ_HOST") ??
                       "localhost",
                Port = int.Parse(configuration["EventBus:RabbitMQ:Port"] ??
                                Environment.GetEnvironmentVariable("RABBITMQ_PORT") ??
                                "5672"),
                Username = configuration["EventBus:RabbitMQ:Username"] ??
                          Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ??
                          "cardactions",
                Password = configuration["EventBus:RabbitMQ:Password"] ??
                          Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ??
                          "devpassword",
                VirtualHost = configuration["EventBus:RabbitMQ:VirtualHost"] ?? "/",
                Exchange = configuration["EventBus:RabbitMQ:Exchange"] ?? "cardactions.integration.test"
            }
        };

        _options.RabbitMQ.Validate();

        _eventBus = new RabbitMQEventBus(
            _loggerMock.Object,
            _options.RabbitMQ.Host,
            _options.RabbitMQ.Port,
            _options.RabbitMQ.Username,
            _options.RabbitMQ.Password,
            _options.RabbitMQ.VirtualHost,
            _options.RabbitMQ.Exchange,
            _options);
    }

    public async Task InitializeAsync()
    {
        // Verify RabbitMQ is reachable before running tests
        if (_options.RabbitMQ is null)
            throw new InvalidOperationException("RabbitMQ configuration is missing in test options.");

        try
        {
            var testEvent = new CardActionsRetrievedEvent
            {
                UserId = "init-test",
                CardNumber = "INIT",
                CardType = "Test",
                CardStatus = "Test",
                AllowedActions = [],
                TraceId = "init-trace"
            };

            await _eventBus.PublishAsync(testEvent);
            Console.WriteLine($"RabbitMQ connection verified - Host: {_options.RabbitMQ.Host}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"RabbitMQ not available for integration tests. " +
                $"Host: {_options.RabbitMQ.Host}:{_options.RabbitMQ.Port}, " +
                $"User: {_options.RabbitMQ.Username}, " +
                $"Error: {ex.Message}", ex);
        }
    }

    public Task DisposeAsync()
    {
        _eventBus?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PublishAsync_WithValidEvent_ShouldPublishSuccessfully()
    {
        // Arrange
        var @event = new CardActionsRetrievedEvent
        {
            UserId = "User1",
            CardNumber = "CARD123",
            CardType = "Debit",
            CardStatus = "Active",
            AllowedActions = ["ACTION1", "ACTION2"],
            TraceId = "integration-test-trace"
        };

        // Act
        var act = async () => await _eventBus.PublishAsync(@event);

        // Assert
        await act.Should().NotThrowAsync();

        // Verify logger called with success message
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Event published")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task PublishBatchAsync_WithMultipleEvents_ShouldPublishAll()
    {
        // Arrange
        var events = new[]
        {
            new CardNotFoundEvent
            {
                UserId = "User1",
                CardNumber = "CARD1",
                TraceId = "trace-1"
            },
            new CardNotFoundEvent
            {
                UserId = "User2",
                CardNumber = "CARD2",
                TraceId = "trace-2"
            }
        };

        // Act
        var act = async () => await _eventBus.PublishBatchAsync(events);

        // Assert
        await act.Should().NotThrowAsync();
    }
}