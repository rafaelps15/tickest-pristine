using System.Text.Json;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Tickets;

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    // Nomes das propriedades como no C# (ex.: "ResolutionMinutes"); a migration dos chamados existentes monta o mesmo JSON.
    private static readonly JsonSerializerOptions SlaPlanJsonOptions = new(JsonSerializerDefaults.General);

    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.HasKey(t => t.Id);

        // citext: a busca do painel não diferencia maiúsculas de minúsculas.
        builder.Property(t => t.Title).HasMaxLength(200).HasColumnType("citext");

        // citext também: a busca de "Meus chamados" procura no título e na descrição sem diferenciar maiúsculas (spec 019).
        builder.Property(t => t.Description).HasMaxLength(500).HasColumnType("citext");

        // O citext não guarda tamanho: os limites do título e da descrição (de 10 a 500 caracteres) valem no banco pelas
        // constraints.
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_tickets_title_length", "char_length(title) <= 200");
            t.HasCheckConstraint("ck_tickets_description_length", "char_length(description) BETWEEN 10 AND 500");
        });

        // Enums gravados pelo nome (ex.: "InProgress"), legíveis no banco; a API continua recebendo e devolvendo o número.
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20);

        // Situação de espera pelo nome (ex.: "Requester"), calculada pelo chamado (spec 029).
        builder.Property(t => t.WaitingOn).HasConversion<string>().HasMaxLength(20);

        // Propriedade comum convertida para jsonb, e não ToJson, como a grade do calendário de SLA: o plano entra no
        // registro de auditoria do chamado. O plano é imutável, então a igualdade do record basta para o EF.
        builder.Property(t => t.SlaPlan)
            .HasColumnType("jsonb")
            .HasConversion(
                plan => JsonSerializer.Serialize(plan, SlaPlanJsonOptions),
                json => JsonSerializer.Deserialize<TicketSlaPlan>(json, SlaPlanJsonOptions)!);

        builder.HasQueryFilter(t => t.DeletedAtUtc == null);

        // Token de concorrência sombra mapeado no xmin do PostgreSQL: de duas mudanças simultâneas no mesmo chamado
        // (ex.: resolver e cancelar), só a primeira salva; a segunda falha com conflito em vez de sobrescrever a outra.
        builder.Property<uint>("Version").IsRowVersion();

        // Painel de SLA: filtra por status e ordena pelo prazo.
        builder.HasIndex(t => new { t.Status, t.DueAtUtc });

        // Filtro e contagem por situação de espera: só os chamados ativos têm situação, então só eles entram (spec 029).
        builder.HasIndex(t => t.WaitingOn).HasFilter("waiting_on IS NOT NULL");

        // Todos os chamados: ordena do mais novo para o mais antigo, com o id para desempatar.
        builder.HasIndex(t => new { t.CreatedAtUtc, t.Id })
            .IsDescending(true, true);

        // "Meus chamados": filtra pelo solicitante e ordena pela última atividade, da mais recente para a mais antiga,
        // com o id para desempatar (spec 015).
        builder.HasIndex(t => new { t.CreatedByUserId, t.LastActivityAtUtc, t.Id })
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
