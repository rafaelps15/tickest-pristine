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

        builder.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(rolePermission => rolePermission.PermissionCode);

        // Permissões padrão de cada role. Depois da migration o administrador pode mudar as das outras roles pela API,
        // por isso uma migration que acrescente um vínculo aqui precisa trocar o InsertData gerado por um INSERT com
        // ON CONFLICT DO NOTHING (o vínculo pode já ter sido concedido à mão).
        RolePermission[] defaultRolePermissions =
            [
                .. Permission.All.Select(permission => Create(Role.Administrator, permission)),

                // Atendente
                Create(Role.Agent, Permission.TicketsCreate),
                Create(Role.Agent, Permission.TicketsViewOwn),
                Create(Role.Agent, Permission.TicketsUpdateOwn),
                Create(Role.Agent, Permission.TicketsDeleteOwn),
                Create(Role.Agent, Permission.TicketsReopenOwn),
                Create(Role.Agent, Permission.TicketsManage),

                // Colaborador
                Create(Role.Collaborator, Permission.TicketsCreate),
                Create(Role.Collaborator, Permission.TicketsViewOwn),
                Create(Role.Collaborator, Permission.TicketsUpdateOwn),
                Create(Role.Collaborator, Permission.TicketsDeleteOwn),
                Create(Role.Collaborator, Permission.TicketsReopenOwn)
            ];

        builder.HasData(defaultRolePermissions);
    }

    private static RolePermission Create(Role role, Permission permission) =>
        new() { RoleId = role.Id, PermissionCode = permission.Code };
}
