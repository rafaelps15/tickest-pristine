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

        // O Administrador recebe o catálogo inteiro: incluir ou remover uma permissão do catálogo exige uma migration.
        builder.HasData(PermissionCatalog.All.Select(code =>
            new RolePermission { RoleId = SystemRoles.AdministratorId, PermissionCode = code }));
    }
}
