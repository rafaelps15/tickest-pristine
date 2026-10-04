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

        // citext: "Suporte" e "suporte" contam como o mesmo nome no índice único.
        builder.Property(d => d.Name).HasMaxLength(100).HasColumnType("citext");

        // Nome único só entre os ativos: um departamento desativado não impede recriar o mesmo nome.
        builder.HasIndex(d => d.Name).IsUnique().HasFilter("is_active");

        builder.Property(d => d.Description).HasMaxLength(500);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.ResponsibleUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
