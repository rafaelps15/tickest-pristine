using System.IdentityModel.Tokens.Jwt;
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
    public async Task AssignRoles_Should_ReturnForbidden_WhenRoleGrantsAdministrativePermissionCallerLacks()
    {
        // Arrange: role que só atribui roles, dada a um usuário comum
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        Guid delegateRoleId = await CreateRoleWithPermissionsAsync("users:assign-roles");
        Guid privilegedRoleId = await CreateRoleWithPermissionsAsync("roles:manage");
        Guid ticketRoleId = await CreateRoleWithPermissionsAsync("tickets:manage");

        string delegateEmail = UniqueEmail();
        Guid delegateUserId = await RegisterUserAsync(delegateEmail);
        (await HttpClient.PutAsJsonAsync($"users/{delegateUserId}/roles", new { roleIds = new[] { delegateRoleId } }))
            .EnsureSuccessStatusCode();

        Guid targetUserId = await RegisterUserAsync(UniqueEmail());

        AccessTokens delegateTokens = await LoginAsync(delegateEmail);
        Authenticate(delegateTokens.AccessToken);

        // Act
        HttpResponseMessage escalation = await HttpClient.PutAsJsonAsync(
            $"users/{targetUserId}/roles",
            new { roleIds = new[] { privilegedRoleId } });

        HttpResponseMessage ticketOnly = await HttpClient.PutAsJsonAsync(
            $"users/{targetUserId}/roles",
            new { roleIds = new[] { ticketRoleId } });

        // Assert
        escalation.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        ticketOnly.StatusCode.ShouldBe(HttpStatusCode.NoContent);
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

    private async Task<Guid> CreateRoleWithPermissionsAsync(params string[] permissionCodes)
    {
        HttpResponseMessage createRoleResponse = await HttpClient.PostAsJsonAsync("roles", new { name = $"Role-{Guid.NewGuid():N}" });
        createRoleResponse.EnsureSuccessStatusCode();
        Guid roleId = await createRoleResponse.Content.ReadFromJsonAsync<Guid>();

        (await HttpClient.PutAsJsonAsync($"roles/{roleId}/permissions", new { permissionCodes }))
            .EnsureSuccessStatusCode();

        return roleId;
    }

    [Fact]
    public async Task AssignRoles_Should_ReturnConflict_WhenRemovingAdministratorRoleFromLastAdministrator()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        var adminUserId = Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(adminTokens.AccessToken).Subject);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"users/{adminUserId}/roles",
            new { roleIds = Array.Empty<Guid>() });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        HttpResponseMessage stillAdmin = await HttpClient.GetAsync("roles");
        stillAdmin.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
