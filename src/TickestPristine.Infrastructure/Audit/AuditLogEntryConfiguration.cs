using TickestPristine.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Audit;

internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.HasKey(x => x.Id);

        // Os valores já chegam serializados em JSON; jsonb permite consultar dentro deles no PostgreSQL.
        builder.Property(x => x.OldValues).HasColumnType("jsonb");

        builder.Property(x => x.NewValues).HasColumnType("jsonb");

        // Sem chave estrangeira para users: o registro de auditoria sobrevive ao usuário que fez a mudança.

        builder.HasIndex(x => x.EntityName);
        builder.HasIndex(x => x.Timestamp);
        builder.HasIndex(x => new { x.EntityName, x.EntityId });
    }
}
