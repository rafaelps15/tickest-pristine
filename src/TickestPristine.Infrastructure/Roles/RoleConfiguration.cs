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
        builder.HasKey(r => r.Id);

        // O nome continua sendo a chave que user_roles e role_permissions referenciam (e a que a API usa nas rotas).
        builder.HasAlternateKey(r => r.Name);

        builder.Property(r => r.Name).HasMaxLength(50);

        // Token de concorrência só no banco, incrementado pelo AdministratorGuard quando um administrador perde o acesso.
        builder.Property<int>("Version").IsConcurrencyToken().HasDefaultValue(0);

        builder.HasData(
            Create(RoleNames.Administrator),
            Create(RoleNames.Agent),
            Create(RoleNames.Collaborator));
    }

    private static Role Create(string name) => new() { Id = DeterministicGuid.Create($"role:{name}"), Name = name };
}
