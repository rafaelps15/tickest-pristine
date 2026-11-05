using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.IntegrationTests.Middleware;

public sealed class SecurityHeadersTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Theory]
    [InlineData("health")]
    [InlineData("users/me")]
    [InlineData("rota-inexistente-dos-cabecalhos")]
    public async Task Responses_Should_IncludeNoSniff_WhenAnyRouteIsCalled(string route)
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(route);

        // Assert
        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
    }

    [Fact]
    public async Task TokenResponses_Should_IncludeNoStore_WhenTokensAreIssued()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);

        // Act
        HttpResponseMessage login = await HttpClient.PostAsJsonAsync("users/login", new { email, password = "Password123!" });
        AccessTokens tokens = (await login.Content.ReadFromJsonAsync<AccessTokens>())!;

        HttpResponseMessage refresh = await HttpClient.PostAsJsonAsync("users/refresh-token", new { refreshToken = tokens.RefreshToken });

        await MarkPasswordAsProvisionalAsync(userId);
        HttpResponseMessage newPassword = await HttpClient.PostAsJsonAsync(
            "users/login/new-password",
            new { email, currentPassword = "Password123!", newPassword = "NewPassword123!" });

        // Assert
        foreach (HttpResponseMessage response in new[] { login, refresh, newPassword })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Headers.CacheControl.ShouldNotBeNull();
            response.Headers.CacheControl.NoStore.ShouldBeTrue();
        }
    }

    private async Task MarkPasswordAsProvisionalAsync(Guid userId)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.UserCredentials
            .Where(c => c.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.MustChangePassword, true));
    }
}
