using TickestPristine.Application.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;
using TickestPristine.Application.Users;

namespace TickestPristine.Infrastructure.Database.Seeding;

/// <summary>
/// Cria o primeiro administrador a partir de <see cref="AdminUserOptions"/>, só num banco sem nenhum administrador
/// ativo, com a senha marcada como provisória: o primeiro login exige trocá-la. Depois disso os administradores são
/// geridos pela API: o seed não devolve a role a quem a perdeu, não recria a conta e nem lê mais a seção "Admin".
/// </summary>
internal sealed class AdminUserSeeder(
    IOptions<AdminUserOptions> adminUserOptions,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider)
{
    public async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        if (await ActiveAdministratorExistsAsync(context, cancellationToken))
        {
            return;
        }

        AdminUserOptions settings = adminUserOptions.Value;

        // Promover uma conta que já existe entregaria o sistema a quem se cadastrou com esse e-mail, com a senha dela.
        if (await context.Users.AnyAsync(u => u.Email == settings.Email, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Não há administrador ativo e a conta de '{AdminUserOptions.SectionName}:Email' já existe. " +
                "Atribua a função de administrador a uma conta manualmente ou configure outro e-mail.");
        }

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = settings.Email,
            FirstName = settings.FirstName,
            LastName = settings.LastName,
            Code = UserCodes.New(),
            IsActive = true,
            CreatedAtUtc = dateTimeProvider.UtcNow
        };

        context.Users.Add(admin);
        context.UserCredentials.Add(new UserCredential
        {
            Id = Guid.NewGuid(),
            UserId = admin.Id,
            PasswordHash = passwordHasher.Hash(settings.Password),
            MustChangePassword = true
        });
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = admin.Id, RoleName = RoleNames.Administrator });

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Task<bool> ActiveAdministratorExistsAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        return context.UserRoles.AnyAsync(
            userRole => userRole.RoleName == RoleNames.Administrator &&
                        context.Users.Any(user => user.Id == userRole.UserId && user.IsActive),
            cancellationToken);
    }
}
