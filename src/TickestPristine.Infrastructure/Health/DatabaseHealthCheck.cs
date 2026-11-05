using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace TickestPristine.Infrastructure.Health;

// Roda um SELECT 1 pelo NpgsqlDataSource da aplicação, o mesmo pool das requisições. A exceção (banco fora do ar ou
// inexistente) vira Unhealthy pelo próprio serviço de health checks.
internal sealed class DatabaseHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand("SELECT 1");
        await command.ExecuteScalarAsync(cancellationToken);

        return HealthCheckResult.Healthy();
    }
}
