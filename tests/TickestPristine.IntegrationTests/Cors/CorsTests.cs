using Microsoft.Extensions.Options;
using TickestPristine.IntegrationTests.Tickets;

namespace TickestPristine.IntegrationTests.Cors;

/// <summary>
/// Valida a política de CORS com "Cors:AllowLoopbackOrigins" ligado: front local em qualquer porta e as origens de
/// "Cors:AllowedOrigins" são aceitos; qualquer outra origem externa não.
/// </summary>
public sealed class CorsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private readonly IntegrationTestWebAppFactory _factory = factory;

    [Fact]
    public void Startup_Should_Fail_When_AllowedOriginHasNoScheme()
    {
        // Arrange
        using WebApplicationFactoryWithSetting invalidFactory = new(_factory, "Cors:AllowedOrigins:0", "app.example.com");

        // Act
        Exception exception = Should.Throw<Exception>(() => invalidFactory.CreateClient());

        // Assert
        Exception? current = exception;

        while (current is not null and not OptionsValidationException)
        {
            current = current.InnerException;
        }

        current.ShouldBeOfType<OptionsValidationException>().Message.ShouldContain("Cors:AllowedOrigins");
    }

    [Fact]
    public async Task Startup_Should_Succeed_When_AllowedOriginIsAbsoluteHttps()
    {
        // Arrange: a fábrica dos testes configura "https://app.example.com/", com a barra final
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("health");

        // Assert
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("http://localhost:55343")]
    [InlineData("http://127.0.0.1:4200")]
    public async Task Preflight_Should_AllowOrigin_WhenOriginIsLoopback(string origin)
    {
        // Arrange
        using HttpRequestMessage request = CreatePreflightRequest(origin);

        // Act
        HttpResponseMessage response = await HttpClient.SendAsync(request);

        // Assert
        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe([origin]);
    }

    [Fact]
    public async Task Preflight_Should_AllowOrigin_WhenOriginIsConfiguredWithTrailingSlash()
    {
        // Arrange
        using HttpRequestMessage request = CreatePreflightRequest("https://app.example.com");

        // Act
        HttpResponseMessage response = await HttpClient.SendAsync(request);

        // Assert
        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe(["https://app.example.com"]);
    }

    [Fact]
    public async Task Preflight_Should_NotAllowOrigin_WhenOriginIsExternal()
    {
        // Arrange
        using HttpRequestMessage request = CreatePreflightRequest("https://example.com");

        // Act
        HttpResponseMessage response = await HttpClient.SendAsync(request);

        // Assert
        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    private static HttpRequestMessage CreatePreflightRequest(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "users/login");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        return request;
    }
}
