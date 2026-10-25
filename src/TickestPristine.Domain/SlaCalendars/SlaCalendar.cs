using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.SlaCalendars;

public sealed class SlaCalendar : Entity, IAuditable
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string TimeZoneId { get; set; }
    public bool Is24x7 { get; set; }
    public List<SlaWorkPeriod> WorkSchedule { get; set; } = [];
}
