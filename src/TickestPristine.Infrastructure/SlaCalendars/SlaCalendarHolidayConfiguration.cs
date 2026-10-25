using TickestPristine.Domain.SlaCalendars;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.SlaCalendars;

internal sealed class SlaCalendarHolidayConfiguration : IEntityTypeConfiguration<SlaCalendarHoliday>
{
    public void Configure(EntityTypeBuilder<SlaCalendarHoliday> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Description).HasMaxLength(100);

        // No máximo um feriado por calendário e data.
        builder.HasIndex(h => new { h.SlaCalendarId, h.Date }).IsUnique();

        builder.HasOne<SlaCalendar>()
            .WithMany()
            .HasForeignKey(h => h.SlaCalendarId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
