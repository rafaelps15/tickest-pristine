using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Roles;
using TickestPristine.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Roles;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.HasKey(permission => permission.Id);

        // O código continua sendo a chave que role_permissions referencia (e a que vai nas claims e nos endpoints).
        builder.HasAlternateKey(permission => permission.Code);

        builder.Property(permission => permission.Code).HasMaxLength(100);

        builder.Property(permission => permission.Name).HasMaxLength(100);

        builder.Property(permission => permission.Description).HasMaxLength(500);

        builder.Property(permission => permission.Group).HasMaxLength(100);

        builder.Property(permission => permission.IsAdministrative);

        builder.HasData(PermissionCatalog.All.Select(entry => new Permission
        {
            Id = DeterministicGuid.Create($"permission:{entry.Code}"),
            Code = entry.Code,
            Name = entry.Name,
            Description = entry.Description,
            Group = entry.Group,
            IsAdministrative = entry.IsAdministrative
        }));
    }
}
