using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Departments;

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name).HasMaxLength(100);

        builder.Property(d => d.Description).HasMaxLength(500);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.ResponsibleUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
