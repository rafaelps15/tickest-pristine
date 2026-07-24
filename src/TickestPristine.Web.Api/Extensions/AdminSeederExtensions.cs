using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using TickestPristine.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Web.Api.Extensions;

/// <summary>
/// Production-safe bootstrap seeder: idempotently provisions the Admin/Agent/Requester roles and the
/// initial admin user from <c>Admin:Email</c>/<c>Admin:Password</c> configuration. Unlike
/// <see cref="SampleDataSeederExtensions"/>, this is intended to run in every environment, including
/// production, so a deployment always has a working admin login.
/// </summary>
public static class AdminSeederExtensions
{
    private static readonly string[] RequesterPermissions =
    [
        PermissionCodes.Tickets.Create,
        PermissionCodes.Tickets.ViewOwn,
        PermissionCodes.Tickets.UpdateOwn,
        PermissionCodes.Tickets.DeleteOwn,
        PermissionCodes.Tickets.ReopenOwn,
        PermissionCodes.Users.Access
    ];

    // Agents handle tickets across the board (not just their own), so they get the "Manage"-level
    // ticket permission plus the same baseline access a Requester has - but nothing Admin-only
    // (Users/Roles/Departments/Sectors management stays Admin-only).
    private static readonly string[] AgentPermissions =
    [
        PermissionCodes.Tickets.Create,
        PermissionCodes.Tickets.ViewOwn,
        PermissionCodes.Tickets.UpdateOwn,
        PermissionCodes.Tickets.DeleteOwn,
        PermissionCodes.Tickets.ReopenOwn,
        PermissionCodes.Tickets.Manage,
        PermissionCodes.Users.Access
    ];

    public static async Task SeedAdminUserAsync(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        ApplicationDbContext context = services.GetRequiredService<ApplicationDbContext>();
        IConfiguration configuration = services.GetRequiredService<IConfiguration>();
        IPasswordHasher passwordHasher = services.GetRequiredService<IPasswordHasher>();

        bool adminAlreadyProvisioned = await context.Roles.AnyAsync(r => r.Name == RoleNames.Admin);

        if (adminAlreadyProvisioned)
        {
            return;
        }

        var adminRole = Role.Create(RoleNames.Admin);
        context.Roles.Add(adminRole);

        foreach (string permissionCode in PermissionCodes.All)
        {
            context.RolePermissions.Add(RolePermission.Create(adminRole.Id, permissionCode));
        }

        var agentRole = Role.Create(RoleNames.Agent);
        context.Roles.Add(agentRole);

        foreach (string permissionCode in AgentPermissions)
        {
            context.RolePermissions.Add(RolePermission.Create(agentRole.Id, permissionCode));
        }

        var requesterRole = Role.Create(RoleNames.Requester);
        context.Roles.Add(requesterRole);

        foreach (string permissionCode in RequesterPermissions)
        {
            context.RolePermissions.Add(RolePermission.Create(requesterRole.Id, permissionCode));
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
            adminUser = User.Create(email, firstName, lastName);

            context.Users.Add(adminUser);
            context.UserCredentials.Add(UserCredential.Create(adminUser.Id, passwordHasher.Hash(password)));
        }

        context.UserRoles.Add(UserRole.Create(adminUser.Id, adminRole.Id));

        await context.SaveChangesAsync();
    }
}
