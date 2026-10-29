using TickestPristine.Application.SlaCalendars;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetVolume;

/// <summary>
/// Inícios dos períodos do gráfico de volume, do mais antigo para o mais recente, em datas locais de São Paulo: o
/// próprio dia, a segunda-feira da semana ou o dia 1º do mês. Com período, os pontos vão do período do início ao do fim;
/// sem fim, ele é hoje; sem início, vale a janela padrão terminando no fim (30 dias, 12 semanas ou 12 meses). Mais de
/// <see cref="MaxPoints"/> pontos ou início depois do fim são recusados (spec 028, DEC-3, DEC-5 e DEC-6).
/// </summary>
internal static class TicketVolumePeriods
{
    internal const int MaxPoints = 366;

    internal static Result<IReadOnlyList<DateOnly>> Build(TicketVolumeGrouping groupBy, DateOnly? from, DateOnly? to, DateOnly today)
    {
        DateOnly last = Truncate(groupBy, to ?? today);
        DateOnly first = from is { } start ? Truncate(groupBy, start) : Step(groupBy, last, -(DefaultPoints(groupBy) - 1));

        if (LocalDatePeriod.IsInverted(from, to) || first > last)
        {
            return Result.Failure<IReadOnlyList<DateOnly>>(TicketErrors.InvalidPeriod);
        }

        var periods = new List<DateOnly>();

        for (DateOnly period = first; period <= last; period = Step(groupBy, period, 1))
        {
            if (periods.Count == MaxPoints)
            {
                return Result.Failure<IReadOnlyList<DateOnly>>(TicketErrors.VolumeTooManyPoints(MaxPoints));
            }

            periods.Add(period);
        }

        return Result.Success<IReadOnlyList<DateOnly>>(periods);
    }

    /// <summary>
    /// Início do período seguinte (ou anterior, com <paramref name="count"/> negativo).
    /// </summary>
    internal static DateOnly Step(TicketVolumeGrouping groupBy, DateOnly periodStart, int count) => groupBy switch
    {
        TicketVolumeGrouping.Week => periodStart.AddDays(7 * count),
        TicketVolumeGrouping.Month => periodStart.AddMonths(count),
        _ => periodStart.AddDays(count)
    };

    /// <summary>
    /// Unidade do <c>date_trunc</c> do PostgreSQL, que começa a semana na segunda-feira, como <see cref="Truncate"/>.
    /// </summary>
    internal static string SqlUnit(TicketVolumeGrouping groupBy) => groupBy switch
    {
        TicketVolumeGrouping.Week => "week",
        TicketVolumeGrouping.Month => "month",
        _ => "day"
    };

    private static DateOnly Truncate(TicketVolumeGrouping groupBy, DateOnly date) => groupBy switch
    {
        TicketVolumeGrouping.Week => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
        TicketVolumeGrouping.Month => new DateOnly(date.Year, date.Month, 1),
        _ => date
    };

    private static int DefaultPoints(TicketVolumeGrouping groupBy) => groupBy == TicketVolumeGrouping.Day ? 30 : 12;
}
