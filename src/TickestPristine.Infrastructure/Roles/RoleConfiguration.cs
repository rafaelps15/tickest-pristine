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
        // user_roles e role_permissions referenciam o id (spec 036): o nome pode mudar sem quebrar os vínculos.
        builder.HasKey(r => r.Id);

        // citext: o nome é único sem diferenciar maiúsculas de minúsculas. O citext não tem tamanho, então o limite de
        // 100 caracteres fica no validador e numa restrição de checagem no banco.
        builder.Property(r => r.Name).HasColumnType("citext");

        builder.HasIndex(r => r.Name).IsUnique();

        builder.ToTable(table => table.HasCheckConstraint("ck_roles_name_max_length", "char_length(name) <= 100"));

        // Token de concorrência só no banco, incrementado pelo AdministratorGuard quando um administrador perde o acesso.
        builder.Property<int>("Version").IsConcurrencyToken().HasDefaultValue(0);

        builder.HasData(
            Create(RoleNames.Administrator),
            Create(RoleNames.Agent),
            Create(RoleNames.Collaborator));
    }

    private static Role Create(string name) => new() { Id = DeterministicGuid.Create($"role:{name}"), Name = name };
}
