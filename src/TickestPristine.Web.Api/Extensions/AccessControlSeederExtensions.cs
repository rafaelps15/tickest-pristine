using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using TickestPristine.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Web.Api.Extensions;

/// <summary>
/// Cria as roles padrão e o usuário administrador inicial e mantém as permissões do administrador em dia. Roda em todos os ambientes.
/// </summary>
public static class AccessControlSeederExtensions
{
    public static async Task SeedAccessControlAsync(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        ApplicationDbContext context = services.GetRequiredService<ApplicationDbContext>();
        IConfiguration configuration = services.GetRequiredService<IConfiguration>();
        IPasswordHasher passwordHasher = services.GetRequiredService<IPasswordHasher>();

        await RemoveUnknownPermissionsAsync(context);

        Role? existingAdminRole = await context.Roles.SingleOrDefaultAsync(r => r.IsAdministrator);

        if (existingAdminRole is not null)
        {
            await GrantMissingPermissionsToAdminAsync(context, existingAdminRole.Id);
            return;
        }

        // Sem role marcada como administradora: reaproveita as roles com os nomes padrão e cria apenas as que faltam.
        Role adminRole = await GetOrCreateRoleAsync(context, DefaultRoles.Admin, PermissionCodes.All);
        adminRole.IsAdministrator = true;

        await GetOrCreateRoleAsync(context, DefaultRoles.Agent, DefaultRoles.AgentPermissions);

        Role requesterRole = await GetOrCreateRoleAsync(context, DefaultRoles.Requester, DefaultRoles.RequesterPermissions);

        if (!await context.Roles.AnyAsync(r => r.IsDefault))
        {
            requesterRole.IsDefault = true;
        }

        string email = configuration["Admin:Email"]
            ?? throw new InvalidOperationException("Admin:Email configuration is required to seed the admin user.");
        string firstName = configuration["Admin:FirstName"] ?? "Admin";
        string lastName = configuration["Admin:LastName"] ?? "Master";
        string password = configuration["Admin:Password"]
            ?? throw new InvalidOperationException("Admin:Password configuration is required to seed the admin user.");

        User? adminUser = await context.Users.SingleOrDefaultAsync(u => u.Email == email);

        if (adminUser is null)
        {
            adminUser = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                Code = $"usr_{Ulid.NewUlid()}"
            };

            adminUser.Raise(new UserRegisteredDomainEvent(adminUser.Id));

            context.Users.Add(adminUser);
            context.UserCredentials.Add(new UserCredential { Id = Guid.NewGuid(), UserId = adminUser.Id, PasswordHash = passwordHasher.Hash(password) });
        }

        bool adminUserHasRole = await context.UserRoles.AnyAsync(ur => ur.UserId == adminUser.Id && ur.RoleId == adminRole.Id);

        if (!adminUserHasRole)
        {
            context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = adminUser.Id, RoleId = adminRole.Id });
        }

        await context.SaveChangesAsync();

        // Completa as permissões de uma role Admin reaproveitada.
        await GrantMissingPermissionsToAdminAsync(context, adminRole.Id);
    }

    private static async Task<Role> GetOrCreateRoleAsync(
        ApplicationDbContext context,
        string name,
        IEnumerable<string> initialPermissions)
    {
        Role? existing = await context.Roles.SingleOrDefaultAsync(r => r.Name == name);

        if (existing is not null)
        {
            return existing;
        }

        var role = new Role { Id = Guid.NewGuid(), Name = name };

        role.Raise(new RoleCreatedDomainEvent(role.Id));
        context.Roles.Add(role);

        foreach (string permissionCode in initialPermissions)
        {
            context.RolePermissions.Add(new RolePermission { Id = Guid.NewGuid(), RoleId = role.Id, PermissionCode = permissionCode });
        }

        return role;
    }

    /// <summary>
    /// Remove das roles os códigos de permissão que não existem mais no catálogo.
    /// </summary>
    private static async Task RemoveUnknownPermissionsAsync(ApplicationDbContext context)
    {
        List<RolePermission> unknownPermissions = await context.RolePermissions
            .Where(p => !PermissionCodes.All.Contains(p.PermissionCode))
            .ToListAsync();

        if (unknownPermissions.Count == 0)
        {
            return;
        }

        context.RolePermissions.RemoveRange(unknownPermissions);

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Concede à role de administrador as permissões do catálogo que ela ainda não tem.
    /// </summary>
    private static async Task GrantMissingPermissionsToAdminAsync(ApplicationDbContext context, Guid adminRoleId)
    {
        List<string> currentPermissions = await context.RolePermissions
            .Where(p => p.RoleId == adminRoleId)
            .Select(p => p.PermissionCode)
            .ToListAsync();

        var missingPermissions = PermissionCodes.All.Except(currentPermissions).ToList();

        if (missingPermissions.Count == 0)
        {
            return;
        }

        foreach (string permissionCode in missingPermissions)
        {
            context.RolePermissions.Add(new RolePermission { Id = Guid.NewGuid(), RoleId = adminRoleId, PermissionCode = permissionCode });
        }

        await context.SaveChangesAsync();
    }
}
