using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.IntegrationTests.Seeding;

/// <summary>
/// Valida os dados que as migrations e o seed criam: roles padrão (HasData), o administrador inicial e a
/// estrutura inicial de departamentos e setores.
/// </summary>
public sealed class DatabaseSeedingTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string AdminEmail = "admin@tickestpristine.dev";
    private const string AdminPassword = "ChangeMe123!";

    [Fact]
    public async Task Migrations_Should_CreateDefaultRolesWithFixedIds()
    {
        // Arrange
        AccessTokens tokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(tokens.AccessToken);

        // Act
        List<RoleDto>? roles = await HttpClient.GetFromJsonAsync<List<RoleDto>>("roles");

        // Assert
        RoleDto administrator = roles!.Single(r => r.Id == Role.Administrator.Id);
        administrator.Name.ShouldBe(Role.Administrator.Name);
        administrator.IsAdministrator.ShouldBeTrue();

        RoleDto agent = roles!.Single(r => r.Id == Role.Agent.Id);
        agent.Name.ShouldBe(Role.Agent.Name);

        RoleDto collaborator = roles!.Single(r => r.Id == Role.Collaborator.Id);
        collaborator.Name.ShouldBe(Role.Collaborator.Name);
        collaborator.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task Migrations_Should_GrantEveryCatalogPermissionToAdministrator()
    {
        // Arrange
        AccessTokens tokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(tokens.AccessToken);

        // Act
        List<RoleDto>? roles = await HttpClient.GetFromJsonAsync<List<RoleDto>>("roles");

        // Assert: falha quando um código entra ou sai do PermissionCodes sem a migration correspondente
        RoleDto administrator = roles!.Single(r => r.Id == Role.Administrator.Id);
        administrator.PermissionCodes.ShouldBe(PermissionCodes.All, ignoreOrder: true);
    }

    [Fact]
    public async Task Seeding_Should_NotDuplicateAdministrator_WhenMigrationsRunAgain()
    {
        // Arrange
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act
        await context.Database.MigrateAsync();

        // Assert
        User administrator = await context.Users.SingleAsync(u => u.Email == AdminEmail);
        administrator.IsActive.ShouldBeTrue();

        Guid administratorRoleId = Role.Administrator.Id;
        int administratorRoleLinks = await context.UserRoles
            .CountAsync(ur => ur.UserId == administrator.Id && ur.RoleId == administratorRoleId);
        administratorRoleLinks.ShouldBe(1);
    }

    [Fact]
    public async Task Seeding_Should_NotDuplicateDefaultOrganization_WhenMigrationsRunAgain()
    {
        // Arrange
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act
        await context.Database.MigrateAsync();

        // Assert
        Department informationTechnology = await context.Departments.SingleAsync(d => d.Name == "TI");
        informationTechnology.IsActive.ShouldBeTrue();

        List<string> sectorNames = await context.Sectors
            .Where(s => s.DepartmentId == informationTechnology.Id)
            .Select(s => s.Name)
            .ToListAsync();
        sectorNames.ShouldBe(["Helpdesk", "Infraestrutura"], ignoreOrder: true);
    }

    private sealed record RoleDto(Guid Id, string Name, bool IsDefault, bool IsAdministrator, List<string> PermissionCodes);
}
