using System.Net;
using System.Net.Http.Json;

namespace TickestPristine.IntegrationTests.Permissions;

public sealed class PermissionsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task GetAll_Should_ReturnUnauthorized_WhenNoTokenProvided()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("permissions");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_Should_ReturnAllPermissionCodes_WhenCallerIsAuthenticated()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("permissions");

        // Assert
        response.EnsureSuccessStatusCode();
        List<string>? permissions = await response.Content.ReadFromJsonAsync<List<string>>();
        permissions!.ShouldContain("tickets:create");
        permissions!.ShouldContain("users:manage");
    }
}
