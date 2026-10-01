using System.Net;
using System.Net.Http.Json;

namespace TickestPristine.IntegrationTests.Roles;

public sealed class RolesTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string AdminEmail = "admin@tickestpristine.dev";
    private const string AdminPassword = "ChangeMe123!";

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

        roles!.Where(r => r.IsDefault).ShouldHaveSingleItem().Name.ShouldBe("Requester");

        RoleDto administrator = roles!.Where(r => r.IsAdministrator).ShouldHaveSingleItem();
        administrator.Name.ShouldBe("Admin");
        administrator.PermissionCodes.ShouldContain("users:assign-roles");
    }

    private sealed record RoleDto(Guid Id, string Name, bool IsDefault, bool IsAdministrator, List<string> PermissionCodes);

    [Fact]
    public async Task AssignPermissions_Should_ReturnConflict_WhenRoleIsAdministrator()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        List<RoleDto>? roles = await HttpClient.GetFromJsonAsync<List<RoleDto>>("roles");
        Guid administratorRoleId = roles!.Single(r => r.IsAdministrator).Id;

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"roles/{administratorRoleId}/permissions",
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

        Guid roleManagerRoleId = await CreateRoleAsync();
        (await HttpClient.PutAsJsonAsync(
                $"roles/{roleManagerRoleId}/permissions",
                new { permissionCodes = RoleManagerPermissionCodes }))
            .EnsureSuccessStatusCode();

        Guid targetRoleId = await CreateRoleAsync();

        string roleManagerEmail = UniqueEmail();
        Guid roleManagerUserId = await RegisterUserAsync(roleManagerEmail);
        (await HttpClient.PutAsJsonAsync($"users/{roleManagerUserId}/roles", new { roleIds = new[] { roleManagerRoleId } }))
            .EnsureSuccessStatusCode();

        AccessTokens roleManagerTokens = await LoginAsync(roleManagerEmail);
        Authenticate(roleManagerTokens.AccessToken);

        // Act
        HttpResponseMessage escalation = await HttpClient.PutAsJsonAsync(
            $"roles/{targetRoleId}/permissions",
            new { permissionCodes = EscalationPermissionCodes });

        HttpResponseMessage ticketOnly = await HttpClient.PutAsJsonAsync(
            $"roles/{targetRoleId}/permissions",
            new { permissionCodes = TicketOnlyPermissionCodes });

        // Assert
        escalation.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        ticketOnly.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task<Guid> CreateRoleAsync()
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("roles", new { name = $"Role-{Guid.NewGuid():N}" });
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }
}
