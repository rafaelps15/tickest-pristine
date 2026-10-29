using System.Net;
using System.Net.Http.Json;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Domain.Audit;
using TickestPristine.Domain.Roles;
using TickestPristine.Infrastructure.Database;

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

    [Fact]
    public async Task GetAll_Should_ReturnIdSystemFlagAndUserCounts_WhenRoleHasActivePendingAndInactiveUsers()
    {
        // Arrange: na role de teste, um usuário ativo com senha, um com convite pendente e um inativo
        await AuthenticateAsAdminAsync();
        string roleName = await CreateTestRoleAsync();

        Guid activeUserId = await RegisterUserAsync(UniqueEmail());
        (await HttpClient.PutAsJsonAsync($"users/{activeUserId}/roles", new { roleNames = new[] { roleName } }))
            .EnsureSuccessStatusCode();
        await InsertUserAsync("Pendente", "Teste", UniqueEmail(), null, true, roleName);
        await InsertUserAsync("Inativo", "Teste", UniqueEmail(), null, false, roleName);

        // Act
        List<RoleDto>? roles = await HttpClient.GetFromJsonAsync<List<RoleDto>>("roles");

        // Assert: convite pendente conta como ativo (DEC-10); o total inclui o inativo (DEC-4)
        RoleDto role = roles!.Single(r => r.Name == roleName);
        role.Id.ShouldNotBe(Guid.Empty);
        role.IsSystem.ShouldBeFalse();
        role.ActiveUserCount.ShouldBe(2);
        role.UserCount.ShouldBe(3);

        roles!.Where(r => r.IsSystem).Select(r => r.Id)
            .ShouldBe([RoleIds.Administrator, RoleIds.Agent, RoleIds.Collaborator], ignoreOrder: true);
    }

    [Fact]
    public async Task GetById_Should_ReturnRoleInTheListingFormat_WhenRoleExists()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string roleName = await CreateTestRoleAsync();
        (await HttpClient.PutAsJsonAsync($"roles/{roleName}/permissions", new { permissionCodes = AssignedPermissionCodes }))
            .EnsureSuccessStatusCode();
        await InsertUserAsync("Pendente", "Teste", UniqueEmail(), null, true, roleName);

        List<RoleDto>? roles = await HttpClient.GetFromJsonAsync<List<RoleDto>>("roles");
        RoleDto listed = roles!.Single(r => r.Name == roleName);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"roles/{listed.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        RoleDto? role = await response.Content.ReadFromJsonAsync<RoleDto>();
        role.ShouldNotBeNull();
        role.Id.ShouldBe(listed.Id);
        role.Name.ShouldBe(roleName);
        role.IsSystem.ShouldBeFalse();
        role.ActiveUserCount.ShouldBe(1);
        role.UserCount.ShouldBe(1);
        role.PermissionCodes.ShouldBe(AssignedPermissionCodes, ignoreOrder: true);
    }

    [Fact]
    public async Task GetById_Should_ReturnNotFound_WhenRoleDoesNotExist()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        var roleId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"roles/{roleId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Roles.NotFound");
        problem.Detail.ShouldBe($"A função com o id '{roleId}' não foi encontrada");
    }

    [Fact]
    public async Task GetById_Should_ReturnForbidden_WhenCallerLacksRolesReadPermission()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"roles/{RoleIds.Collaborator}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetById_Should_ReturnUnauthorized_WhenNoTokenProvided()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"roles/{RoleIds.Collaborator}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_Should_ReturnCreatedWithLocationAndTheRole_WhenNameIsFree()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string name = $"Supervisor {Guid.NewGuid():N}";

        try
        {
            // Act
            HttpResponseMessage response = await HttpClient.PostAsJsonAsync("roles", new { name = $"  {name}  " });

            // Assert: 201 com o endereço da consulta pelo id e a role sem permissões nem usuários
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            RoleDto? created = await response.Content.ReadFromJsonAsync<RoleDto>();
            created.ShouldNotBeNull();
            created.Name.ShouldBe(name);
            created.IsSystem.ShouldBeFalse();
            created.ActiveUserCount.ShouldBe(0);
            created.UserCount.ShouldBe(0);
            created.PermissionCodes.ShouldBeEmpty();
            response.Headers.Location!.ToString().ShouldBe($"/roles/{created.Id}");

            RoleDto? fetched = await HttpClient.GetFromJsonAsync<RoleDto>(response.Headers.Location);
            fetched!.Name.ShouldBe(name);
            List<RoleDto>? roles = await HttpClient.GetFromJsonAsync<List<RoleDto>>("roles");
            roles!.ShouldContain(r => r.Id == created.Id);
        }
        finally
        {
            await DeleteRolesNamedAsync(name);
        }
    }

    [Fact]
    public async Task Create_Should_ReturnConflict_WhenNameExistsIgnoringCase()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string name = $"Supervisor-{Guid.NewGuid():N}";
        string upperName = name.ToUpperInvariant();

        try
        {
            (await HttpClient.PostAsJsonAsync("roles", new { name })).EnsureSuccessStatusCode();

            // Act
            HttpResponseMessage response = await HttpClient.PostAsJsonAsync("roles", new { name = $"  {upperName}  " });

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
            problem!.Title.ShouldBe("Roles.NameNotUnique");
            problem.Detail.ShouldBe($"Já existe uma função com o nome \"{upperName}\"");

            List<RoleDto>? roles = await HttpClient.GetFromJsonAsync<List<RoleDto>>("roles");
            roles!.Count(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)).ShouldBe(1);
        }
        finally
        {
            await DeleteRolesNamedAsync(name);
        }
    }

    [Theory]
    [InlineData("", "'Função' deve ser informado.")]
    [InlineData("   ", "'Função' deve ser informado.")]
    [InlineData("101", "'Função' deve ter no máximo 100 caracteres.")]
    [InlineData("guid", "'Função' não pode ter o formato de um identificador (Guid).")]
    public async Task Create_Should_ReturnBadRequest_WhenNameIsInvalid(string kind, string message)
    {
        // Arrange: vazio, só espaços, 101 caracteres ou um Guid (DEC-11)
        await AuthenticateAsAdminAsync();
        string name = kind switch
        {
            "101" => new string('a', 101),
            "guid" => Guid.NewGuid().ToString(),
            _ => kind
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("roles", new { name });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ValidationProblemDto? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDto>();
        problem!.Errors.Select(e => e.Description).ShouldContain(message);
    }

    [Fact]
    public async Task Create_Should_ReturnCreated_WhenNameHas100CharactersAfterTrimming()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string name = Guid.NewGuid().ToString("N").PadRight(100, 'x');

        try
        {
            // Act
            HttpResponseMessage response = await HttpClient.PostAsJsonAsync("roles", new { name = $"  {name}  " });

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            (await response.Content.ReadFromJsonAsync<RoleDto>())!.Name.ShouldBe(name);
        }
        finally
        {
            await DeleteRolesNamedAsync(name);
        }
    }

    [Fact]
    public async Task Create_Should_CreateOnlyOne_WhenTwoRequestsRaceWithNamesDifferingOnlyInCase()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string name = $"Coordenação-{Guid.NewGuid():N}";

        try
        {
            // Act: as duas podem passar pela checagem do handler; o índice único do citext barra a segunda
            HttpResponseMessage[] responses = await Task.WhenAll(
                HttpClient.PostAsJsonAsync("roles", new { name }),
                HttpClient.PostAsJsonAsync("roles", new { name = name.ToUpperInvariant() }));

            // Assert: uma criada e a outra em conflito legível, nunca 500
            responses.Select(r => r.StatusCode).ShouldBe([HttpStatusCode.Created, HttpStatusCode.Conflict], ignoreOrder: true);
            ProblemDto? problem = await responses.Single(r => r.StatusCode == HttpStatusCode.Conflict)
                .Content.ReadFromJsonAsync<ProblemDto>();
            problem!.Detail.ShouldNotBeNullOrWhiteSpace();

            List<RoleDto>? roles = await HttpClient.GetFromJsonAsync<List<RoleDto>>("roles");
            roles!.Count(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)).ShouldBe(1);
        }
        finally
        {
            await DeleteRolesNamedAsync(name);
        }
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
    public async Task Create_Should_ReturnUnauthorized_WhenNoTokenProvided()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("roles", new { name = $"Role-{Guid.NewGuid():N}" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_Should_AuditTheNewRole()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string name = $"Auditada-{Guid.NewGuid():N}";

        try
        {
            // Act
            HttpResponseMessage response = await HttpClient.PostAsJsonAsync("roles", new { name });

            // Assert: a role é auditável (DEC-8), então a criação fica registrada com o nome
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            Guid roleId = (await response.Content.ReadFromJsonAsync<RoleDto>())!.Id;

            using IServiceScope scope = Services.CreateScope();
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            AuditLogEntry entry = await context.AuditLogs.AsNoTracking()
                .SingleAsync(e => e.EntityName == "Role" && e.EntityId == roleId.ToString());
            entry.Action.ShouldBe("Added");
            entry.NewValues.ShouldNotBeNull().ShouldContain(name);
        }
        finally
        {
            await DeleteRolesNamedAsync(name);
        }
    }

    /// <summary>
    /// Apaga as roles criadas por um teste de criação, comparando o nome sem maiúsculas como o citext: o banco é
    /// compartilhado pela coleção, e um nome longo deixado para trás atrapalharia os testes que voltam migrations.
    /// </summary>
    private async Task DeleteRolesNamedAsync(string name)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var roleIds = (await context.Roles.AsNoTracking().Select(r => new { r.Id, r.Name }).ToListAsync())
            .Where(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Id)
            .ToList();

        await context.Roles.Where(r => roleIds.Contains(r.Id)).ExecuteDeleteAsync();
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

    private sealed record RoleDto(
        Guid Id,
        string Name,
        bool IsDefault,
        bool IsAdministrator,
        bool IsSystem,
        int ActiveUserCount,
        int UserCount,
        List<string> PermissionCodes);

    private sealed record ProblemDto(int Status, string Title, string Detail);

    private sealed record ValidationProblemDto(int Status, string Title, List<ValidationErrorDto> Errors);

    private sealed record ValidationErrorDto(string Code, string Description);
}
