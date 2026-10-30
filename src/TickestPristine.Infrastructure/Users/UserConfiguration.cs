using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Users;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        // citext: busca, login e e-mail único não diferenciam maiúsculas de minúsculas.
        builder.Property(u => u.Email).HasMaxLength(254).HasColumnType("citext");
        builder.Property(u => u.FirstName).HasMaxLength(100).HasColumnType("citext");
        builder.Property(u => u.LastName).HasMaxLength(100).HasColumnType("citext");

        // O citext não guarda tamanho: os limites do e-mail e do nome valem no banco pelas constraints.
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_users_email_length", "char_length(email) <= 254");
            t.HasCheckConstraint("ck_users_first_name_length", "char_length(first_name) <= 100");
            t.HasCheckConstraint("ck_users_last_name_length", "char_length(last_name) <= 100");
        });

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.Code).HasMaxLength(30);

        builder.HasIndex(u => u.Code).IsUnique();

        // Listagem paginada de usuários: segue a ordenação da consulta (nome, sobrenome e a chave como desempate).
        builder.HasIndex(u => new { u.FirstName, u.LastName, u.Id });

        builder.Property(u => u.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Setor em que o usuário atende, opcional. Desativar o setor não apaga o vínculo (é exclusão lógica), e um setor
        // com usuários vinculados não pode ser excluído fisicamente.
        builder.HasOne<Sector>()
            .WithMany()
            .HasForeignKey(u => u.SectorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => u.SectorId);
    }
}
