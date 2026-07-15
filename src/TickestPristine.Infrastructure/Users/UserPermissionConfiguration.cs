using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Users;

internal sealed class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.HasKey(userPermission => userPermission.Id);

        builder.Property(userPermission => userPermission.PermissionCode).HasMaxLength(100);

        builder.HasIndex(userPermission => new { userPermission.UserId, userPermission.PermissionCode }).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(userPermission => userPermission.UserId);
    }
}
