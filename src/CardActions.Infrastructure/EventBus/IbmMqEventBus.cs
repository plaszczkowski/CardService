using CardActions.Domain.Abstractions;
using CardActions.Domain.Events;
using CardActions.Infrastructure.Configuration;
using IBM.WMQ;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Text;
using System.Text.Json;

namespace CardActions.Infrastructure.EventBus;

/// <summary>
/// Production-grade IBM MQ event bus implementation.
/// Uses IBM.WMQ .NET Standard Client (amqmdnetstd/IBMMQDotnetClient).
/// Thread-safe with lazy connection initialization.
/// </summary>
public sealed class IbmMqEventBus : IEventBus, IDisposable
{
    private readonly ILogger<IbmMqEventBus> _logger;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly IbmMqOptions _options;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private MQQueueManager? _queueManager;
    private bool _disposed;

    /// <summary>
    /// Initializes IBM MQ event bus with configuration.
    /// Connection is lazily created on first publish.
    /// </summary>
    /// <param name="logger">Structured logger for diagnostics</param>
    /// <param name="options">IBM MQ configuration options</param>
    /// <param name="eventBusOptions">Global event bus configuration</param>
    /// <exception cref="ArgumentNullException">Thrown if any required parameter is null</exception>
    public IbmMqEventBus(ILogger<IbmMqEventBus> logger, IbmMqOptions options, EventBusOptions eventBusOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        ArgumentNullException.ThrowIfNull(eventBusOptions);

        // Validate configuration on construction (fail-fast)
        _options.Validate();

        _serializerOptions = eventBusOptions.SerializerOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        _logger.LogInformation(
            "IbmMqEventBus initialized - QueueManager: {QueueManager}, Host: {Host}:{Port}, Channel: {Channel}, Queue: {Queue}",
            _options.QueueManager, _options.Host, _options.Port, _options.Channel, _options.QueueName);
    }

    /// <summary>
    /// Publishes a domain event to IBM MQ queue.
    /// Uses persistent messages with correlation ID for tracing.
    /// </summary>
    /// <typeparam name="TEvent">Domain event type</typeparam>
    /// <param name="event">Event instance to publish</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing async operation</returns>
    /// <exception cref="ArgumentNullException">Thrown if event is null</exception>
    /// <exception cref="MQException">Thrown if IBM MQ operation fails</exception>
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(@event);
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            // Ensure connection exists (lazy initialization + recovery)
            await EnsureConnectionAsync(cancellationToken);

            // Serialize event to JSON
            var message = JsonSerializer.Serialize(@event, _serializerOptions);
            var body = Encoding.UTF8.GetBytes(message);

            // Create IBM MQ message
            var mqMessage = new MQMessage
            {
                CharacterSet = 1208, // UTF-8
                Encoding = MQC.MQENC_NATIVE,
                Format = MQC.MQFMT_STRING,
                Persistence = MQC.MQPER_PERSISTENT, // Survive broker restarts
                MessageId = Encoding.UTF8.GetBytes(@event.EventId.ToString()),
                CorrelationId = Encoding.UTF8.GetBytes(@event.EventId.ToString()),
            };

            // Add custom properties for observability
            mqMessage.SetStringProperty("EventType", @event.EventType);
            mqMessage.SetIntProperty("SchemaVersion", @event.SchemaVersion);
            mqMessage.SetStringProperty("PublishedAt", DateTime.UtcNow.ToString("O"));

            // Write message body
            mqMessage.WriteString(message);

            // Open queue for output (each publish gets fresh handle - thread-safe)
            var openOptions = MQC.MQOO_OUTPUT | MQC.MQOO_FAIL_IF_QUIESCING;
            using var queue = _queueManager!.AccessQueue(_options.QueueName, openOptions);

            // Put message to queue (synchronous - IBM MQ .NET doesn't have true async put)
            var putOptions = new MQPutMessageOptions();
            await Task.Run(() => queue.Put(mqMessage, putOptions), cancellationToken);

