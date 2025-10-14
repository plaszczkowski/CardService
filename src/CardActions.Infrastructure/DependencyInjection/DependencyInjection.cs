using CardActions.Application.Interfaces;
using CardActions.Domain.Abstractions;
using CardActions.Infrastructure.Data;
using CardActions.Infrastructure.EventBus;
using Microsoft.Extensions.DependencyInjection;

namespace CardActions.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        bool useInMemoryEventBus = true)
    {
        services.AddScoped<ICardRepository, CardRepository>();

        if (useInMemoryEventBus)
        {
            services.AddSingleton<IEventBus, InMemoryEventBus>();
        }
        else
        {
            services.AddSingleton<IEventBus, RabbitMQEventBus>();
        }

        return services;
    }
}