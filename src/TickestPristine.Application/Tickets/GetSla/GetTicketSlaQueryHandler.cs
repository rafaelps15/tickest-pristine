using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Sla;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Application.Tickets.GetSla;

/// <summary>
/// Só o bloco <c>sla</c> do chamado, com o mesmo acesso do detalhe: participantes ou quem tem <c>tickets:manage</c>.
/// </summary>
internal sealed class GetTicketSlaQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    ISlaCalendarProvider slaCalendarProvider,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTicketSlaQuery, TicketSlaResponse>
{
    public async Task<Result<TicketSlaResponse>> Handle(GetTicketSlaQuery query, CancellationToken cancellationToken)
    {
        Result access = await TicketAccessGuard.EnsureCanViewAsync(
            context,
            permissionProvider,
            userContext.UserId,
            query.TicketId,
            cancellationToken);

        if (access.IsFailure)
        {
            return Result.Failure<TicketSlaResponse>(access.Error);
        }

        TicketSlaPlan plan = await context.Tickets
            .Where(t => t.Id == query.TicketId)
            .Select(t => t.SlaPlan)
            .SingleAsync(cancellationToken);

        Result<BusinessCalendar> calendar = await slaCalendarProvider.GetAsync(plan.CalendarId, cancellationToken);

        if (calendar.IsFailure)
        {
            return Result.Failure<TicketSlaResponse>(calendar.Error);
        }

        List<TicketSlaTimer> timers = await context.TicketSlaTimers
            .AsNoTracking()
            .Where(t => t.TicketId == query.TicketId)
            .ToListAsync(cancellationToken);

        string calendarName = CodeCalendarName(plan.CalendarId)
            ?? await context.SlaCalendars.Where(c => c.Id == plan.CalendarId).Select(c => c.Name).SingleAsync(cancellationToken);

        DateTime nowUtc = dateTimeProvider.UtcNow;

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
            int consumedMinutes = (int)(timer.GetConsumed(calendar, nowUtc).Ticks / TimeSpan.TicksPerMinute);

            return new TicketSlaObjectiveResponse
            {
                Metric = metric,
                Cycle = timer.Cycle,
                State = timer.GetState(nowUtc),
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
