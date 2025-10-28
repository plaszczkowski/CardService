namespace CardActions.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed wrapper for IBM MQ queue name.
/// Implements Value Object pattern from Domain-Driven Design.
/// Prevents DI ambiguity when multiple string services are registered.
/// </summary>
/// <remarks>
/// This wrapper ensures type safety and makes the code self-documenting.
/// The implicit conversion operator allows seamless usage as string.
/// </remarks>
public sealed record IbmMqQueueName
{
    /// <summary>
    /// Gets the queue name value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of IbmMqQueueName.
    /// </summary>
    /// <param name="value">Queue name (e.g., "DEV.QUEUE.1", "CARDACTIONS.EVENTS")</param>
    /// <exception cref="ArgumentException">Thrown if value is null or whitespace</exception>
    public IbmMqQueueName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Queue name cannot be null or whitespace", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Implicit conversion to string for convenience.
    /// Allows usage: string queueName = ibmMqQueueName;
    /// </summary>
    public static implicit operator string(IbmMqQueueName queueName)
        => queueName?.Value ?? throw new ArgumentNullException(nameof(queueName));

    /// <summary>
    /// Returns the queue name value.
    /// </summary>
    public override string ToString() => Value;
}