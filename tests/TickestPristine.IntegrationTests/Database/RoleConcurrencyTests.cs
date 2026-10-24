using TickestPristine.Application.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Domain.Roles;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.IntegrationTests.Database;

/// <summary>
/// Valida que a coluna Version da role impede gravações concorrentes.
/// </summary>
public sealed class RoleConcurrencyTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task SaveChanges_Should_ThrowConcurrencyException_WhenBothContextsReadTheSameRoleVersion()
    {
        // Arrange
        // Dois contextos leem a role de administrador na mesma versão.
        using IServiceScope firstScope = Services.CreateScope();
        using IServiceScope secondScope = Services.CreateScope();
        ApplicationDbContext firstContext = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationDbContext secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Role firstCopy = await firstContext.Roles.SingleAsync(r => r.Name == RoleNames.Administrator);
        Role secondCopy = await secondContext.Roles.SingleAsync(r => r.Name == RoleNames.Administrator);

        firstContext.Entry(firstCopy).Property<int>("Version").CurrentValue++;
        secondContext.Entry(secondCopy).Property<int>("Version").CurrentValue++;

        // Act
        await firstContext.SaveChangesAsync();
        Func<Task> secondSave = () => secondContext.SaveChangesAsync();

        // Assert
        await secondSave.ShouldThrowAsync<DbUpdateConcurrencyException>();
    }
}
