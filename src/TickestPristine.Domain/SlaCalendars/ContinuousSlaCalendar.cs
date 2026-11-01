namespace TickestPristine.Domain.SlaCalendars;

/// <summary>
/// Calendário "24 horas", definido em código e sem linha no banco: todo instante conta, sem feriados. É o calendário
/// dos chamados abertos antes do plano de SLA, que mantêm o prazo em horas corridas.
/// </summary>
public static class ContinuousSlaCalendar
{
    public const string Name = "24 horas";

    public static BusinessCalendar Create() => new([], TimeZoneInfo.Utc, is24x7: true, _ => new HashSet<DateOnly>());
}
