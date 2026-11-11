using TickestPristine.Application.Roles;
using TickestPristine.Domain.Roles;
using TickestPristine.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Roles;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Id).ValueGeneratedNever();
        builder.HasAlternateKey(role => role.Name);
        builder.Property(role => role.Name).HasMaxLength(50);
        builder.Property(r => r.Version).IsConcurrencyToken().HasDefaultValue(0);
        builder.HasMany(role => role.Permissions)
            .WithOne()
            .HasForeignKey(permission => permission.RoleName)
            .HasPrincipalKey(role => role.Name);
        builder.Navigation(role => role.Permissions)
            .HasField("_permissions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasData(
            CreateSeed(RoleNames.Administrator),
            CreateSeed(RoleNames.Agent),
            CreateSeed(RoleNames.Collaborator));
    }

    private static object CreateSeed(string name) => new
    {
        Id = DeterministicGuid.Create($"role:{name}"),
        Name = name
    };
}
