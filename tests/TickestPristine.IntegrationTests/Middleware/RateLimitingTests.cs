using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TickestPristine.IntegrationTests.Tickets;

namespace TickestPristine.IntegrationTests.Middleware;

public sealed class RateLimitingTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private readonly IntegrationTestWebAppFactory _factory = factory;

    [Fact]
    public async Task Request_Should_ReturnTooManyRequestsWithRetryAfter_When_InvalidTokenExceedsLimit()
    {
        // Arrange: limite global de 2 requisições e um token inválido, que a autorização recusaria
        using WebApplicationFactoryWithSetting limited = new(_factory, "RateLimiting:Global:PermitLimit", "2");
        using HttpClient client = limited.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token-invalido");

        // Act
        using HttpResponseMessage first = await client.GetAsync("departments");
        using HttpResponseMessage second = await client.GetAsync("departments");
        using HttpResponseMessage third = await client.GetAsync("departments");

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        second.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        third.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        third.Headers.RetryAfter.ShouldNotBeNull();
        ProblemDto? problem = await third.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Code.ShouldBe("General.TooManyRequests");
        problem.Title.ShouldBe("Muitas requisições");
    }

    [Fact]
    public async Task Requests_Should_HaveSeparateLimits_When_TwoUsersShareTheSameIp()
    {
        // Arrange: limite global de 2 requisições por partição, com dois usuários no mesmo IP
        (_, AccessTokens first) = await RegisterAndLoginAsync();
        (_, AccessTokens second) = await RegisterAndLoginAsync();
        using WebApplicationFactoryWithSetting limited = new(_factory, "RateLimiting:Global:PermitLimit", "2");
        using HttpClient client = limited.CreateClient();

        // Act
        HttpStatusCode[] firstUser = [await GetDepartmentsAsync(client, first), await GetDepartmentsAsync(client, first)];
        HttpStatusCode[] secondUser = [await GetDepartmentsAsync(client, second), await GetDepartmentsAsync(client, second)];
        HttpStatusCode firstUserThird = await GetDepartmentsAsync(client, first);

        // Assert
        firstUser.ShouldAllBe(status => status == HttpStatusCode.OK);
        secondUser.ShouldAllBe(status => status == HttpStatusCode.OK);
        firstUserThird.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Login_Should_ReturnProblemWithRetryAfter_WhenAuthenticationLimitIsExceeded()
    {
        // Arrange: limite de autenticação de 1 tentativa
        using WebApplicationFactoryWithSetting limited = new(_factory, "RateLimiting:Authentication:PermitLimit", "1");
        using HttpClient client = limited.CreateClient();
        var request = new { email = UniqueEmail(), password = "Senha@123" };

        // Act
        using HttpResponseMessage first = await client.PostAsJsonAsync("users/login", request);
        using HttpResponseMessage second = await client.PostAsJsonAsync("users/login", request);

        // Assert
        first.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        second.Headers.RetryAfter.ShouldNotBeNull();
        (await second.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe("Muitas requisições");
    }

    private static async Task<HttpStatusCode> GetDepartmentsAsync(HttpClient client, AccessTokens tokens)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "departments");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using HttpResponseMessage response = await client.SendAsync(request);

        return response.StatusCode;
    }

    private sealed record ProblemDto(int Status, string Title, string? Detail, string? Code);
}
