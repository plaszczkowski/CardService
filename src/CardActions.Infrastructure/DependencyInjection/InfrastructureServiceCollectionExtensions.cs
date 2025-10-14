using CardActions.Application.Interfaces;
using CardActions.Domain.Abstractions;
using CardActions.Infrastructure.Configuration;
using CardActions.Infrastructure.EventBus;
using Microsoft.Extensions.DependencyInjection;

namespace CardActions.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, EventBusOptions options)
    {
        if (options.UseInMemory)
        {
            services.AddSingleton<IEventBus, InMemoryEventBus>();
        }
        else
        {
            if (options.RabbitMQ is null)
                throw new ArgumentNullException(nameof(options), "RabbitMQ configuration must be provided when UseInMemory is false.");

            services.AddSingleton<IEventBus, RabbitMQEventBus>(); 
            services.Configure<RabbitMQOptions>(opts =>
            {
                opts.Host = options.RabbitMQ.Host;
                opts.Port = options.RabbitMQ.Port;
                // Add other RabbitMQ config bindings here
            });
        }

        return services;
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, bool useInMemory)
    {
        return AddInfrastructure(services, new EventBusOptions { UseInMemory = useInMemory });
    }
}