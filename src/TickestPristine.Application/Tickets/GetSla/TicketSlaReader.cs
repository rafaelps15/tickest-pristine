using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Sla;
using TickestPristine.Domain.SlaCalendars;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetSla;

/// <summary>
/// Monta o bloco <c>sla</c> do chamado, comum ao detalhe e ao <c>GET tickets/{id}/sla</c>: o plano gravado, o nome do
/// calendário e todos os ciclos de cada objetivo. Lê pela conexão que o handler já abriu e não confere o acesso, que é
/// do handler.
/// </summary>
internal sealed class TicketSlaReader(ISlaCalendarProvider slaCalendarProvider)
{
    /// <summary>
    /// Bloco <c>sla</c> no instante informado. Falha com <see cref="TicketErrors.NotFound"/> quando o chamado não existe
    /// ou foi excluído, com o erro do provedor de calendários quando o calendário do plano não existe mais, e com
    /// <see cref="SlaCalendarErrors.NotFound"/> quando o nome dele não é encontrado.
    /// </summary>
    public async Task<Result<TicketSlaResponse>> ReadAsync(
        DbConnection connection,
        Guid ticketId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        const string planSql =
            """
            SELECT t.sla_plan
            FROM public.tickets t
            WHERE t.id = @TicketId AND t.deleted_at_utc IS NULL
            """;

        TicketSlaPlan? plan = await connection.QuerySingleOrDefaultAsync<TicketSlaPlan>(
            new CommandDefinition(planSql, new { TicketId = ticketId }, cancellationToken: cancellationToken));

        if (plan is null)
        {
            return Result.Failure<TicketSlaResponse>(TicketErrors.NotFound(ticketId));
        }

        // Os cronômetros de todos os ciclos do chamado.
        const string sql =
            $"""
             SELECT
                 {TicketSql.SlaTimerColumns}
             FROM public.ticket_sla_timers tm
             WHERE tm.ticket_id = @TicketId
             """;

        List<TicketSlaTimer> timers = (await connection.QueryAsync<TicketSlaTimer>(
            new CommandDefinition(sql, new { TicketId = ticketId }, cancellationToken: cancellationToken))).AsList();

        Result<BusinessCalendar> calendar = await slaCalendarProvider.GetAsync(plan.CalendarId, cancellationToken);

        if (calendar.IsFailure)
        {
            return Result.Failure<TicketSlaResponse>(calendar.Error);
        }

        const string calendarNameSql =
            """
            SELECT c.name
            FROM public.sla_calendars c
            WHERE c.id = @CalendarId
            """;

        string? calendarName = CodeCalendarName(plan.CalendarId)
            ?? await connection.QuerySingleOrDefaultAsync<string>(
                new CommandDefinition(calendarNameSql, new { plan.CalendarId }, cancellationToken: cancellationToken));

        if (calendarName is null)
        {
            return Result.Failure<TicketSlaResponse>(SlaCalendarErrors.NotFound(plan.CalendarId));
        }

        return new TicketSlaResponse
        {
            ServerNowUtc = nowUtc,
            Plan = new TicketSlaPlanResponse
            {
                PolicyId = plan.PolicyId,
                PolicyName = plan.PolicyName,
                PolicyVersion = plan.PolicyVersion,
                CalendarId = plan.CalendarId,
                CalendarName = calendarName,
                IsLegacy = plan.IsLegacy
            },
            Objectives = ToObjectives(timers, calendar.Value, nowUtc)
        };
    }

    /// <summary>
    /// Itens do bloco <c>sla</c>: todos os ciclos de cada objetivo, em ordem de objetivo e ciclo, com estado, consumido e
    /// restante no instante informado. Objetivo sem cronômetro vira um único item NotApplicable, como ciclo corrente.
    /// </summary>
    private static List<TicketSlaObjectiveResponse> ToObjectives(
        IReadOnlyCollection<TicketSlaTimer> timers,
        BusinessCalendar calendar,
        DateTime nowUtc) =>
        [.. new[] { SlaMetric.FirstResponse, SlaMetric.Resolution }.SelectMany(metric => ToObjectives(metric, timers, calendar, nowUtc))];

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
            // Minutos inteiros: a fração de minuto é descartada, e o restante nunca é negativo.
            int consumedMinutes = (int)(timer.ConsumedBusinessTime(calendar, nowUtc).Ticks / TimeSpan.TicksPerMinute);

            return new TicketSlaObjectiveResponse
            {
                Metric = metric,
                Cycle = timer.Cycle,
                State = timer.StateAt(nowUtc),
                TargetMinutes = timer.TargetMinutes,
                StartedAtUtc = timer.StartedAtUtc,
                WarningAtUtc = timer.WarningOnUtc,
                DueAtUtc = timer.DueOnUtc,
                CompletedAtUtc = timer.CompletedOnUtc,
                StoppedAtUtc = timer.StoppedOnUtc,
                ConsumedMinutes = consumedMinutes,
                RemainingMinutes = Math.Max(0, timer.TargetMinutes - consumedMinutes),
                IsCurrentCycle = timer.Cycle == currentCycle
            };
        });
    }

    /// <summary>
    /// Nome dos calendários definidos em código, que não têm linha no banco; nulo para os demais.
    /// </summary>
    private static string? CodeCalendarName(Guid calendarId)
    {
        if (calendarId == SlaCalendarIds.Default)
        {
            return DefaultSlaCalendar.Name;
        }

        return calendarId == SlaCalendarIds.Continuous ? ContinuousSlaCalendar.Name : null;
    }
}
