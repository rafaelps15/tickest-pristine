namespace TickestPristine.Infrastructure.Health;

public static class HealthCheckTags
{
    // As checagens de prontidão, que o health/ready roda; o health/live não roda nenhuma, e o health, só a do PostgreSQL.
    public const string Ready = "ready";
}
