namespace TickestPristine.Application.Tickets.GetSla;

public sealed class TicketSlaPlanResponse
{
    public Guid? PolicyId { get; set; }
    public string PolicyName { get; set; }
    public int PolicyVersion { get; set; }
    public Guid CalendarId { get; set; }
    public string CalendarName { get; set; }
    public bool IsLegacy { get; set; }
}
