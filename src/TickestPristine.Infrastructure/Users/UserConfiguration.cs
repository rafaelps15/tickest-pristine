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
        builder.Property(u => u.Email).HasColumnType("citext");
        builder.Property(u => u.FirstName).HasColumnType("citext");
        builder.Property(u => u.LastName).HasColumnType("citext");

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.Code).HasMaxLength(30);

        builder.HasIndex(u => u.Code).IsUnique();

        builder.Property(u => u.IsActive)
            .IsRequired()
            .HasDefaultValue(true);
    }
}
