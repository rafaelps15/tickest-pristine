using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.IntegrationTests.Database;

/// <summary>
/// Valida que as migrations acompanham o modelo do EF Core, inclusive os dados de HasData (roles e o catálogo de
/// permissões do Administrador).
/// </summary>
public sealed class MigrationsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public void Model_Should_HaveNoPendingChanges()
    {
        // Arrange
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act
        bool hasPendingModelChanges = context.Database.HasPendingModelChanges();

        // Assert: falha quando o modelo muda sem "dotnet ef migrations add"
        hasPendingModelChanges.ShouldBeFalse();
    }
}
