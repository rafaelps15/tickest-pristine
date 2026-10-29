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

        builder.HasIndex(rolePermission => new { rolePermission.RoleId, rolePermission.PermissionCode }).IsUnique();

        builder.Property(rolePermission => rolePermission.PermissionCode).HasMaxLength(100);

        // Excluir uma role apaga os vínculos dela com permissões junto (spec 036).
        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(rolePermission => rolePermission.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(rolePermission => rolePermission.PermissionCode)
            .HasPrincipalKey(permission => permission.Code);

        // Permissões padrão de cada role. Depois da migration o administrador pode mudar as das outras roles pela API,
        // por isso uma migration que acrescente um vínculo aqui precisa trocar o InsertData gerado por um INSERT com
        // ON CONFLICT DO NOTHING (o vínculo pode já ter sido concedido à mão).
        RolePermission[] defaultRolePermissions =
            [
                .. PermissionCatalog.All.Select(permission => Create(RoleIds.Administrator, RoleNames.Administrator, permission)),

                // Atendente
                Create(RoleIds.Agent, RoleNames.Agent, PermissionCatalog.TicketsCreate),
                Create(RoleIds.Agent, RoleNames.Agent, PermissionCatalog.TicketsViewOwn),
                Create(RoleIds.Agent, RoleNames.Agent, PermissionCatalog.TicketsUpdateOwn),
                Create(RoleIds.Agent, RoleNames.Agent, PermissionCatalog.TicketsReopenOwn),
                Create(RoleIds.Agent, RoleNames.Agent, PermissionCatalog.TicketsManage),

                // Colaborador
                Create(RoleIds.Collaborator, RoleNames.Collaborator, PermissionCatalog.TicketsCreate),
                Create(RoleIds.Collaborator, RoleNames.Collaborator, PermissionCatalog.TicketsViewOwn),
                Create(RoleIds.Collaborator, RoleNames.Collaborator, PermissionCatalog.TicketsUpdateOwn),
                Create(RoleIds.Collaborator, RoleNames.Collaborator, PermissionCatalog.TicketsReopenOwn)
            ];

        builder.HasData(defaultRolePermissions);
    }

    // O id do vínculo continua derivado do nome da role, como foi gravado: as roles fixas não mudam de nome.
    private static RolePermission Create(Guid roleId, string roleName, PermissionCatalogEntry permission) => new()
    {
        Id = DeterministicGuid.Create($"role-permission:{roleName}:{permission.Code}"),
        RoleId = roleId,
        PermissionCode = permission.Code
    };
}
