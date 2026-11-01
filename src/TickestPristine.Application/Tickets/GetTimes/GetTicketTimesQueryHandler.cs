using Microsoft.EntityFrameworkCore;
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
/// sobre os cronômetros do recorte (spec 027).
/// </summary>
internal sealed class GetTicketTimesQueryHandler(IApplicationDbContext context, ISlaCalendarProvider slaCalendarProvider)
    : IQueryHandler<GetTicketTimesQuery, TicketTimesResponse>
{
    public async Task<Result<TicketTimesResponse>> Handle(GetTicketTimesQuery query, CancellationToken cancellationToken)
    {
        if (query.Scope.HasInvertedPeriod)
        {
            return Result.Failure<TicketTimesResponse>(TicketErrors.InvalidPeriod);
        }

        // O período de resolução do recorte vale para a data da conclusão de cada cronômetro (DEC-4), e não para a
        // primeira resolução do chamado; os demais filtros recortam os chamados.
        var completedPeriod = LocalDatePeriod.FromLocalDates(query.Scope.ResolvedFrom, query.Scope.ResolvedTo);
        DateTime? completedFromUtc = completedPeriod.FromUtc;
        DateTime? completedUntilUtc = completedPeriod.UntilUtc;
        IQueryable<Ticket> tickets = (query.Scope with { ResolvedFrom = null, ResolvedTo = null }).Apply(context.Tickets);

        List<TicketSlaTimer> timers = await context.TicketSlaTimers
            .AsNoTracking()
            .Where(s =>
                tickets.Any(t => t.Id == s.TicketId) &&
                s.CompletedOnUtc != null &&
                (s.Metric == SlaMetric.FirstResponse && s.CompletionCause == SlaCompletionCause.Message ||
                 s.Metric == SlaMetric.Resolution && s.Cycle == 1) &&
                (completedFromUtc == null || s.CompletedOnUtc >= completedFromUtc) &&
                (completedUntilUtc == null || s.CompletedOnUtc < completedUntilUtc))
            .ToListAsync(cancellationToken);

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
