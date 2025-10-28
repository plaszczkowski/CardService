namespace CardActions.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed wrapper for RabbitMQ exchange name.
/// Implements Value Object pattern from Domain-Driven Design.
/// Prevents DI ambiguity when multiple string services are registered.
/// </summary>
/// <remarks>
/// This wrapper ensures type safety and makes the code self-documenting.
/// The implicit conversion operator allows seamless usage as string.
/// </remarks>
public sealed record RabbitMQExchangeName
{
    /// <summary>
    /// Gets the exchange name value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of RabbitMQExchangeName.
    /// </summary>
    /// <param name="value">Exchange name (e.g., "cardactions.events")</param>
    /// <exception cref="ArgumentException">Thrown if value is null or whitespace</exception>
    public RabbitMQExchangeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Exchange name cannot be null or whitespace", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Implicit conversion to string for convenience.
    /// Allows usage: string exchangeName = rabbitMQExchangeName;
    /// </summary>
    public static implicit operator string(RabbitMQExchangeName exchangeName)
        => exchangeName?.Value ?? throw new ArgumentNullException(nameof(exchangeName));

    /// <summary>
    /// Returns the exchange name value.
    /// </summary>
    public override string ToString() => Value;
}