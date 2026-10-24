using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Tickets;

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.HasKey(t => t.Id);

        // citext: a busca do painel não diferencia maiúsculas de minúsculas.
        builder.Property(t => t.Title).HasMaxLength(200).HasColumnType("citext");

        builder.Property(t => t.Description).HasMaxLength(500);

        // Enums gravados pelo nome (ex.: "InProgress"), legíveis no banco; a API continua recebendo e devolvendo o número.
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20);

        builder.HasQueryFilter(t => t.DeletedAtUtc == null);

        // Token de concorrência sombra mapeado no xmin do PostgreSQL: de duas mudanças simultâneas no mesmo chamado
        // (ex.: resolver e cancelar), só a primeira salva; a segunda falha com conflito em vez de sobrescrever a outra.
        builder.Property<uint>("Version").IsRowVersion();

        // Painel de SLA: filtra por status e ordena pelo prazo.
        builder.HasIndex(t => new { t.Status, t.DueAtUtc });

        // Todos os chamados: ordena do mais novo para o mais antigo, com o id para desempatar.
        builder.HasIndex(t => new { t.CreatedAtUtc, t.Id })
            .IsDescending(true, true);

        // "Meus chamados": filtra pelo solicitante e ordena do mais novo para o mais antigo, com o id para desempatar.
        builder.HasIndex(t => new { t.CreatedByUserId, t.CreatedAtUtc, t.Id })
            .IsDescending(false, true, true);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.AssignedToUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Sector>()
            .WithMany()
            .HasForeignKey(t => t.SectorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
