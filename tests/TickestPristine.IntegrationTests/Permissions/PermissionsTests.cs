using System.Net;
using System.Net.Http.Json;
using TickestPristine.Domain.Permissions;
using TickestPristine.Domain.Roles;
using TickestPristine.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact]
    public async Task PermissionsTable_Should_MatchCatalog_WhenDatabaseIsMigrated()
    {
        // Arrange
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act
        List<Permission> permissions = await context.Set<Permission>().AsNoTracking().ToListAsync();

        // Assert
        permissions.Select(permission => new
            {
                permission.Code,
                permission.Name,
                permission.Description,
                permission.Group,
                permission.IsAdministrative
            })
            .OrderBy(permission => permission.Code)
            .ShouldBe(PermissionCatalog.All.Select(permission => new
            {
                permission.Code,
                permission.Name,
                permission.Description,
                permission.Group,
                permission.IsAdministrative
            }).OrderBy(permission => permission.Code));
    }
    private sealed record PermissionDto(string Code, string Name, string Description, string Group, bool IsAdministrative);
}


