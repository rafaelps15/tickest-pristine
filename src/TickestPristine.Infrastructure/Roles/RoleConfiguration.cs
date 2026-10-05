using TickestPristine.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Roles;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(r => r.Name);

        builder.Property(r => r.Name).HasMaxLength(50);

        // Token de concorrência só no banco, incrementado pelo AdministratorGuard quando um administrador perde o acesso.
        builder.Property<int>("Version").IsConcurrencyToken().HasDefaultValue(0);

        builder.HasData(
            Role.Administrator,
            Role.Agent,
            Role.Collaborator);
    }
}
