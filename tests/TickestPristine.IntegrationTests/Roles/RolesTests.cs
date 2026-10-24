using System.Net;
using System.Net.Http.Json;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.Roles;
using TickestPristine.Domain.Roles;

namespace TickestPristine.IntegrationTests.Roles;

public sealed class RolesTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private static readonly string[] AssignedPermissionCodes = ["tickets:create", "tickets:view-own"];
    private static readonly string[] RoleManagerPermissionCodes = ["roles:manage", "roles:read"];
    private static readonly string[] EscalationPermissionCodes = ["users:assign-roles"];
    private static readonly string[] TicketOnlyPermissionCodes = ["tickets:manage"];
    private static readonly string[] UnknownPermissionCodes = ["unknown:permission"];

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

        roles!.Where(r => r.IsDefault).ShouldHaveSingleItem().Name.ShouldBe(RoleNames.Collaborator);

        RoleDto administrator = roles!.Where(r => r.IsAdministrator).ShouldHaveSingleItem();
        administrator.Name.ShouldBe(RoleNames.Administrator);
        administrator.PermissionCodes.ShouldContain("users:assign-roles");
    }

    [Fact]
    public async Task AssignPermissions_Should_ReturnConflict_WhenRoleIsAdministrator()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"roles/{RoleNames.Administrator}/permissions",
            new { permissionCodes = AssignedPermissionCodes });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        List<RoleDto>? rolesAfter = await HttpClient.GetFromJsonAsync<List<RoleDto>>("roles");
        rolesAfter!.Single(r => r.IsAdministrator).PermissionCodes.ShouldContain("users:assign-roles");
    }

    [Fact]
    public async Task AssignPermissions_Should_ReturnForbidden_WhenGrantingAdministrativePermissionCallerLacks()
    {
        // Arrange
        string targetRoleName = await CreateTargetRoleAndAuthenticateAsRoleManagerAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"roles/{targetRoleName}/permissions",
            new { permissionCodes = EscalationPermissionCodes });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Roles.PrivilegeEscalation");
    }

    [Fact]
    public async Task AssignPermissions_Should_Succeed_WhenChangingOnlyNonAdministrativePermissions()
    {
        // Arrange
        string targetRoleName = await CreateTargetRoleAndAuthenticateAsRoleManagerAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"roles/{targetRoleName}/permissions",
            new { permissionCodes = TicketOnlyPermissionCodes });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        List<RoleDto>? roles = await HttpClient.GetFromJsonAsync<List<RoleDto>>("roles");
        roles!.Single(r => r.Name == targetRoleName).PermissionCodes.ShouldBe(TicketOnlyPermissionCodes, ignoreOrder: true);
    }

    [Fact]
    public async Task AssignPermissions_Should_ReturnBadRequest_WhenPermissionCodeIsUnknown()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string roleName = await CreateTestRoleAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"roles/{roleName}/permissions",
            new { permissionCodes = UnknownPermissionCodes });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        List<RoleDto>? roles = await HttpClient.GetFromJsonAsync<List<RoleDto>>("roles");
        roles!.Single(r => r.Name == roleName).PermissionCodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task AssignPermissions_Should_ReturnForbidden_WhenCallerLacksRolesManagePermission()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string roleName = await CreateTestRoleAsync();

        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"roles/{roleName}/permissions",
            new { permissionCodes = AssignedPermissionCodes });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAll_Should_ReturnForbidden_WhenCallerLacksRolesReadPermission()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("roles");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Cria uma role de teste vazia e autentica como um colaborador cuja role só gerencia e lê funções.
    /// </summary>
    private async Task<string> CreateTargetRoleAndAuthenticateAsRoleManagerAsync()
    {
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

        return targetRoleName;
    }

    private sealed record RoleDto(string Name, bool IsDefault, bool IsAdministrator, List<string> PermissionCodes);

    private sealed record ProblemDto(int Status, string Title, string Detail);
}
