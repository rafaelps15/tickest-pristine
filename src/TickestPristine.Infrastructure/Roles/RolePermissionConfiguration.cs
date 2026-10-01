using TickestPristine.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Roles;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasKey(rolePermission => rolePermission.Id);

        builder.Property(rolePermission => rolePermission.PermissionCode).HasMaxLength(100);

        builder.HasIndex(rolePermission => new { rolePermission.RoleId, rolePermission.PermissionCode }).IsUnique();

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(rolePermission => rolePermission.RoleId);
    }
}
