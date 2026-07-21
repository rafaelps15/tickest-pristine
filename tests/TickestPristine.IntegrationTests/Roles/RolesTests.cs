using System.Net;
using System.Net.Http.Json;

namespace TickestPristine.IntegrationTests.Roles;

public sealed class RolesTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string AdminEmail = "admin@tickestpristine.dev";
    private const string AdminPassword = "ChangeMe123!";

    private static readonly string[] AssignedPermissionCodes = ["tickets:create", "tickets:view-own"];

    private async Task AuthenticateAsAdminAsync()
    {
        AccessTokens tokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(tokens.AccessToken);
    }

    [Fact]
    public async Task Create_Should_ReturnRoleId_WhenCallerIsAdmin()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("roles", new { name = $"Role-{Guid.NewGuid():N}" });

        // Assert
        response.EnsureSuccessStatusCode();
        Guid roleId = await response.Content.ReadFromJsonAsync<Guid>();
        roleId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Create_Should_ReturnForbidden_WhenCallerLacksRolesManagePermission()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("roles", new { name = $"Role-{Guid.NewGuid():N}" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignPermissions_Then_GetAll_Should_ReflectAssignedPermissions()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync("roles", new { name = $"Role-{Guid.NewGuid():N}" });
        createResponse.EnsureSuccessStatusCode();
        Guid roleId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        // Act
        HttpResponseMessage assignResponse = await HttpClient.PutAsJsonAsync(
            $"roles/{roleId}/permissions",
            new { permissionCodes = AssignedPermissionCodes });

        // Assert
        assignResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage getAllResponse = await HttpClient.GetAsync("roles");
        getAllResponse.EnsureSuccessStatusCode();
        List<RoleDto>? roles = await getAllResponse.Content.ReadFromJsonAsync<List<RoleDto>>();

        RoleDto role = roles!.Single(r => r.Id == roleId);
        role.PermissionCodes.ShouldBe(["tickets:create", "tickets:view-own"], ignoreOrder: true);
    }

    private sealed record RoleDto(Guid Id, string Name, List<string> PermissionCodes);
}
