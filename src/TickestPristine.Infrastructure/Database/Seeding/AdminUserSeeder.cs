using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TickestPristine.SharedKernel;

namespace TickestPristine.Infrastructure.Database.Seeding;

/// <summary>
/// Garante o usuário administrador de <see cref="AdminUserOptions"/>, com a role Administrador.
/// </summary>
internal sealed class AdminUserSeeder(
    IOptions<AdminUserOptions> adminUserOptions,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider)
{
    public void Seed(DbContext context)
    {
        AdminUserOptions settings = adminUserOptions.Value;

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
        AdminUserOptions settings = adminUserOptions.Value;

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
    private bool AddMissing(DbContext context, AdminUserOptions settings, User? admin, bool hasAdminRole)
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
                IsActive = true,
                CreatedAtUtc = dateTimeProvider.UtcNow
            };

            context.Add(admin);
            context.Add(new UserCredential { Id = Guid.NewGuid(), UserId = admin.Id, PasswordHash = passwordHasher.Hash(settings.Password) });
        }

        context.Add(new UserRole { Id = Guid.NewGuid(), UserId = admin.Id, RoleId = Role.Administrator.Id });

        return true;
    }
}
