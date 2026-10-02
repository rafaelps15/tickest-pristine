using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace TickestPristine.Infrastructure.Database.Seeding;

/// <summary>
/// Garante o usuário administrador da seção "Admin" da configuração, com a role Administrador.
/// </summary>
internal sealed class AdminUserSeeder(IConfiguration configuration, IPasswordHasher passwordHasher)
{
    public void Seed(DbContext context)
    {
        AdminSettings settings = ReadSettings();

        Guid administratorRoleId = Role.Administrator.Id;

        User? admin = context.Set<User>().SingleOrDefault(u => u.Email == settings.Email);
        bool hasAdminRole = admin is not null &&
            context.Set<UserRole>().Any(ur => ur.UserId == admin.Id && ur.RoleId == administratorRoleId);

        if (AddMissing(context, settings, admin, hasAdminRole))
        {
            context.SaveChanges();
        }
    }

    public async Task SeedAsync(DbContext context, CancellationToken cancellationToken)
    {
        AdminSettings settings = ReadSettings();

        Guid administratorRoleId = Role.Administrator.Id;

        User? admin = await context.Set<User>().SingleOrDefaultAsync(u => u.Email == settings.Email, cancellationToken);
        bool hasAdminRole = admin is not null &&
            await context.Set<UserRole>().AnyAsync(ur => ur.UserId == admin.Id && ur.RoleId == administratorRoleId, cancellationToken);

        if (AddMissing(context, settings, admin, hasAdminRole))
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Inclui no contexto o que faltar (usuário, credencial, vínculo com a role); retorna se algo foi incluído.
    /// </summary>
    private bool AddMissing(DbContext context, AdminSettings settings, User? admin, bool hasAdminRole)
    {
        if (admin is not null && hasAdminRole)
        {
            return false;
        }

        if (admin is null)
        {
            admin = new User
            {
                Id = Guid.NewGuid(),
                Email = settings.Email,
                FirstName = settings.FirstName,
                LastName = settings.LastName,
                Code = $"usr_{Ulid.NewUlid()}",
                IsActive = true
            };

            context.Add(admin);
            context.Add(new UserCredential { Id = Guid.NewGuid(), UserId = admin.Id, PasswordHash = passwordHasher.Hash(settings.Password) });
        }

        context.Add(new UserRole { Id = Guid.NewGuid(), UserId = admin.Id, RoleId = Role.Administrator.Id });

        return true;
    }

    private AdminSettings ReadSettings()
    {
        string email = configuration["Admin:Email"] is { Length: > 0 } configuredEmail
            ? configuredEmail
            : throw new InvalidOperationException("Admin:Email configuration is required to seed the admin user.");
        string password = configuration["Admin:Password"] is { Length: > 0 } configuredPassword
            ? configuredPassword
            : throw new InvalidOperationException("Admin:Password configuration is required to seed the admin user.");

        return new AdminSettings(
            email,
            configuration["Admin:FirstName"] is { Length: > 0 } firstName ? firstName : "Admin",
            configuration["Admin:LastName"] is { Length: > 0 } lastName ? lastName : "Master",
            password);
    }

    private sealed record AdminSettings(string Email, string FirstName, string LastName, string Password);
}
