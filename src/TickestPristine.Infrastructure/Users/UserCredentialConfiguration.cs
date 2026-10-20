using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Users;

internal sealed class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    public void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        builder.HasKey(credential => credential.Id);

        builder.Property(credential => credential.PasswordHash).HasMaxLength(500);

        // Credencial nova sem carimbo informado recebe um gerado pelo banco.
        builder.Property(credential => credential.SecurityStamp).HasDefaultValueSql("gen_random_uuid()");

        builder.HasIndex(credential => credential.UserId).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(credential => credential.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
