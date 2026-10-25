using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Application.SlaCalendars;

/// <summary>
/// Calendário padrão "Expediente", definido em código e sem linha no banco (DEC-5 e DEC-7 da spec 004): segunda a
/// sexta, das 08:00 às 18:00 de São Paulo, sem expediente nos feriados de <see cref="BrazilianNationalHolidays"/> de
/// qualquer ano. Mudar a grade ou os feriados exige novo deploy.
/// </summary>
public static class DefaultSlaCalendar
{
    public const string Name = "Expediente";

    public const string TimeZoneId = "America/Sao_Paulo";

    public static IReadOnlyList<SlaWorkPeriod> WorkSchedule { get; } =
    [
        .. new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
            .Select(day => new SlaWorkPeriod(day, new TimeOnly(8, 0), new TimeOnly(18, 0)))
    ];

    public static BusinessCalendar Create() => new(
        WorkSchedule,
        TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId),
        is24x7: false,
        year => BrazilianNationalHolidays.For(year).Select(h => h.Date).ToHashSet());
}
