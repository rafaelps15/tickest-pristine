using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace TickestPristine.Application.UnitTests.Abstractions;

public abstract class BaseHandlerTest
{
    protected static TestDbContext CreateDbContext()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"tickestpristine-{Guid.NewGuid()}")
            .Options;

        return new TestDbContext(options);
    }

    protected static HybridCache CreateCache()
    {
        var services = new ServiceCollection();

#pragma warning disable EXTEXP0018
        services.AddHybridCache();
#pragma warning restore EXTEXP0018

        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    /// <summary>
    /// Usuário válido para os testes, com e-mail único; os demais campos podem ser ajustados depois de criado.
    /// </summary>
    protected static User CreateUser(bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Email = $"{Guid.NewGuid():N}@example.com",
        FirstName = "Test",
        LastName = "User",
        Code = $"usr_{Ulid.NewUlid()}",
        IsActive = isActive,
        CreatedAtUtc = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc)
    };
}
