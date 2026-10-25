namespace TickestPristine.Infrastructure.SlaCalendars;

internal static class SlaCalendarCacheKeys
{
    internal static string ForCalendar(Guid calendarId) => $"sla-calendar-{calendarId}";
}
