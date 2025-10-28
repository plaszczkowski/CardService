using System;
using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace CardActions.Infrastructure.Configuration;

/// <summary>
/// Configuration options for event bus selection and RabbitMQ connection.
/// </summary>
public class EventBusOptions
{
    public const string SectionName = "EventBus";

    /// <summary>
    /// Event bus provider: "InMemory", "RabbitMQ", or "IbmMQ".
    /// Default: InMemory (for safety).
    /// </summary>
    public string Provider { get; set; } = "InMemory";

    /// <summary>
    /// DEPRECATED: Use Provider="InMemory" instead.
    /// Kept for backward compatibility.
    /// </summary>
    [Obsolete("Use Provider property instead")]
    public bool UseInMemory { get; set; } = true;

    /// <summary>
    /// RabbitMQ connection configuration (used when Provider = "RabbitMQ").
    /// </summary>
    public RabbitMQOptions? RabbitMQ { get; set; }

    /// <summary>
    /// IBM MQ connection configuration (used when Provider = "IbmMQ").
    /// </summary>
    public IbmMqOptions? IbmMQ { get; set; }

    /// <summary>
    /// Gets or sets the optional <see cref="JsonSerializerOptions"/> used to configure JSON serialization behavior.
    /// If not set, default serialization settings will apply.
    /// </summary>
    public JsonSerializerOptions? SerializerOptions { get; set; }

    /// <summary>
    /// Validates configuration based on selected provider.
    /// </summary>
    public void Validate(IHostEnvironment environment)
    {
        // Normalize provider name (case-insensitive)
        var provider = Provider.ToLowerInvariant();

        switch (provider)
        {
            case "inmemory":
                // No validation needed for in-memory
                break;

            case "rabbitmq":
                if (RabbitMQ == null)
                    throw new InvalidOperationException("EventBus:Provider is 'RabbitMQ' but EventBus:RabbitMQ configuration is missing.");

                RabbitMQ.Validate();

                if (environment.IsProduction())
                {
                    if (RabbitMQ.Password == "guest" || RabbitMQ.Password == "PLACEHOLDER_OVERRIDE_IN_PROD")
                        throw new InvalidOperationException("Production requires secure RabbitMQ password.");
                }
                break;

            case "ibmmq":
                if (IbmMQ == null)
                    throw new InvalidOperationException("EventBus:Provider is 'IbmMQ' but EventBus:IbmMQ configuration is missing.");

                IbmMQ.Validate();

                if (environment.IsProduction())
                {
                    if (string.IsNullOrEmpty(IbmMQ.Password))
                        throw new InvalidOperationException("Production requires IBM MQ password.");
                }
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown EventBus:Provider value: '{Provider}'. " +
                    "Valid values: 'InMemory', 'RabbitMQ', 'IbmMQ'.");
        }

        // Handle deprecated UseInMemory (backward compatibility)
#pragma warning disable CS0618 // Type or member is obsolete
        if (UseInMemory && provider != "inmemory")
        {
            throw new InvalidOperationException(
                "Conflicting configuration: UseInMemory=true but Provider != 'InMemory'. " +
                "Please use Provider property only.");
        }
#pragma warning restore CS0618
    }
}