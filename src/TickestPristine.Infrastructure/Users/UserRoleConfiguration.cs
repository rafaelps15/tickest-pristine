using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Users;

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.HasKey(userRole => userRole.Id);

        builder.HasIndex(userRole => new { userRole.UserId, userRole.RoleId }).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(userRole => userRole.UserId);

        // Restrict: uma role atribuída a alguém não sai do banco, nem numa exclusão concorrente com a atribuição (spec 036).
        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(userRole => userRole.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
