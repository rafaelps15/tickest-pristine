using TickestPristine.Application.SlaCalendars;
using TickestPristine.Application.Tickets.GetSla;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Tickets;

/// <summary>
/// Regras dos cronômetros de SLA do chamado: criação com prazo e alerta no calendário do plano, conclusão, parada,
/// novo ciclo de resolução e o estado, o consumido e o restante calculados num instante. Não há pausa: o consumido é o
/// tempo útil entre o início e o fim do cronômetro (ou o instante da leitura, se ainda estiver em aberto).
/// </summary>
internal static class TicketSlaClock
{
    // O cronômetro entra em risco ao consumir 75% da meta em tempo útil.
    public const double WarningRatio = 0.75;

    /// <summary>
    /// Cronômetros do ciclo 1, iniciados na abertura: primeira resposta (quando o plano tem essa meta) e resolução.
    /// </summary>
    public static List<TicketSlaTimer> Start(
        Guid ticketId,
        TicketSlaPlan plan,
        BusinessCalendar calendar,
        DateTime startedAtUtc)
    {
        var timers = new List<TicketSlaTimer>();

        if (plan.FirstResponseMinutes is { } firstResponseMinutes)
        {
            timers.Add(CreateTimer(ticketId, SlaMetric.FirstResponse, 1, firstResponseMinutes, plan.CalendarId, calendar, startedAtUtc));
        }

        timers.Add(CreateTimer(ticketId, SlaMetric.Resolution, 1, plan.ResolutionMinutes, plan.CalendarId, calendar, startedAtUtc));

        return timers;
    }

    /// <summary>
    /// Novo ciclo de resolução, com a meta integral do plano, iniciado na reabertura. Os ciclos anteriores não mudam.
    /// </summary>
    public static TicketSlaTimer StartResolutionCycle(
        Guid ticketId,
        TicketSlaPlan plan,
        int cycle,
        BusinessCalendar calendar,
        DateTime startedAtUtc) =>
        CreateTimer(ticketId, SlaMetric.Resolution, cycle, plan.ResolutionMinutes, plan.CalendarId, calendar, startedAtUtc);

    public static bool IsOpen(TicketSlaTimer timer) => timer.CompletedOnUtc is null && timer.StoppedOnUtc is null;

    /// <summary>
    /// Conclui o cronômetro, se ainda estiver em aberto; conclusão e parada já gravadas não mudam.
    /// </summary>
    public static void Complete(TicketSlaTimer timer, DateTime completedAtUtc)
    {
        if (IsOpen(timer))
        {
            timer.CompletedOnUtc = completedAtUtc;
        }
    }

    /// <summary>
    /// Para o cronômetro, se ainda estiver em aberto: parado não conta como violação.
    /// </summary>
    public static void Stop(TicketSlaTimer timer, DateTime stoppedAtUtc)
    {
        if (IsOpen(timer))
        {
            timer.StoppedOnUtc = stoppedAtUtc;
        }
    }

    /// <summary>
    /// Cronômetro do ciclo mais recente do objetivo, ou nulo se o objetivo não tiver cronômetro.
    /// </summary>
    public static TicketSlaTimer? CurrentCycle(IEnumerable<TicketSlaTimer> timers, SlaMetric metric) =>
        timers.Where(t => t.Metric == metric).MaxBy(t => t.Cycle);

    /// <summary>
    /// Estado no instante informado: em aberto, em andamento antes do alerta, em risco do alerta até o prazo e
    /// violado a partir do prazo; concluído até o prazo (inclusive), cumprido; depois dele, perdido; parado, parado.
    /// </summary>
    public static SlaObjectiveState GetState(TicketSlaTimer timer, DateTime nowUtc)
    {
        if (timer.CompletedOnUtc is { } completedAt)
        {
            return completedAt <= timer.DueOnUtc ? SlaObjectiveState.Met : SlaObjectiveState.Missed;
        }

        if (timer.StoppedOnUtc is not null)
        {
            return SlaObjectiveState.Stopped;
        }

        if (nowUtc >= timer.DueOnUtc)
        {
            return SlaObjectiveState.Breached;
        }

        return nowUtc >= timer.WarningOnUtc ? SlaObjectiveState.AtRisk : SlaObjectiveState.Running;
    }

