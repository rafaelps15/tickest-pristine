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

        // Permite no máximo uma role padrão e uma role de administrador.
        builder.HasIndex(role => role.IsDefault).IsUnique().HasFilter("is_default");

        builder.HasIndex(role => role.IsAdministrator).IsUnique().HasFilter("is_administrator");

        builder.Property(role => role.Version).IsConcurrencyToken();

        // Roles padrão: dados de referência versionados pelas migrations.
        builder.HasData(Role.Administrator, Role.Agent, Role.Collaborator);
    }
}
