using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Domain.Users;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.IntegrationTests.Database;

/// <summary>
/// Valida que a coluna is_active, que tem valor padrão true no banco, grava exatamente o valor informado.
/// </summary>
public sealed class UserIsActiveTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveChanges_Should_PersistIsActiveAsInformed_WhenUserIsInserted(bool isActive)
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = UniqueEmail(),
            FirstName = "Test",
            LastName = "User",
            Code = $"usr_{Ulid.NewUlid()}",
            IsActive = isActive
        };

        // Act
        using (IServiceScope writeScope = Services.CreateScope())
        {
            ApplicationDbContext writeContext = writeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            writeContext.Users.Add(user);
            await writeContext.SaveChangesAsync();
        }

        // Assert
        using IServiceScope readScope = Services.CreateScope();
        ApplicationDbContext readContext = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        User stored = await readContext.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        stored.IsActive.ShouldBe(isActive);
    }
}
