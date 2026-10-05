using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Roles;

namespace TickestPristine.Infrastructure.Database.Seeding;

/// <summary>
/// Roles iniciais, para o sistema já nascer com a role padrão do cadastro e a de quem atende a fila. Liga com
/// "Seeding:DefaultRoles" (ligado por padrão) e só roda num banco sem nenhuma role além do Administrador: depois
/// disso as roles e as permissões delas são dos administradores, e o seed não recria o que eles editaram.
/// </summary>
internal sealed class DefaultRolesSeeder(IOptions<SeedingOptions> seedingOptions)
{
    private static readonly RoleSeed[] Roles =
    [
        new("Atendente", IsDefault: false,
        [
            PermissionCodes.Tickets.Create,
            PermissionCodes.Tickets.ViewOwn,
            PermissionCodes.Tickets.UpdateOwn,
            PermissionCodes.Tickets.DeleteOwn,
            PermissionCodes.Tickets.ReopenOwn,
            PermissionCodes.Tickets.Manage
        ]),
        new("Colaborador", IsDefault: true,
        [
            PermissionCodes.Tickets.Create,
            PermissionCodes.Tickets.ViewOwn,
            PermissionCodes.Tickets.UpdateOwn,
            PermissionCodes.Tickets.DeleteOwn,
            PermissionCodes.Tickets.ReopenOwn
        ])
    ];

    public async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        if (!seedingOptions.Value.DefaultRoles ||
            await context.Roles.AnyAsync(r => r.Id != SystemRoles.AdministratorId, cancellationToken))
        {
            return;
        }

        foreach (RoleSeed roleSeed in Roles)
        {
            var role = new Role { Id = Guid.NewGuid(), Name = roleSeed.Name, IsDefault = roleSeed.IsDefault };

            context.Roles.Add(role);
            context.RolePermissions.AddRange(roleSeed.PermissionCodes.Select(code =>
                new RolePermission { RoleId = role.Id, PermissionCode = code }));
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private sealed record RoleSeed(string Name, bool IsDefault, string[] PermissionCodes);
}
