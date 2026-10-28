using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Tickets;

internal sealed class TicketSlaTimerConfiguration : IEntityTypeConfiguration<TicketSlaTimer>
{
    public void Configure(EntityTypeBuilder<TicketSlaTimer> builder)
    {
        builder.HasKey(t => t.Id);

        // Objetivo gravado pelo nome (ex.: "FirstResponse"), legível no banco; a API continua devolvendo o número.
        builder.Property(t => t.Metric).HasConversion<string>().HasMaxLength(20);

        // Causa da conclusão também pelo nome (ex.: "Message"); nula enquanto o cronômetro não foi concluído (spec 027).
        builder.Property(t => t.CompletionCause).HasConversion<string>().HasMaxLength(20);

        // A conclusão e a parada são os tokens de concorrência: de duas conclusões simultâneas, só a primeira salva.
        // Não é o xmin da linha para que outras colunas do cronômetro possam ser gravadas sem conflitar com elas.
        builder.Property(t => t.CompletedOnUtc).IsConcurrencyToken();

        builder.Property(t => t.StoppedOnUtc).IsConcurrencyToken();

        // Um cronômetro por objetivo e ciclo; a migration dos chamados existentes depende deste índice para não duplicar.
        builder.HasIndex(t => new { t.TicketId, t.Metric, t.Cycle }).IsUnique();

        // Fila do monitoramento: só os cronômetros com ação pendente entram no índice.
        builder.HasIndex(t => t.NextActionOnUtc).HasFilter("next_action_on_utc IS NOT NULL");

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(t => t.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
