using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using TickestPristine.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Web.Api.Extensions;

public static class AdminSeederExtensions
{
    private static readonly string[] MemberPermissions =
    [
        PermissionCodes.Tickets.Create,
        PermissionCodes.Tickets.ViewOwn,
        PermissionCodes.Tickets.UpdateOwn,
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

        var memberRole = Role.Create(RoleNames.Member);
        context.Roles.Add(memberRole);

        foreach (string permissionCode in MemberPermissions)
        {
            context.RolePermissions.Add(RolePermission.Create(memberRole.Id, permissionCode));
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
