using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TickestPristine.Infrastructure.Health;

// Só com o Redis configurado: lê uma chave pelo IDistributedCache que o HybridCache usa como segundo nível. O Redis
// inalcançável lança, e o serviço de health checks marca Unhealthy.
internal sealed class DistributedCacheHealthCheck(IDistributedCache cache) : IHealthCheck
{
    private const string ProbeKey = "tickest:health-probe";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await cache.GetAsync(ProbeKey, cancellationToken);

        return HealthCheckResult.Healthy();
    }
}
