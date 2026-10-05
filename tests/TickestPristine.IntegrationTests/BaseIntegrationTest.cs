using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.IntegrationTests;

[Collection(nameof(IntegrationTestCollection))]
public abstract class BaseIntegrationTest
{
    protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
    {
        HttpClient = factory.CreateClient();
        Services = factory.Services;
    }

    protected const string AdminEmail = IntegrationTestWebAppFactory.AdminEmail;
    protected const string AdminPassword = IntegrationTestWebAppFactory.AdminPassword;

    protected HttpClient HttpClient { get; }

    protected IServiceProvider Services { get; }

    protected sealed record AccessTokens(string AccessToken, string RefreshToken);

    protected static string UniqueEmail() => $"test-{Guid.NewGuid():N}@example.com";

    protected async Task<Guid> RegisterUserAsync(string email)
    {
        var request = new
        {
            email,
            firstName = "Test",
            lastName = "User",
            password = "Password123!"
        };

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/register", request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    protected Task<AccessTokens> LoginAsync(string email) => LoginAsync(email, "Password123!");

    protected async Task<AccessTokens> LoginAsync(string email, string password)
    {
        var request = new { email, password };

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/login", request);
        response.EnsureSuccessStatusCode();

        AccessTokens? tokens = await response.Content.ReadFromJsonAsync<AccessTokens>();

        return tokens!;
    }

    protected async Task<(Guid UserId, AccessTokens Tokens)> RegisterAndLoginAsync()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);

        return (userId, tokens);
    }

    protected void Authenticate(string accessToken)
    {
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    /// <summary>
    /// Id do usuário dono do token (claim "sub").
    /// </summary>
    protected static Guid GetUserId(AccessTokens tokens) =>
        Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken).Subject);

    /// <summary>
    /// Cria uma role isolada para o teste: as roles do código são compartilhadas por todos os testes e não devem ser
    /// alteradas. A API não cria roles, então ela entra direto no banco.
    /// </summary>
    protected async Task<string> CreateTestRoleAsync()
    {
        string name = $"Role-{Guid.NewGuid():N}";

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.ExecuteSqlAsync($"INSERT INTO public.roles (name) VALUES ({name})");

        return name;
    }
}
