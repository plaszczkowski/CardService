using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace CardActions.Infrastructure.HealthChecks;

/// <summary>
/// RabbitMQ health check compatible with v7.0.0 API.
/// Breaking change: CreateModel() → CreateChannelAsync().
/// </summary>
public class RabbitMQHealthCheck : IHealthCheck
{
    private readonly ConnectionFactory _connectionFactory;
    private readonly string _exchange;

    public RabbitMQHealthCheck(ConnectionFactory connectionFactory, string exchange)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // ✅ v7.0.0 API: CreateConnectionAsync
            await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

            if (!connection.IsOpen)
            {
                return HealthCheckResult.Unhealthy("RabbitMQ connection is not open");
            }

            // ✅ v7.0.0 API: CreateChannelAsync
            await using var channel = await connection.CreateChannelAsync(null, cancellationToken);

            if (!channel.IsOpen)
            {
                return HealthCheckResult.Unhealthy("RabbitMQ channel creation failed");
            }

            // ✅ v7.0.0 API: ExchangeDeclarePassiveAsync
            await channel.ExchangeDeclarePassiveAsync(_exchange, cancellationToken);

            return HealthCheckResult.Healthy("RabbitMQ connection and exchange verified");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ connection failed", ex);
        }
    }
}