using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Application.SlaCalendars;

/// <summary>
/// Período informado em datas locais de São Paulo (o fuso do <see cref="DefaultSlaCalendar"/>), inclusivo nas duas
/// pontas, convertido para limites UTC: <see cref="FromUtc"/> é o início do primeiro dia (inclusivo) e
/// <see cref="UntilUtc"/> é o início do dia seguinte ao último (exclusivo). Uma ponta não informada fica nula. A
/// conversão usa o fuso IANA, e não um deslocamento fixo, para continuar certa se o horário de verão voltar (spec 025).
/// </summary>
public sealed record LocalDatePeriod(DateTime? FromUtc, DateTime? UntilUtc)
{
    private static readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById(DefaultSlaCalendar.TimeZoneId);

    public static LocalDatePeriod FromLocalDates(DateOnly? from, DateOnly? to) => new(
        from is null ? null : StartOfDayUtc(from.Value),
        to is null ? null : StartOfDayUtc(to.Value.AddDays(1)));

    /// <summary>
    /// Início depois do fim; com uma das pontas ausente, o período nunca é invertido.
    /// </summary>
    public static bool IsInverted(DateOnly? from, DateOnly? to) => from > to;

    /// <summary>
    /// Data local de São Paulo de um instante UTC (por exemplo, o "hoje" das janelas padrão do volume, spec 028).
    /// </summary>
    public static DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZone));

    private static DateTime StartOfDayUtc(DateOnly day) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), TimeZone);
}
