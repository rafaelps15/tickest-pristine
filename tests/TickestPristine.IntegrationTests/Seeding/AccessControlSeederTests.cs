using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Roles;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Web.Api.Extensions;

namespace TickestPristine.IntegrationTests.Seeding;

/// <summary>
/// Valida o seeder do administrador; a verificação é feita por HTTP.
/// </summary>
public sealed class AccessControlSeederTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string AdminEmail = "admin@tickestpristine.dev";
    private const string AdminPassword = "ChangeMe123!";
    private const string UnknownPermissionCode = "legacy:removed-permission";

    [Fact]
    public async Task SeedAccessControl_Should_RestoreMissingAdministratorPermissions_AndRemoveUnknownCodes()
    {
        // Arrange: Admin sem uma permissão do catálogo e com um código inexistente
        using (IServiceScope scope = Services.CreateScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Guid administratorRoleId = await context.Roles.Where(r => r.IsAdministrator).Select(r => r.Id).SingleAsync();

            RolePermission removed = await context.RolePermissions.SingleAsync(
                p => p.RoleId == administratorRoleId && p.PermissionCode == PermissionCodes.Users.Read);
            context.RolePermissions.Remove(removed);
            context.RolePermissions.Add(new RolePermission { Id = Guid.NewGuid(), RoleId = administratorRoleId, PermissionCode = UnknownPermissionCode });

            await context.SaveChangesAsync();
        }

        // Act
        await new ApplicationBuilder(Services).SeedAccessControlAsync();

        // Assert
        AccessTokens tokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(tokens.AccessToken);

        List<RoleDto>? roles = await HttpClient.GetFromJsonAsync<List<RoleDto>>("roles");

        RoleDto administrator = roles!.Single(r => r.IsAdministrator);
        administrator.PermissionCodes.ShouldBe(PermissionCodes.All, ignoreOrder: true);
        roles!.ShouldAllBe(r => !r.PermissionCodes.Contains(UnknownPermissionCode));
    }

    [Fact]
    public async Task SeedAccessControl_Should_ReuseExistingAdminRole_WhenNoRoleIsFlaggedAsAdministrator()
    {
        // Arrange: role "Admin" sem a flag de administrador
        AccessTokens tokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(tokens.AccessToken);
        List<RoleDto>? rolesBefore = await HttpClient.GetFromJsonAsync<List<RoleDto>>("roles");

        using (IServiceScope scope = Services.CreateScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.ExecuteSqlRawAsync("UPDATE public.roles SET is_administrator = FALSE WHERE is_administrator");
        }

        // Act
        await new ApplicationBuilder(Services).SeedAccessControlAsync();

        // Assert
        List<RoleDto>? rolesAfter = await HttpClient.GetFromJsonAsync<List<RoleDto>>("roles");

        rolesAfter!.Count.ShouldBe(rolesBefore!.Count);
        RoleDto administrator = rolesAfter.Where(r => r.IsAdministrator).ShouldHaveSingleItem();
        administrator.Name.ShouldBe(DefaultRoles.Admin);
        administrator.Id.ShouldBe(rolesBefore.Single(r => r.Name == DefaultRoles.Admin).Id);
    }

    private sealed record RoleDto(Guid Id, string Name, bool IsDefault, bool IsAdministrator, List<string> PermissionCodes);
}
