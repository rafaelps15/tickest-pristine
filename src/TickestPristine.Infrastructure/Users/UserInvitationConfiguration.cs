using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Users;

internal sealed class UserInvitationConfiguration : IEntityTypeConfiguration<UserInvitation>
{
    public void Configure(EntityTypeBuilder<UserInvitation> builder)
    {
        builder.HasKey(invitation => invitation.Id);

        // SHA-256 em hexadecimal: 64 caracteres. A ativação procura o convite pelo hash do token recebido.
        builder.Property(invitation => invitation.TokenHash).HasMaxLength(64);

        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();

        // Revogação dos convites do usuário e contagem dos emitidos nas últimas 24 horas.
        builder.HasIndex(invitation => new { invitation.UserId, invitation.CreatedAtUtc });

        // Uso e revogação são tokens de concorrência: duas ativações simultâneas, ou uma ativação junto com uma
        // revogação, não gravam as duas.
        builder.Property(invitation => invitation.UsedAtUtc).IsConcurrencyToken();
        builder.Property(invitation => invitation.RevokedAtUtc).IsConcurrencyToken();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(invitation => invitation.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
