using TickestPristine.Domain.Permissions;
using TickestPristine.Domain.Roles;
using TickestPristine.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Roles;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");
        builder.HasKey(permission => permission.Id);
        builder.Property(permission => permission.Id).ValueGeneratedNever();
        builder.HasAlternateKey(permission => permission.Code);
        builder.Property(permission => permission.Code).HasMaxLength(100);
        builder.Property(permission => permission.Name).HasMaxLength(100);
        builder.Property(permission => permission.Description).HasMaxLength(500);
        builder.Property(permission => permission.Group).HasMaxLength(100);
        builder.Property(permission => permission.IsAdministrative);

        builder.HasData(PermissionCatalog.All.Select(entry => new
        {
            Id = DeterministicGuid.Create($"permission:{entry.Code}"),
            entry.Code,
            entry.Name,
            entry.Description,
            entry.Group,
            entry.IsAdministrative
        }));
    }
}
