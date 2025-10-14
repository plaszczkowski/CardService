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
        if (eventBusOptions.UseInMemory)
        {
            services.AddSingleton<IEventBus, InMemoryEventBus>();
        }
        else
        {
            services.AddSingleton<IEventBus>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<RabbitMQEventBus>>();
                if (eventBusOptions.RabbitMQ == null)
                {
                    throw new InvalidOperationException(
                        "RabbitMQ configuration is required when UseInMemory=false.");
                }

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
        }

        // Register health checks
        services.AddHealthChecks()
                 .AddCheck<RabbitMQHealthCheck>("rabbitmq",
                     failureStatus: HealthStatus.Degraded,
                     tags: new[] { "event-bus", "infrastructure" });

        return services;
    }
}