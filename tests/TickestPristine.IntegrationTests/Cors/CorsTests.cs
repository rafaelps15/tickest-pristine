namespace TickestPristine.IntegrationTests.Cors;

/// <summary>
/// Valida a política de CORS com "Cors:AllowLoopbackOrigins" ligado: front local em qualquer porta é aceito, origem
/// externa não.
/// </summary>
public sealed class CorsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
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
