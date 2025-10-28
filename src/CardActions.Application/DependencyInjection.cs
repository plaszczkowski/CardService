using CardActions.Application.Interfaces;
using CardActions.Domain.Abstractions;
using CardActions.Infrastructure.Configuration;
using CardActions.Infrastructure.Data;
using CardActions.Infrastructure.EventBus;
using CardActions.Infrastructure.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace CardActions.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        EventBusOptions eventBusOptions)
    {
        if (eventBusOptions == null)
            throw new ArgumentNullException(nameof(eventBusOptions));

        services.AddScoped<ICardRepository, CardRepository>();

        // Register event bus based on configuration
        var provider = eventBusOptions.Provider.ToLowerInvariant();

        switch (provider)
        {
            case "inmemory":
                services.AddSingleton<IEventBus, InMemoryEventBus>();
                // No health check for in-memory (always healthy)
                break;

            case "rabbitmq":
                if (eventBusOptions.RabbitMQ == null)
                    throw new InvalidOperationException(
                        "RabbitMQ configuration is required when Provider='RabbitMQ'.");

                // Register RabbitMQ Event Bus with Resilient decorator
                services.AddSingleton<IEventBus>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<RabbitMQEventBus>>();
                    var rabbitMQEventBus = new RabbitMQEventBus(
                        logger,
                        eventBusOptions.RabbitMQ.Host,
                        eventBusOptions.RabbitMQ.Port,
                        eventBusOptions.RabbitMQ.Username,
                        eventBusOptions.RabbitMQ.Password,
                        eventBusOptions.RabbitMQ.VirtualHost,
                        eventBusOptions.RabbitMQ.Exchange,
                        eventBusOptions
                    );

                    return new ResilientEventBus(rabbitMQEventBus, logger);
                });

                // Register RabbitMQ dependencies for health check (strongly-typed)
                services.AddSingleton(new RabbitMQExchangeName(eventBusOptions.RabbitMQ.Exchange));
                services.AddSingleton(sp => new ConnectionFactory
                {
                    HostName = eventBusOptions.RabbitMQ.Host,
                    Port = eventBusOptions.RabbitMQ.Port,
                    UserName = eventBusOptions.RabbitMQ.Username,
                    Password = eventBusOptions.RabbitMQ.Password,
                    VirtualHost = eventBusOptions.RabbitMQ.VirtualHost,
                    AutomaticRecoveryEnabled = true
                });

                // Register RabbitMQ Health Check (auto-resolve from DI)
                services.AddHealthChecks()
                    .AddCheck<RabbitMQHealthCheck>(
                        "rabbitmq",
                        failureStatus: HealthStatus.Degraded,
                        tags: new[] { "event-bus", "infrastructure" });
                break;

            case "ibmmq":
                if (eventBusOptions.IbmMQ == null)
                    throw new InvalidOperationException(
                        "IBM MQ configuration is required when Provider='IbmMQ'.");

                // Register IBM MQ Event Bus with Resilient decorator
                services.AddSingleton<IEventBus>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<IbmMqEventBus>>();
                    var ibmMqEventBus = new IbmMqEventBus(
                        logger,
                        eventBusOptions.IbmMQ,
                        eventBusOptions
                    );

                    return new ResilientEventBus(ibmMqEventBus, logger);
                });

                // Register IBM MQ dependencies for health check
                services.AddSingleton(eventBusOptions.IbmMQ);

                // Register IBM MQ Health Check (auto-resolve from DI)
                services.AddHealthChecks()
                    .AddCheck<IbmMqHealthCheck>(
                        "ibmmq",
                        failureStatus: HealthStatus.Degraded,
                        tags: new[] { "event-bus", "infrastructure" },
                        timeout: TimeSpan.FromSeconds(5));
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown EventBus Provider: '{eventBusOptions.Provider}'. " +
                    "Valid values: 'InMemory', 'RabbitMQ', 'IbmMQ'");
        }

        return services;
    }
}