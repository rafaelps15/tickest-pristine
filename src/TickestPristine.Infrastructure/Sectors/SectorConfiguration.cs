using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Sectors;

internal sealed class SectorConfiguration : IEntityTypeConfiguration<Sector>
{
    public void Configure(EntityTypeBuilder<Sector> builder)
    {
        builder.HasKey(s => s.Id);

        // citext: "Suporte" e "suporte" contam como o mesmo nome no índice único.
        builder.Property(s => s.Name).HasMaxLength(100).HasColumnType("citext");

        // O citext não guarda tamanho: o limite do nome vale no banco pela constraint.
        builder.ToTable(t => t.HasCheckConstraint("ck_sectors_name_length", "char_length(name) <= 100"));

        // Nome único dentro do departamento, só entre os ativos.
        builder.HasIndex(s => new { s.DepartmentId, s.Name }).IsUnique().HasFilter("is_active");

        builder.Property(s => s.Description).HasMaxLength(500);

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(s => s.DepartmentId);
    }
}
