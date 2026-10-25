using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.SlaCalendars;

public sealed class SlaCalendarHoliday : Entity, IAuditable
{
    public Guid Id { get; set; }
    public Guid SlaCalendarId { get; set; }
    public DateOnly Date { get; set; }
    public string Description { get; set; }
}
