using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Infrastructure.SlaCalendars;

/// <summary>
/// Cópia serializável de um calendário do banco, guardada no <c>HybridCache</c>. O <c>BusinessCalendar</c> não vai
/// para o cache porque guarda uma função de feriados e um <see cref="TimeZoneInfo"/> (DEC-13 da spec 004).
/// </summary>
internal sealed record SlaCalendarSnapshot(
    string Name,
    string TimeZoneId,
    bool Is24x7,
    List<SlaWorkPeriod> WorkSchedule,
    List<DateOnly> Holidays);
