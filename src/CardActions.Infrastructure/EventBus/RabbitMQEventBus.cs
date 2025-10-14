using CardActions.Domain.Abstractions;
using CardActions.Domain.Events;
using CardActions.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Text;
using System.Text.Json;

namespace CardActions.Infrastructure.EventBus;

/// <summary>
/// Production-grade RabbitMQ event bus implementation for v7.0.0.
/// </summary>
public sealed class RabbitMQEventBus : IEventBus, IDisposable
{
    private readonly ILogger<RabbitMQEventBus> _logger;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ConnectionFactory _connectionFactory;
    private readonly string _exchange;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private IConnection? _connection;
    private bool _disposed;
    private readonly EventBusOptions _options;


    /// <summary>
    /// Initializes RabbitMQ event bus with configuration.
    /// Connection is lazily created on first publish.
    /// </summary>
    public RabbitMQEventBus(
        ILogger<RabbitMQEventBus> logger,
        string host,
        int port,
        string username,
        string password,
        string virtualHost,
        string exchange,
        EventBusOptions eventBusOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
        _options = eventBusOptions ?? throw new ArgumentNullException(nameof(eventBusOptions));

        ArgumentNullException.ThrowIfNull(host);
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("Host cannot be null or empty", nameof(host));

        ArgumentNullException.ThrowIfNull(username);
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be null or empty", nameof(username));

        ArgumentNullException.ThrowIfNull(password);
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));

        ArgumentNullException.ThrowIfNull(virtualHost);
        if (string.IsNullOrWhiteSpace(virtualHost))
            throw new ArgumentException("Virtual host cannot be null or empty", nameof(virtualHost));

        // Validate port range
        if (port <= 0 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535");

        _connectionFactory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = username,
            Password = password,
            VirtualHost = virtualHost ?? "/",

            // Production settings for reliability
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
            RequestedHeartbeat = TimeSpan.FromSeconds(60),
            RequestedConnectionTimeout = TimeSpan.FromSeconds(30)
        };

        _serializerOptions = eventBusOptions.SerializerOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        _logger.LogInformation(
            "RabbitMQEventBus initialized (v7.0.0) - Host: {Host}:{Port}, VHost: {VHost}, Exchange: {Exchange}",
            host, port, virtualHost, exchange);
    }

    /// <summary>
    /// Publishes domain event to RabbitMQ using v7.0.0 API.
    /// Uses fire-and-forget pattern - confirms disabled for performance.
    /// For guaranteed delivery, consider using transactional publishing or confirms in a separate implementation.
    /// </summary>
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(@event);
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            await EnsureConnectionAsync(cancellationToken);

            // v7.0.0 API: CreateChannelAsync() with await using
            await using var channel = await _connection!.CreateChannelAsync();

            // v7.0.0 API: ExchangeDeclareAsync (idempotent operation)
            await channel.ExchangeDeclareAsync(
                exchange: _exchange,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            // Serialize event to JSON
            var message = JsonSerializer.Serialize(@event, _serializerOptions);
            var body = Encoding.UTF8.GetBytes(message);
            var routingKey = @event.EventType.ToLowerInvariant();

            // Set message properties for durability and tracing
            var properties = new BasicProperties
            {
                Persistent = true, // Survive broker restarts
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                MessageId = @event.EventId.ToString(),
                Timestamp = new AmqpTimestamp(
                    new DateTimeOffset(@event.OccurredAt).ToUnixTimeSeconds()),
                Type = @event.EventType,
                Headers = new Dictionary<string, object?>
                {
                    { "schema-version", @event.SchemaVersion },
                    { "published-at", DateTime.UtcNow.ToString("O") }
                }
            };

            // 7.0.0 API: BasicPublishAsync
            await channel.BasicPublishAsync(
                exchange: _exchange,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Event published to RabbitMQ - EventType: {EventType}, EventId: {EventId}, RoutingKey: {RoutingKey}, Size: {Size} bytes",
                @event.EventType, @event.EventId, routingKey, body.Length);
        }
        catch (BrokerUnreachableException ex)
        {
            _logger.LogError(ex,
                "Failed to publish event {EventType} (EventId: {EventId}) - RabbitMQ broker unreachable",
                @event.EventType, @event.EventId);
            throw;
        }
        catch (AlreadyClosedException ex)
        {
            _logger.LogWarning(ex,
                "Connection closed while publishing event {EventType} (EventId: {EventId}) - will reconnect on next publish",
                @event.EventType, @event.EventId);

            await DisconnectAsync();
            throw;
        }
        catch (OperationInterruptedException ex)
        {
            _logger.LogWarning(ex,
                "Operation interrupted while publishing event {EventType} (EventId: {EventId}) - Code: {ShutdownCode}",
                @event.EventType, @event.EventId, ex.ShutdownReason?.ReplyCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error publishing event {EventType} (EventId: {EventId})",
                @event.EventType, @event.EventId);
            throw;
        }
    }

    /// <summary>
    /// Publishes a batch of domain events individually.
    /// Validates input, enforces guard clauses, and logs lifecycle events.
    /// </summary>
    public async Task PublishBatchAsync<TEvent>(
        IEnumerable<TEvent> events,
        CancellationToken cancellationToken = default)
        where TEvent : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(events);

        var eventList = events.ToList();

        if (eventList.Count == 0)
            throw new ArgumentException("Event batch cannot be empty", nameof(events));

        _logger.LogInformation(
            "Batch publish initiated - {Count} events of type {EventType}",
            eventList.Count, typeof(TEvent).Name);

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
                _logger.LogWarning(ex,
                    "Failed to publish event {EventType} (EventId: {EventId})",
                    typeof(TEvent).Name, @event?.EventId);
            }
        }

        if (failedEvents.Count > 0)
        {
            _logger.LogError(
                "Batch publish completed with partial failure - {PublishedCount}/{TotalCount} succeeded",
                publishedCount, eventList.Count);

            var aggregate = new AggregateException(
                $"Batch publish failed for {failedEvents.Count} event(s)",
                failedEvents.Select(f => new InvalidOperationException(
                    $"Failed to publish event {typeof(TEvent).Name} with ID {f.Event?.EventId}", f.Error)));

            throw aggregate;
        }

        _logger.LogInformation(
            "Batch publish completed successfully - {PublishedCount} events published",
            publishedCount);
    }

    /// <summary>
    /// Ensures RabbitMQ connection is established.
    /// Thread-safe lazy initialization with automatic recovery.
    /// </summary>
    private async Task EnsureConnectionAsync(CancellationToken cancellationToken)
    {
        // Fast path: connection already exists and is open
        if (_connection?.IsOpen == true)
            return;

        // Slow path: acquire lock and create connection
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check pattern: another thread may have created connection
            if (_connection?.IsOpen == true)
                return;

            // Dispose old connection if exists
            _connection?.Dispose();

            _logger.LogInformation(
                "Establishing RabbitMQ connection - Host: {Host}:{Port}",
                _connectionFactory.HostName, _connectionFactory.Port);

            // v7.0.0 API: CreateConnectionAsync
            _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

            _logger.LogInformation(
                "RabbitMQ connection established - ConnectionId: {ConnectionId}",
                _connection.ClientProvidedName ?? "default");
        }
        catch (BrokerUnreachableException ex)
        {
            _logger.LogError(ex,
                "Failed to establish RabbitMQ connection - Host: {Host}:{Port}",
                _connectionFactory.HostName, _connectionFactory.Port);
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Disconnects from RabbitMQ (for recovery scenarios).
    /// </summary>
    private async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
                _logger.LogInformation("RabbitMQ connection disposed for recovery");
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Disposes RabbitMQ connection and resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _connection?.Dispose();
        _connectionLock.Dispose();
        _disposed = true;

        _logger.LogInformation("RabbitMQEventBus disposed");
    }
}