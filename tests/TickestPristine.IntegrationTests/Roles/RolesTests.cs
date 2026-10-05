using System.Net;
using System.Net.Http.Json;
using TickestPristine.Domain.Roles;

namespace TickestPristine.IntegrationTests.Roles;

public sealed class RolesTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private static readonly string[] AssignedPermissionCodes = ["tickets:create", "tickets:view-own"];
    private static readonly string[] RoleManagerPermissionCodes = ["roles:manage", "roles:read"];
    private static readonly string[] EscalationPermissionCodes = ["users:assign-roles"];
    private static readonly string[] TicketOnlyPermissionCodes = ["tickets:manage"];

    private async Task AuthenticateAsAdminAsync()
    {
        AccessTokens tokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(tokens.AccessToken);
    }

    [Fact]
    public async Task GetAll_Should_ReturnUnauthorized_WhenNoTokenProvided()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("roles");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AssignPermissions_Should_ReflectAssignedPermissionsInGetAll()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string roleName = await CreateTestRoleAsync();

        // Act
        HttpResponseMessage assignResponse = await HttpClient.PutAsJsonAsync(
            $"roles/{roleName}/permissions",
            new { permissionCodes = AssignedPermissionCodes });

        // Assert
        assignResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        List<RoleDto>? roles = await HttpClient.GetFromJsonAsync<List<RoleDto>>("roles");
        RoleDto role = roles!.Single(r => r.Name == roleName);
        role.PermissionCodes.ShouldBe(["tickets:create", "tickets:view-own"], ignoreOrder: true);
    }

    [Fact]
    public async Task AssignPermissions_Should_ReturnNotFound_WhenRoleDoesNotExist()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"roles/Role-{Guid.NewGuid():N}/permissions",
            new { permissionCodes = AssignedPermissionCodes });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAll_Should_FlagExactlyOneDefaultRoleAndOneAdministratorRole()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("roles");

        // Assert
        response.EnsureSuccessStatusCode();
        List<RoleDto>? roles = await response.Content.ReadFromJsonAsync<List<RoleDto>>();

        roles!.Where(r => r.IsDefault).ShouldHaveSingleItem().Name.ShouldBe(Role.Collaborator.Name);

        RoleDto administrator = roles!.Where(r => r.IsAdministrator).ShouldHaveSingleItem();
        administrator.Name.ShouldBe(Role.Administrator.Name);
        administrator.PermissionCodes.ShouldContain("users:assign-roles");
    }

    [Fact]
    public async Task AssignPermissions_Should_ReturnConflict_WhenRoleIsAdministrator()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"roles/{Role.Administrator.Name}/permissions",
            new { permissionCodes = AssignedPermissionCodes });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        List<RoleDto>? rolesAfter = await HttpClient.GetFromJsonAsync<List<RoleDto>>("roles");
        rolesAfter!.Single(r => r.IsAdministrator).PermissionCodes.ShouldContain("users:assign-roles");
    }

    [Fact]
    public async Task AssignPermissions_Should_ReturnForbidden_WhenGrantingAdministrativePermissionCallerLacks()
    {
        // Arrange: role que só gerencia funções, atribuída a um colaborador
        await AuthenticateAsAdminAsync();

        string roleManagerRoleName = await CreateTestRoleAsync();
        (await HttpClient.PutAsJsonAsync(
                $"roles/{roleManagerRoleName}/permissions",
                new { permissionCodes = RoleManagerPermissionCodes }))
            .EnsureSuccessStatusCode();

        string targetRoleName = await CreateTestRoleAsync();

        string roleManagerEmail = UniqueEmail();
        Guid roleManagerUserId = await RegisterUserAsync(roleManagerEmail);
        (await HttpClient.PutAsJsonAsync($"users/{roleManagerUserId}/roles", new { roleNames = new[] { roleManagerRoleName } }))
            .EnsureSuccessStatusCode();

        AccessTokens roleManagerTokens = await LoginAsync(roleManagerEmail);
        Authenticate(roleManagerTokens.AccessToken);

        // Act
        HttpResponseMessage escalation = await HttpClient.PutAsJsonAsync(
            $"roles/{targetRoleName}/permissions",
            new { permissionCodes = EscalationPermissionCodes });

        HttpResponseMessage ticketOnly = await HttpClient.PutAsJsonAsync(
            $"roles/{targetRoleName}/permissions",
            new { permissionCodes = TicketOnlyPermissionCodes });

        // Assert
        escalation.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        ticketOnly.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private sealed record RoleDto(string Name, bool IsDefault, bool IsAdministrator, List<string> PermissionCodes);
}
