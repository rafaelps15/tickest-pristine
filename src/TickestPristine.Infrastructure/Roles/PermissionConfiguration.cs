using TickestPristine.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Roles;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.HasKey(permission => permission.Code);

        builder.Property(permission => permission.Code).HasMaxLength(100);

        builder.Property(permission => permission.Name).HasMaxLength(100);

        builder.Property(permission => permission.Description).HasMaxLength(500);

        builder.Property(permission => permission.Group).HasMaxLength(100);

        builder.Property(permission => permission.IsAdministrative);

        builder.HasData(Permission.All);
    }
}