    /// <summary>
    /// Tempo útil consumido entre o início e o menor instante entre agora, a conclusão e a parada.
    /// </summary>
    public static TimeSpan GetConsumed(TicketSlaTimer timer, BusinessCalendar calendar, DateTime nowUtc)
    {
        DateTime endUtc = new[] { nowUtc, timer.CompletedOnUtc ?? DateTime.MaxValue, timer.StoppedOnUtc ?? DateTime.MaxValue }.Min();

        return calendar.BusinessTimeBetween(timer.StartedAtUtc, endUtc);
    }

    /// <summary>
    /// Minutos inteiros de uma duração: a fração de minuto é descartada.
    /// </summary>
    public static int ToWholeMinutes(TimeSpan duration) => (int)(duration.Ticks / TimeSpan.TicksPerMinute);

    /// <summary>
    /// Restante da meta depois do consumido em minutos inteiros; nunca negativo.
    /// </summary>
    public static int GetRemainingMinutes(int targetMinutes, int consumedMinutes) => Math.Max(0, targetMinutes - consumedMinutes);

    /// <summary>
    /// Itens do bloco <c>sla</c>: todos os ciclos de cada objetivo, em ordem de objetivo e ciclo, com estado, consumido e
    /// restante no instante informado. Objetivo sem cronômetro vira um único item NotApplicable, como ciclo corrente.
    /// </summary>
    public static List<TicketSlaObjectiveResponse> ToObjectives(
        IReadOnlyCollection<TicketSlaTimer> timers,
        BusinessCalendar calendar,
        DateTime nowUtc) =>
        [.. new[] { SlaMetric.FirstResponse, SlaMetric.Resolution }.SelectMany(metric => ToObjectives(metric, timers, calendar, nowUtc))];

    /// <summary>
    /// Nome dos calendários definidos em código, que não têm linha no banco; nulo para os demais.
    /// </summary>
    public static string? CodeCalendarName(Guid calendarId)
    {
        if (calendarId == SlaCalendarIds.Default)
        {
            return DefaultSlaCalendar.Name;
        }

        return calendarId == SlaCalendarIds.Continuous ? ContinuousSlaCalendar.Name : null;
    }

    private static IEnumerable<TicketSlaObjectiveResponse> ToObjectives(
        SlaMetric metric,
        IReadOnlyCollection<TicketSlaTimer> timers,
        BusinessCalendar calendar,
        DateTime nowUtc)
    {
        TicketSlaTimer[] cycles = [.. timers.Where(t => t.Metric == metric).OrderBy(t => t.Cycle)];

        if (cycles.Length == 0)
        {
            return
            [
                new TicketSlaObjectiveResponse
                {
                    Metric = metric,
                    Cycle = 1,
                    State = SlaObjectiveState.NotApplicable,
                    IsCurrentCycle = true
                }
            ];
        }

        int currentCycle = cycles[^1].Cycle;

        return cycles.Select(timer =>
        {
            int consumedMinutes = ToWholeMinutes(GetConsumed(timer, calendar, nowUtc));

            return new TicketSlaObjectiveResponse
            {
                Metric = metric,
                Cycle = timer.Cycle,
                State = GetState(timer, nowUtc),
                TargetMinutes = timer.TargetMinutes,
                StartedAtUtc = timer.StartedAtUtc,
                WarningAtUtc = timer.WarningOnUtc,
                DueAtUtc = timer.DueOnUtc,
                CompletedAtUtc = timer.CompletedOnUtc,
                StoppedAtUtc = timer.StoppedOnUtc,
                ConsumedMinutes = consumedMinutes,
                RemainingMinutes = GetRemainingMinutes(timer.TargetMinutes, consumedMinutes),
                IsCurrentCycle = timer.Cycle == currentCycle
            };
        });
    }

    private static TicketSlaTimer CreateTimer(
        Guid ticketId,
        SlaMetric metric,
        int cycle,
        int targetMinutes,
        Guid calendarId,
        BusinessCalendar calendar,
        DateTime startedAtUtc)
    {
        // Em ticks, para o alerta cair no segundo exato (ex.: 75% de 30 minutos = 22 minutos e 30 segundos).
        var target = TimeSpan.FromTicks(TimeSpan.TicksPerMinute * targetMinutes);
        var warning = TimeSpan.FromTicks((long)(target.Ticks * WarningRatio));

        return new TicketSlaTimer
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            Metric = metric,
            Cycle = cycle,
            TargetMinutes = targetMinutes,
            CalendarId = calendarId,
            StartedAtUtc = startedAtUtc,
            WarningOnUtc = calendar.AddBusinessTime(startedAtUtc, warning),
            DueOnUtc = calendar.AddBusinessTime(startedAtUtc, target)
        };
    }
}
