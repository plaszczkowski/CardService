using CardActions.Application.Interfaces;
using CardActions.Domain.Abstractions;
using CardActions.Infrastructure.Configuration;
using CardActions.Infrastructure.Data;
using CardActions.Infrastructure.EventBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using HealthChecks.RabbitMQ;
using Polly;
using Polly.Extensions.Http;

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
                break;

            case "rabbitmq":
                if (eventBusOptions.RabbitMQ == null)
                    throw new InvalidOperationException("RabbitMQ configuration is required when Provider='RabbitMQ'");

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

                // Register RabbitMQ health check
                services.AddHealthChecks()
                    .AddCheck(sp => new RabbitMQHealthCheck(
                        new ConnectionFactory
                        {
                            HostName = eventBusOptions.RabbitMQ.Host,
                            Port = eventBusOptions.RabbitMQ.Port,
                            UserName = eventBusOptions.RabbitMQ.Username,
                            Password = eventBusOptions.RabbitMQ.Password,
                            VirtualHost = eventBusOptions.RabbitMQ.VirtualHost,
                            AutomaticRecoveryEnabled = true
                        },
                        eventBusOptions.RabbitMQ.Exchange),
                        name: "rabbitmq",
                        failureStatus: HealthStatus.Degraded,
                        tags: new[] { "event-bus", "infrastructure" });
                break;

            case "ibmmq":
                if (eventBusOptions.IbmMQ == null)
                    throw new InvalidOperationException("IBM MQ configuration is required when Provider='IbmMQ'");

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

                // Register IBM MQ health check
                services.AddHealthChecks()
                    .AddCheck(sp => new IbmMqHealthCheck(eventBusOptions.IbmMQ),
                        name: "ibmmq",
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