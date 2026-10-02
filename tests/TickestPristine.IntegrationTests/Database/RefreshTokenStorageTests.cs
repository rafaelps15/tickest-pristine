using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Domain.Users;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.IntegrationTests.Database;

/// <summary>
/// Valida que o banco guarda só o hash do refresh token, nunca o valor entregue ao cliente.
/// </summary>
public sealed class RefreshTokenStorageTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Login_Should_StoreOnlyRefreshTokenHash()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();

        // Act
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        RefreshToken stored = await context.RefreshTokens.AsNoTracking().SingleAsync(rt => rt.UserId == userId);

        // Assert
        stored.TokenHash.ShouldNotBe(tokens.RefreshToken);
        stored.TokenHash.Length.ShouldBe(64);
    }
}
