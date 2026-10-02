using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Users;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(refreshToken => refreshToken.Id);

        // SHA-256 em hexadecimal: 64 caracteres.
        builder.Property(refreshToken => refreshToken.TokenHash).HasMaxLength(64);

        builder.HasIndex(refreshToken => refreshToken.TokenHash).IsUnique();

        // Duas renovações simultâneas com o mesmo token: só a primeira grava, a outra recebe conflito.
        builder.Property(refreshToken => refreshToken.TokenHash).IsConcurrencyToken();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(refreshToken => refreshToken.UserId);
    }
}
