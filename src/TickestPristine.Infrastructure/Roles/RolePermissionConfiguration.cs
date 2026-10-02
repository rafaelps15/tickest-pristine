using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Roles;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasKey(rolePermission => new { rolePermission.RoleId, rolePermission.PermissionCode });

        builder.Property(rolePermission => rolePermission.PermissionCode).HasMaxLength(100);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(rolePermission => rolePermission.RoleId);

        // Permissões das roles padrão. Qualquer mudança aqui, inclusive no catálogo PermissionCodes
        // (que o Administrador recebe inteiro), exige uma nova migration.
        RolePermission[] defaultRolePermissions =
            [
                .. PermissionCodes.All.Select(code => Create(Role.Administrator, code)),

                // Atendente
                Create(Role.Agent, PermissionCodes.Tickets.Create),
                Create(Role.Agent, PermissionCodes.Tickets.ViewOwn),
                Create(Role.Agent, PermissionCodes.Tickets.UpdateOwn),
                Create(Role.Agent, PermissionCodes.Tickets.DeleteOwn),
                Create(Role.Agent, PermissionCodes.Tickets.ReopenOwn),
                Create(Role.Agent, PermissionCodes.Tickets.Manage),

                // Colaborador
                Create(Role.Collaborator, PermissionCodes.Tickets.Create),
                Create(Role.Collaborator, PermissionCodes.Tickets.ViewOwn),
                Create(Role.Collaborator, PermissionCodes.Tickets.UpdateOwn),
                Create(Role.Collaborator, PermissionCodes.Tickets.DeleteOwn),
                Create(Role.Collaborator, PermissionCodes.Tickets.ReopenOwn)
            ];

        builder.HasData(defaultRolePermissions);
    }

    private static RolePermission Create(Role role, string permissionCode) =>
        new() { RoleId = role.Id, PermissionCode = permissionCode };
}
