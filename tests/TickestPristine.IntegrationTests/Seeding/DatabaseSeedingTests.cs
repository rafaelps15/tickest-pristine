using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.IntegrationTests.Seeding;

/// <summary>
/// Valida os dados que as migrations e o seed criam: role de administrador (HasData), roles iniciais, o
/// administrador inicial e a estrutura inicial de departamentos e setores.
/// </summary>
public sealed class DatabaseSeedingTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Seeding_Should_CreateAdministratorAndDefaultRoles()
    {
        // Arrange
        AccessTokens tokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(tokens.AccessToken);

        // Act
        List<RoleDto>? roles = await HttpClient.GetFromJsonAsync<List<RoleDto>>("roles");

        // Assert
        RoleDto administrator = roles!.Single(r => r.Name == Role.Administrator.Name);
        administrator.IsAdministrator.ShouldBeTrue();

        RoleDto agent = roles!.Single(r => r.Name == "Atendente");
        agent.IsDefault.ShouldBeFalse();
        agent.PermissionCodes.ShouldContain(PermissionCodes.Tickets.Manage);

        RoleDto collaborator = roles!.Single(r => r.Name == "Colaborador");
        collaborator.IsDefault.ShouldBeTrue();
        collaborator.PermissionCodes.ShouldContain(PermissionCodes.Tickets.Create);
        collaborator.PermissionCodes.ShouldNotContain(PermissionCodes.Tickets.Manage);
    }

    [Fact]
    public async Task Migrations_Should_GrantEveryCatalogPermissionToAdministrator()
    {
        // Arrange
        AccessTokens tokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(tokens.AccessToken);

        // Act
        List<RoleDto>? roles = await HttpClient.GetFromJsonAsync<List<RoleDto>>("roles");

        // Assert: falha quando um código entra ou sai do catálogo de Permission sem a migration correspondente
        RoleDto administrator = roles!.Single(r => r.Name == Role.Administrator.Name);
        administrator.PermissionCodes.ShouldBe(Permission.All.Select(p => p.Code), ignoreOrder: true);
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

        string administratorRoleName = Role.Administrator.Name;
        int administratorRoleLinks = await context.UserRoles
            .CountAsync(ur => ur.UserId == administrator.Id && ur.RoleName == administratorRoleName);
        administratorRoleLinks.ShouldBe(1);
    }

    [Fact]
    public async Task Seeding_Should_NotRestoreAdministratorRole_WhenAnotherActiveAdministratorExists()
    {
        // Arrange
        string administratorRoleName = Role.Administrator.Name;
        Guid anotherAdministratorId = await RegisterUserAsync(UniqueEmail());

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        User seededAdministrator = await context.Users.SingleAsync(u => u.Email == AdminEmail);
        UserRole seededAdministratorRole = await context.UserRoles
            .SingleAsync(ur => ur.UserId == seededAdministrator.Id && ur.RoleName == administratorRoleName);
        var anotherAdministratorRole = new UserRole { Id = Guid.NewGuid(), UserId = anotherAdministratorId, RoleName = administratorRoleName };

        context.UserRoles.Remove(seededAdministratorRole);
        context.UserRoles.Add(anotherAdministratorRole);
        await context.SaveChangesAsync();

        try
        {
            // Act
            await context.Database.MigrateAsync();

            // Assert
            bool seededAdministratorHasRole = await context.UserRoles
                .AnyAsync(ur => ur.UserId == seededAdministrator.Id && ur.RoleName == administratorRoleName);
            seededAdministratorHasRole.ShouldBeFalse();
        }
        finally
        {
            // Devolve o banco compartilhado ao estado inicial: só o administrador do seed.
            context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = seededAdministrator.Id, RoleName = administratorRoleName });
            context.UserRoles.Remove(anotherAdministratorRole);
            await context.SaveChangesAsync();
        }
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
        sectorNames.ShouldBe(
            ["Suporte ao Usuário", "Infraestrutura e Redes", "Sistemas Corporativos", "Segurança da Informação"],
            ignoreOrder: true);
    }

    private sealed record RoleDto(string Name, bool IsDefault, bool IsAdministrator, List<string> PermissionCodes);
}
