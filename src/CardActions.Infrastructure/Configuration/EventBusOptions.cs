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
    /// Determines whether to use in-memory event bus (dev/test) or RabbitMQ (production).
    /// Default: true (in-memory) for safety.
    /// </summary>
    public bool UseInMemory { get; set; } = true;

    /// <summary>
    /// RabbitMQ connection configuration (only used when UseInMemory = false).
    /// </summary>
    public RabbitMQOptions? RabbitMQ { get; set; }

    /// <summary>
    /// Gets or sets the optional <see cref="JsonSerializerOptions"/> used to configure JSON serialization behavior.
    /// If not set, default serialization settings will apply.
    /// </summary>
    public JsonSerializerOptions? SerializerOptions { get; set; }

    /// <summary>
    /// Validates configuration and throws if RabbitMQ is required but not configured.
    /// </summary>
    public void Validate(IHostEnvironment environment)
    {
        if (!UseInMemory && RabbitMQ == null)
        {
            throw new InvalidOperationException(
                "EventBus:UseInMemory is false but EventBus:RabbitMQ configuration is missing. " +
                "Either set UseInMemory=true or provide RabbitMQ connection details.");
        }

        if (!UseInMemory)
        {
            RabbitMQ?.Validate();

            if (environment.IsProduction())
            {
                if (RabbitMQ!.Password == "guest" || RabbitMQ.Password == "PLACEHOLDER_OVERRIDE_IN_PROD")
                    throw new InvalidOperationException("Production requires secure RabbitMQ password");
            }
        }
    }
}