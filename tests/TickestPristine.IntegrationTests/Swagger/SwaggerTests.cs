using System.Net;
using System.Text.Json;

namespace TickestPristine.IntegrationTests.Swagger;

public sealed class SwaggerTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task GetDocument_Should_RequireTokenOnlyOnProtectedEndpoints()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("swagger/v1/swagger.json");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement paths = document.RootElement.GetProperty("paths");

        JsonElement login = paths.GetProperty("/users/login").GetProperty("post");
        login.TryGetProperty("security", out _).ShouldBeFalse();

        JsonElement getUsers = paths.GetProperty("/users").GetProperty("get");
        getUsers.GetProperty("security")[0].TryGetProperty("Bearer", out _).ShouldBeTrue();
    }
}
