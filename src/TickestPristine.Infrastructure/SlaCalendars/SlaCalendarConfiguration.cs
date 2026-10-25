using System.Text.Json;
using TickestPristine.Domain.SlaCalendars;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.SlaCalendars;

internal sealed class SlaCalendarConfiguration : IEntityTypeConfiguration<SlaCalendar>
{
    private static readonly JsonSerializerOptions WorkScheduleJsonOptions = new(JsonSerializerDefaults.General);

    public void Configure(EntityTypeBuilder<SlaCalendar> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(100);

        // Identificador IANA do fuso (ex.: America/Sao_Paulo).
        builder.Property(c => c.TimeZoneId).HasMaxLength(64);

        // Propriedade comum convertida para jsonb, e não ToJson: assim a grade faz parte das propriedades da própria
        // entidade, e o interceptor de auditoria registra a alteração dela (DEC-15 da spec 004). O comparador faz o
        // EF perceber mudanças feitas dentro da lista.
        builder.Property(c => c.WorkSchedule)
            .HasColumnType("jsonb")
            .HasConversion(
                schedule => JsonSerializer.Serialize(schedule, WorkScheduleJsonOptions),
                json => JsonSerializer.Deserialize<List<SlaWorkPeriod>>(json, WorkScheduleJsonOptions) ?? new List<SlaWorkPeriod>(),
                new ValueComparer<List<SlaWorkPeriod>>(
                    (left, right) => ReferenceEquals(left, right) || left != null && right != null && left.SequenceEqual(right),
                    schedule => schedule.Aggregate(0, (hash, period) => HashCode.Combine(hash, period.GetHashCode())),
                    schedule => schedule.ToList()));
    }
}
