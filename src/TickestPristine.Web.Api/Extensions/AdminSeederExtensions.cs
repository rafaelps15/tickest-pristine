using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Users;
using TickestPristine.Infrastructure.Database;
using TickestPristine.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Web.Api.Extensions;

public static class AdminSeederExtensions
{
    public static async Task SeedAdminUserAsync(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        ApplicationDbContext context = services.GetRequiredService<ApplicationDbContext>();
        IConfiguration configuration = services.GetRequiredService<IConfiguration>();
        IPasswordHasher passwordHasher = services.GetRequiredService<IPasswordHasher>();
        IDateTimeProvider dateTimeProvider = services.GetRequiredService<IDateTimeProvider>();

        bool adminAlreadyProvisioned = await context.UserPermissions
            .AnyAsync(p => p.PermissionCode == PermissionCodes.Users.ManagePermissions);

        if (adminAlreadyProvisioned)
        {
            return;
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
            adminUser = User.Create(email, firstName, lastName, dateTimeProvider.UtcNow);

            context.Users.Add(adminUser);
            context.UserCredentials.Add(UserCredential.Create(adminUser.Id, passwordHasher.Hash(password)));
        }

        foreach (string permissionCode in PermissionCodes.All)
        {
            context.UserPermissions.Add(UserPermission.Create(adminUser.Id, permissionCode));
        }

        await context.SaveChangesAsync();
    }
}