            _logger.LogInformation(
                "Event published to IBM MQ - EventType: {EventType}, EventId: {EventId}, Queue: {Queue}, Size: {Size} bytes",
                @event.EventType, @event.EventId, _options.QueueName, body.Length);
        }
        catch (MQException mqEx)
        {
            // IBM MQ specific exception handling
            _logger.LogError(mqEx,
                "IBM MQ error publishing event {EventType} (EventId: {EventId}) - ReasonCode: {ReasonCode}, CompCode: {CompCode}",
                @event.EventType, @event.EventId, mqEx.ReasonCode, mqEx.CompletionCode);

            // Handle connection failures - clear connection for retry
            if (IsConnectionError(mqEx.ReasonCode))
            {
                await DisconnectAsync();
            }

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error publishing event {EventType} (EventId: {EventId})", @event.EventType, @event.EventId);
            throw;
        }
    }

    /// <summary>
    /// Publishes multiple events as a batch.
    /// Each event published individually (IBM MQ doesn't have native batch support).
    /// Partial failure possible: Some events may publish successfully before exception.
    /// </summary>
    /// <typeparam name="TEvent">Domain event type</typeparam>
    /// <param name="events">Collection of events to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing async operation</returns>
    /// <exception cref="ArgumentNullException">Thrown if events collection is null</exception>
    /// <exception cref="ArgumentException">Thrown if events collection is empty</exception>
    /// <exception cref="AggregateException">Thrown if any events fail to publish</exception>
    public async Task PublishBatchAsync<TEvent>(
        IEnumerable<TEvent> events,
        CancellationToken cancellationToken = default)
        where TEvent : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(events);

        var eventList = events.ToList();

        if (eventList.Count == 0)
            throw new ArgumentException("Event batch cannot be empty", nameof(events));

        _logger.LogInformation("Batch publish initiated - {Count} events of type {EventType}", eventList.Count, typeof(TEvent).Name);

        var publishedCount = 0;
        var failedEvents = new List<(TEvent Event, Exception Error)>();

        foreach (var @event in eventList)
        {
            try
            {
                await PublishAsync(@event, cancellationToken);
                publishedCount++;
            }
            catch (Exception ex)
            {
                failedEvents.Add((@event, ex));
                _logger.LogWarning(ex, "Failed to publish event {EventType} (EventId: {EventId}) in batch", typeof(TEvent).Name, @event?.EventId);
            }
        }

        if (failedEvents.Count > 0)
        {
            _logger.LogError("Batch publish completed with partial failure - {PublishedCount}/{TotalCount} succeeded", publishedCount, eventList.Count);

            var aggregate = new AggregateException(
                $"Batch publish failed for {failedEvents.Count} event(s)",
                failedEvents.Select(f => new InvalidOperationException(
                    $"Failed to publish event {typeof(TEvent).Name} with ID {f.Event?.EventId}", f.Error)));

            throw aggregate;
        }

        _logger.LogInformation("Batch publish completed successfully - {PublishedCount} events published", publishedCount);
    }

    /// <summary>
    /// Ensures IBM MQ connection is established.
    /// Thread-safe lazy initialization with automatic recovery.
    /// </summary>
    private async Task EnsureConnectionAsync(CancellationToken cancellationToken)
    {
        // Fast path: connection already exists and is open
        if (_queueManager?.IsConnected == true)
            return;

        // Slow path: acquire lock and create connection
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check pattern: another thread may have created connection
            if (_queueManager?.IsConnected == true)
                return;

            // Dispose old connection if exists
            _queueManager?.Disconnect();
            _queueManager?.Dispose();

            _logger.LogInformation("Establishing IBM MQ connection - QueueManager: {QueueManager}, Host: {Host}:{Port}",
                _options.QueueManager, _options.Host, _options.Port);

            // Build connection properties (IBM MQ specific)
            var properties = new Hashtable
            {
                { MQC.TRANSPORT_PROPERTY, MQC.TRANSPORT_MQSERIES_CLIENT },
                { MQC.HOST_NAME_PROPERTY, _options.Host },
                { MQC.PORT_PROPERTY, _options.Port },
                { MQC.CHANNEL_PROPERTY, _options.Channel }
            };

            // Add authentication if provided
            if (!string.IsNullOrEmpty(_options.Username))
            {
                properties.Add(MQC.USER_ID_PROPERTY, _options.Username);
            }

            if (!string.IsNullOrEmpty(_options.Password))
            {
                properties.Add(MQC.PASSWORD_PROPERTY, _options.Password);
            }

            // Add SSL configuration if enabled
            if (_options.UseSsl)
            {
                properties.Add(MQC.SSL_CIPHER_SPEC_PROPERTY, _options.SslCipherSpec);
            }

            // Connect to Queue Manager (synchronous - no true async in IBM MQ .NET)
            _queueManager = await Task.Run(
                () => new MQQueueManager(_options.QueueManager, properties),
                cancellationToken);

            _logger.LogInformation(
                "IBM MQ connection established - QueueManager: {QueueManager}, Connected: {Connected}",
                _options.QueueManager, _queueManager.IsConnected);
        }
        catch (MQException ex)
        {
            _logger.LogError(ex,
                "Failed to establish IBM MQ connection - QueueManager: {QueueManager}, ReasonCode: {ReasonCode}",
                _options.QueueManager, ex.ReasonCode);
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Disconnects from IBM MQ (for recovery scenarios).
    /// </summary>
    private async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_queueManager != null)
            {
                _queueManager.Disconnect();
                _queueManager.Dispose();
                _queueManager = null;
                _logger.LogInformation("IBM MQ connection disposed for recovery");
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Determines if MQException represents a connection error requiring reconnection.
    /// </summary>
    private static bool IsConnectionError(int reasonCode)
    {
        return reasonCode switch
        {
            MQC.MQRC_CONNECTION_BROKEN => true,
            MQC.MQRC_CONNECTION_ERROR => true,
            MQC.MQRC_Q_MGR_NOT_AVAILABLE => true,
            MQC.MQRC_Q_MGR_QUIESCING => true,
            MQC.MQRC_CONNECTION_NOT_AUTHORIZED => true,
            _ => false
        };
    }

    /// <summary>
    /// Disposes IBM MQ connection and resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            _queueManager?.Disconnect();
            _queueManager?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during IBM MQ connection disposal");
        }

        _connectionLock.Dispose();
        _disposed = true;

        _logger.LogInformation("IbmMqEventBus disposed");
    }
}