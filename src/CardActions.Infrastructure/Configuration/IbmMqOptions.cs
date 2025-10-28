namespace CardActions.Infrastructure.Configuration;

/// <summary>
/// IBM MQ-specific configuration options.
/// Follows pattern from RabbitMQOptions.cs for consistency.
/// </summary>
public class IbmMqOptions
{
    /// <summary>
    /// IBM MQ Queue Manager name (required).
    /// Example: "QM1", "DEV.QUEUE.MANAGER"
    /// </summary>
    public string QueueManager { get; set; } = "QM1";

    /// <summary>
    /// IBM MQ host (default: localhost for dev).
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// IBM MQ port (default: 1414 for MQ).
    /// </summary>
    public int Port { get; set; } = 1414;

    /// <summary>
    /// IBM MQ channel name (required for client connections).
    /// Example: "DEV.APP.SVRCONN"
    /// </summary>
    public string Channel { get; set; } = "DEV.APP.SVRCONN";

    /// <summary>
    /// Target queue name for publishing events.
    /// Example: "DEV.QUEUE.1", "CARDACTIONS.EVENTS"
    /// </summary>
    public string QueueName { get; set; } = "DEV.QUEUE.1";

    /// <summary>
    /// Username for authentication (optional - can be empty for dev).
    /// Should come from secrets in production.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for authentication (optional - can be empty for dev).
    /// MUST come from secrets vault in production.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Connection timeout in seconds (default: 30).
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Enable SSL/TLS connection (default: false for dev).
    /// </summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// SSL cipher spec (required if UseSsl = true).
    /// Example: "TLS_RSA_WITH_AES_256_CBC_SHA256"
    /// </summary>
    public string? SslCipherSpec { get; set; }

    /// <summary>
    /// Validates IBM MQ configuration.
    /// Throws InvalidOperationException if configuration is invalid.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(QueueManager))
            throw new InvalidOperationException("EventBus:IbmMQ:QueueManager is required.");

        if (string.IsNullOrWhiteSpace(Host))
            throw new InvalidOperationException("EventBus:IbmMQ:Host is required.");

        if (Port <= 0 || Port > 65535)
            throw new InvalidOperationException($"EventBus:IbmMQ:Port must be between 1 and 65535 (got {Port}).");

        if (string.IsNullOrWhiteSpace(Channel))
            throw new InvalidOperationException("EventBus:IbmMQ:Channel is required.");

        if (string.IsNullOrWhiteSpace(QueueName))
            throw new InvalidOperationException("EventBus:IbmMQ:QueueName is required.");

        if (UseSsl && string.IsNullOrWhiteSpace(SslCipherSpec))
            throw new InvalidOperationException("EventBus:IbmMQ:SslCipherSpec is required when UseSsl=true.");
    }
}