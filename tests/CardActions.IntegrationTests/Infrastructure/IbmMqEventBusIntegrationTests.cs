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
/// Integration tests for IbmMqEventBus with real IBM MQ instance.
/// Requires IBM MQ running with Queue Manager "QM1" and queue "DEV.QUEUE.1" configured.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Requires", "IbmMQ")]
public class IbmMqEventBusIntegrationTests : IAsyncLifetime
{
    private readonly IbmMqEventBus _eventBus;
    private readonly Mock<ILogger<IbmMqEventBus>> _loggerMock;
    private readonly EventBusOptions _options;

    public IbmMqEventBusIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<IbmMqEventBus>>();

        // Load configuration from appsettings.Test.json or environment variables
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Test.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        _options = new EventBusOptions
        {
            Provider = "IbmMQ",
            IbmMQ = new IbmMqOptions
            {
                QueueManager = configuration["EventBus:IbmMQ:QueueManager"] ??
                              Environment.GetEnvironmentVariable("IBMMQ_QUEUE_MANAGER") ??
                              "QM1",
                Host = configuration["EventBus:IbmMQ:Host"] ??
                       Environment.GetEnvironmentVariable("IBMMQ_HOST") ??
                       "localhost",
                Port = int.Parse(configuration["EventBus:IbmMQ:Port"] ??
                                Environment.GetEnvironmentVariable("IBMMQ_PORT") ??
                                "1414"),
                Channel = configuration["EventBus:IbmMQ:Channel"] ??
                         Environment.GetEnvironmentVariable("IBMMQ_CHANNEL") ??
                         "DEV.APP.SVRCONN",
                QueueName = configuration["EventBus:IbmMQ:QueueName"] ??
                           Environment.GetEnvironmentVariable("IBMMQ_QUEUE_NAME") ??
                           "DEV.QUEUE.1",
                Username = configuration["EventBus:IbmMQ:Username"] ??
                          Environment.GetEnvironmentVariable("IBMMQ_USERNAME") ??
                          "app",
                Password = configuration["EventBus:IbmMQ:Password"] ??
                          Environment.GetEnvironmentVariable("IBMMQ_PASSWORD") ??
                          "passw0rd"
            }
        };

        _options.IbmMQ.Validate();

        _eventBus = new IbmMqEventBus(
            _loggerMock.Object,
            _options.IbmMQ,
            _options);
    }

    public async Task InitializeAsync()
    {
        // Verify IBM MQ is reachable before running tests
        if (_options.IbmMQ is null)
            throw new InvalidOperationException("IBM MQ configuration is missing in test options.");

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
            Console.WriteLine($"IBM MQ connection verified - QueueManager: {_options.IbmMQ.QueueManager}, Host: {_options.IbmMQ.Host}:{_options.IbmMQ.Port}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"IBM MQ not available for integration tests. " +
                $"QueueManager: {_options.IbmMQ.QueueManager}, " +
                $"Host: {_options.IbmMQ.Host}:{_options.IbmMQ.Port}, " +
                $"Channel: {_options.IbmMQ.Channel}, " +
                $"Queue: {_options.IbmMQ.QueueName}, " +
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

    [Fact]
    public async Task PublishAsync_WithConnectionFailure_ShouldThrowAndLog()
    {
        // Arrange
        var invalidOptions = new EventBusOptions
        {
            Provider = "IbmMQ",
            IbmMQ = new IbmMqOptions
            {
                QueueManager = "INVALID_QM",
                Host = "invalid-host",
                Port = 9999,
                Channel = "INVALID.CHANNEL",
                QueueName = "INVALID.QUEUE"
            }
        };

        var invalidEventBus = new IbmMqEventBus(
            _loggerMock.Object,
            invalidOptions.IbmMQ,
            invalidOptions);

        var @event = new CardActionsRetrievedEvent
        {
            UserId = "User1",
            CardNumber = "CARD123",
            CardType = "Debit",
            CardStatus = "Active",
            AllowedActions = ["ACTION1"],
            TraceId = "test-trace"
        };

        // Act & Assert
        var exception = await Record.ExceptionAsync(() => invalidEventBus.PublishAsync(@event));
        exception.Should().NotBeNull();

        // Verify error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        invalidEventBus.Dispose();
    }
}