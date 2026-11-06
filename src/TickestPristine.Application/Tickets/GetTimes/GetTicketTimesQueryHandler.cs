using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Sla;
using TickestPristine.Application.SlaCalendars;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Application.Tickets.GetTimes;

/// <summary>
/// A primeira resposta conta só os cronômetros concluídos por mensagem de quem atende; a resolução conta o ciclo 1
/// (abertura até a primeira resolução). Cronômetros parados (cancelamento) ficam fora, porque não têm conclusão. Os
/// minutos úteis de cada cronômetro saem da mesma regra do <c>GET tickets/{id}/sla</c>
/// (<see cref="TicketSlaTimer.GetConsumed"/>), com o calendário do próprio cronômetro, e o cálculo é feito em memória
/// sobre os cronômetros do recorte.
/// </summary>
internal sealed class GetTicketTimesQueryHandler(IDbConnectionFactory dbConnectionFactory, ISlaCalendarProvider slaCalendarProvider)
    : IQueryHandler<GetTicketTimesQuery, TicketTimesResponse>
{
    public async Task<Result<TicketTimesResponse>> Handle(
        GetTicketTimesQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Scope.HasInvertedPeriod)
        {
            return Result.Failure<TicketTimesResponse>(TicketErrors.InvalidPeriod);
        }

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        // Os cronômetros concluídos dos chamados do recorte: a primeira resposta concluída por mensagem e a resolução do
        // ciclo 1, com o período de conclusão.
        const string sql =
            $"""
             SELECT
                 {TicketSql.SlaTimerColumns}
             FROM public.ticket_sla_timers tm
             WHERE EXISTS (SELECT 1 FROM public.tickets t WHERE t.id = tm.ticket_id AND {TicketSql.MatchesScope})
               AND tm.completed_on_utc IS NOT NULL
               AND ((tm.metric = '{nameof(SlaMetric.FirstResponse)}' AND tm.completion_cause = '{nameof(SlaCompletionCause.Message)}')
                    OR (tm.metric = '{nameof(SlaMetric.Resolution)}' AND tm.cycle = 1))
               AND (@CompletedFromUtc::timestamptz IS NULL OR tm.completed_on_utc >= @CompletedFromUtc::timestamptz)
               AND (@CompletedUntilUtc::timestamptz IS NULL OR tm.completed_on_utc < @CompletedUntilUtc::timestamptz)
             """;

        // O período de resolução do recorte vale para a data da conclusão de cada cronômetro, e não para a
        // primeira resolução do chamado; os demais filtros recortam os chamados.
        var created = LocalDatePeriod.FromLocalDates(request.Scope.CreatedFrom, request.Scope.CreatedTo);
        var completed = LocalDatePeriod.FromLocalDates(request.Scope.ResolvedFrom, request.Scope.ResolvedTo);

        List<TicketSlaTimer> timers = (await connection.QueryAsync<TicketSlaTimer>(
            new CommandDefinition(
                sql,
                new
                {
                    SectorIds = request.Scope.SectorIds.ToArray(),
                    request.Scope.DepartmentId,
                    Priority = request.Scope.Priority?.ToString(),
                    request.Scope.RequesterId,
                    request.Scope.UnassignedOnly,
                    request.Scope.AssignedToUserId,
                    CreatedFromUtc = created.FromUtc,
                    CreatedUntilUtc = created.UntilUtc,
                    ResolvedFromUtc = (DateTime?)null,
                    ResolvedUntilUtc = (DateTime?)null,
                    CompletedFromUtc = completed.FromUtc,
                    CompletedUntilUtc = completed.UntilUtc
                },
                cancellationToken: cancellationToken))).AsList();

        var calendars = new Dictionary<Guid, BusinessCalendar>();

        foreach (Guid calendarId in timers.Select(t => t.CalendarId).Distinct())
        {
            Result<BusinessCalendar> calendar = await slaCalendarProvider.GetAsync(calendarId, cancellationToken);

            if (calendar.IsFailure)
            {
                return Result.Failure<TicketTimesResponse>(calendar.Error);
            }

            calendars[calendarId] = calendar.Value;
        }

        List<int> MinutesOf(SlaMetric metric) =>
        [
            .. timers
                .Where(t => t.Metric == metric)
                // Minutos inteiros: a fração de minuto é descartada.
                .Select(t => (int)(t.GetConsumed(calendars[t.CalendarId], t.CompletedOnUtc!.Value).Ticks / TimeSpan.TicksPerMinute))
        ];

        return new TicketTimesResponse
        {
            MinimumSampleSize = TicketTimeStatistics.MinimumSampleSize,
            FirstResponse = TicketTimeStatistics.From(MinutesOf(SlaMetric.FirstResponse)),
            Resolution = TicketTimeStatistics.From(MinutesOf(SlaMetric.Resolution))
        };
    }
}
