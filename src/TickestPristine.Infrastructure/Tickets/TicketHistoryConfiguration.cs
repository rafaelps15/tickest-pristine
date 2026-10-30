using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Tickets;

internal sealed class TicketHistoryConfiguration : IEntityTypeConfiguration<TicketHistory>
{
    public void Configure(EntityTypeBuilder<TicketHistory> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id).ValueGeneratedNever();

        // Ação gravada pelo nome (ex.: "StatusChanged"), legível no banco; a API continua devolvendo o número.
        builder.Property(h => h.Action).HasConversion<string>().HasMaxLength(30);

        builder.Property(h => h.OldValue).HasMaxLength(500);

        builder.Property(h => h.NewValue).HasMaxLength(500);

        // O histórico é imutável, por isso não tem filtro de exclusão lógica.

        // Paginação por cursor composto (data + id), do mais novo para o mais antigo, dentro de cada chamado.
        builder.HasIndex(h => new { h.TicketId, h.OccurredAtUtc, h.Id })
            .IsDescending(false, true, true);

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(h => h.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(h => h.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
