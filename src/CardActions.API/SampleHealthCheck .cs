using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CardActions.API;

public class SampleHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Tutaj możesz dodać prawdziwe checki (baza danych, external services, etc.)
        var isHealthy = true; // Przykład - zawsze zdrowy

        return Task.FromResult(
            isHealthy
                ? HealthCheckResult.Healthy("Card service is healthy")
                : HealthCheckResult.Unhealthy("Card service is unhealthy"));
    }
}