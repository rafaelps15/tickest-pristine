using TickestPristine.Application.Permissions;
using TickestPristine.Application.Roles;
using TickestPristine.Domain.Roles;
using TickestPristine.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Roles;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasKey(rolePermission => rolePermission.Id);

        builder.HasIndex(rolePermission => new { rolePermission.RoleName, rolePermission.PermissionCode }).IsUnique();

        builder.Property(rolePermission => rolePermission.PermissionCode).HasMaxLength(100);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(rolePermission => rolePermission.RoleName)
            .HasPrincipalKey(role => role.Name);

        builder.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(rolePermission => rolePermission.PermissionCode)
            .HasPrincipalKey(permission => permission.Code);

        // Permissões padrão de cada role. Depois da migration o administrador pode mudar as das outras roles pela API,
        // por isso uma migration que acrescente um vínculo aqui precisa trocar o InsertData gerado por um INSERT com
        // ON CONFLICT DO NOTHING (o vínculo pode já ter sido concedido à mão).
        RolePermission[] defaultRolePermissions =
            [
                .. PermissionCatalog.All.Select(permission => Create(RoleNames.Administrator, permission)),

                // Atendente
                Create(RoleNames.Agent, PermissionCatalog.TicketsCreate),
                Create(RoleNames.Agent, PermissionCatalog.TicketsViewOwn),
                Create(RoleNames.Agent, PermissionCatalog.TicketsUpdateOwn),
                Create(RoleNames.Agent, PermissionCatalog.TicketsReopenOwn),
                Create(RoleNames.Agent, PermissionCatalog.TicketsManage),

                // Colaborador
                Create(RoleNames.Collaborator, PermissionCatalog.TicketsCreate),
                Create(RoleNames.Collaborator, PermissionCatalog.TicketsViewOwn),
                Create(RoleNames.Collaborator, PermissionCatalog.TicketsUpdateOwn),
                Create(RoleNames.Collaborator, PermissionCatalog.TicketsReopenOwn)
            ];

        builder.HasData(defaultRolePermissions);
    }

    private static RolePermission Create(string roleName, PermissionCatalogEntry permission) => new()
    {
        Id = DeterministicGuid.Create($"role-permission:{roleName}:{permission.Code}"),
        RoleName = roleName,
        PermissionCode = permission.Code
    };
}
