using System.Net;
using System.Net.Http.Json;

namespace TickestPristine.IntegrationTests.Permissions;

public sealed class PermissionsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task GetAll_Should_ReturnUnauthorized_WhenNoTokenProvided()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("permissions");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_Should_ReturnPermissionCatalogWithDisplayData_WhenCallerIsAuthenticated()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("permissions");

        // Assert
        response.EnsureSuccessStatusCode();
        List<PermissionDto>? permissions = await response.Content.ReadFromJsonAsync<List<PermissionDto>>();
        permissions!.Select(p => p.Code).ShouldContain("tickets:create");

        PermissionDto assignRoles = permissions!.Single(p => p.Code == "users:assign-roles");
        assignRoles.Name.ShouldBe("Alterar funções de colaboradores");
        assignRoles.Description.ShouldNotBeNullOrWhiteSpace();
        assignRoles.Group.ShouldBe("Usuários");
        assignRoles.IsAdministrative.ShouldBeTrue();
    }

    private sealed record PermissionDto(string Code, string Name, string Description, string Group, bool IsAdministrative);
}
