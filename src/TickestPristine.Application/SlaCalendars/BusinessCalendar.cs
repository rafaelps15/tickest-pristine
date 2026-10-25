using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Application.SlaCalendars;

/// <summary>
/// Cálculo de tempo útil sobre um calendário de expediente: as faixas de cada dia da semana valem em hora local do
/// fuso, e os feriados e os dias sem faixa ficam fora. Os instantes de entrada e de saída são <see cref="DateTime"/>
/// em UTC; as durações, <see cref="TimeSpan"/> (DEC-11 da spec 004). Num calendário 24x7, as contas são corridas.
/// Imutável e seguro para uso concorrente.
/// </summary>
public sealed class BusinessCalendar(
    IEnumerable<SlaWorkPeriod> workSchedule,
    TimeZoneInfo timeZone,
    bool is24x7,
    Func<int, IReadOnlySet<DateOnly>> holidaysForYear)
{
    // Limite de dias que a soma e a subtração percorrem à procura de tempo útil (DEC-8 e DEC-12 da spec 004).
    private const int MaxSearchDays = 366;

    // Faixas agrupadas por dia da semana, em ordem de início.
    private readonly Dictionary<DayOfWeek, SlaWorkPeriod[]> _periodsByDay = workSchedule
        .GroupBy(p => p.DayOfWeek)
        .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Start).ToArray());

    /// <summary>
    /// Instante em que a duração útil, contada a partir de <paramref name="startUtc"/>, se esgota. Se o tempo acabar
    /// exatamente no fim de uma faixa, o resultado é esse fim (DEC-9).
    /// </summary>
    public DateTime AddBusinessTime(DateTime startUtc, TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);

        if (duration == TimeSpan.Zero)
        {
            return startUtc;
        }

        if (is24x7)
        {
            return startUtc + duration;
        }

        TimeSpan remaining = duration;
        DateOnly firstDay = ToLocalDate(startUtc);

        for (int offset = 0; offset <= MaxSearchDays; offset++)
        {
            foreach ((DateTime periodStartUtc, DateTime periodEndUtc) in PeriodsOf(firstDay.AddDays(offset)))
            {
                DateTime from = periodStartUtc > startUtc ? periodStartUtc : startUtc;

                if (from >= periodEndUtc)
                {
                    continue;
                }

                TimeSpan available = periodEndUtc - from;

                if (remaining <= available)
                {
                    return from + remaining;
                }

                remaining -= available;
            }
        }

        throw NoBusinessTime();
    }

    /// <summary>
    /// Instante a partir do qual, contando para a frente, faltaria exatamente a duração útil até
    /// <paramref name="endUtc"/>. Se o tempo acabar exatamente no início de uma faixa, o resultado é esse início (DEC-9).
    /// </summary>
    public DateTime SubtractBusinessTime(DateTime endUtc, TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);

        if (duration == TimeSpan.Zero)
        {
            return endUtc;
        }

        if (is24x7)
        {
            return endUtc - duration;
        }

        TimeSpan remaining = duration;
        DateOnly lastDay = ToLocalDate(endUtc);

        for (int offset = 0; offset <= MaxSearchDays; offset++)
        {
            foreach ((DateTime periodStartUtc, DateTime periodEndUtc) in PeriodsOf(lastDay.AddDays(-offset)).Reverse())
            {
                DateTime until = periodEndUtc < endUtc ? periodEndUtc : endUtc;

                if (until <= periodStartUtc)
                {
                    continue;
                }

                TimeSpan available = until - periodStartUtc;

                if (remaining <= available)
                {
                    return until - remaining;
                }

                remaining -= available;
            }
        }

        throw NoBusinessTime();
    }

    /// <summary>
    /// Tempo útil entre dois instantes; zero se o fim não for posterior ao início. Percorre só o intervalo dado, sem
    /// o limite de dias da soma e da subtração (DEC-12).
    /// </summary>
    public TimeSpan BusinessTimeBetween(DateTime startUtc, DateTime endUtc)
    {
        if (endUtc <= startUtc)
        {
            return TimeSpan.Zero;
        }

        if (is24x7)
        {
            return endUtc - startUtc;
        }

        TimeSpan total = TimeSpan.Zero;
        DateOnly lastDay = ToLocalDate(endUtc);

        for (DateOnly day = ToLocalDate(startUtc); day <= lastDay; day = day.AddDays(1))
        {
            foreach ((DateTime periodStartUtc, DateTime periodEndUtc) in PeriodsOf(day))
            {
                DateTime from = periodStartUtc > startUtc ? periodStartUtc : startUtc;
                DateTime until = periodEndUtc < endUtc ? periodEndUtc : endUtc;

                if (until > from)
                {
                    total += until - from;
                }
            }
        }

        return total;
    }

    // Faixas do dia em UTC, em ordem; nenhuma em feriado ou em dia sem faixa.
    private IEnumerable<(DateTime StartUtc, DateTime EndUtc)> PeriodsOf(DateOnly day)
    {
        if (!_periodsByDay.TryGetValue(day.DayOfWeek, out SlaWorkPeriod[]? periods) || IsHoliday(day))
        {
            return [];
        }

        return periods.Select(p => (ToUtc(day, p.Start), ToUtc(day, p.End)));
    }

    private bool IsHoliday(DateOnly day) => holidaysForYear(day.Year).Contains(day);

    private DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone));

    private DateTime ToUtc(DateOnly day, TimeOnly time) => TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(time), timeZone);

    private static InvalidOperationException NoBusinessTime() => new(
        $"O calendário não tem tempo útil em {MaxSearchDays} dias; confira a grade de expediente e os feriados.");
}
