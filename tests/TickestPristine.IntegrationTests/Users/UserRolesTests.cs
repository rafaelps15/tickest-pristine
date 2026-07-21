using System.Net;
using System.Net.Http.Json;

namespace TickestPristine.IntegrationTests.Users;

public sealed class UserRolesTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string AdminEmail = "admin@tickestpristine.dev";
    private const string AdminPassword = "ChangeMe123!";

    [Fact]
    public async Task AssignRoles_Should_ReturnNoContent_WhenCallerIsAdminAndRolesExist()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        HttpResponseMessage createRoleResponse = await HttpClient.PostAsJsonAsync("roles", new { name = $"Role-{Guid.NewGuid():N}" });
        createRoleResponse.EnsureSuccessStatusCode();
        Guid roleId = await createRoleResponse.Content.ReadFromJsonAsync<Guid>();

        Guid userId = await RegisterUserAsync(UniqueEmail());

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"users/{userId}/roles",
            new { roleIds = new[] { roleId } });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AssignRoles_Should_ReturnProblem_WhenRoleDoesNotExist()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        Guid userId = await RegisterUserAsync(UniqueEmail());

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"users/{userId}/roles",
            new { roleIds = new[] { Guid.NewGuid() } });

        // Assert
        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    public async Task AssignRoles_Should_ReturnForbidden_WhenCallerLacksManageRolesPermission()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"users/{userId}/roles",
            new { roleIds = Array.Empty<Guid>() });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
