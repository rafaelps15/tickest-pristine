using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Roles;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(role => role.Id);

        builder.Property(role => role.Name).HasMaxLength(100);

        builder.HasIndex(role => role.Name).IsUnique();

        // Permite no máximo uma role padrão.
        builder.HasIndex(role => role.IsDefault).IsUnique().HasFilter("is_default");

        builder.Property(role => role.Version).IsConcurrencyToken();

        // Só as roles de que o código depende ficam nas migrations; as iniciais editáveis vêm do DefaultRolesSeeder.
        builder.HasData(new Role { Id = SystemRoles.AdministratorId, Name = SystemRoles.AdministratorName });
    }
}
