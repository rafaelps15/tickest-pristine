using TickestPristine.Application.Abstractions.Sla;
using TickestPristine.Application.SlaCalendars;
using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Application.UnitTests.Abstractions;

/// <summary>
/// Provedor com os calendários definidos em código ("Expediente" e "24 horas"), sem banco nem cache.
/// </summary>
public sealed class TestSlaCalendarProvider : ISlaCalendarProvider
{
    public Task<Result<BusinessCalendar>> GetAsync(Guid calendarId, CancellationToken cancellationToken = default)
    {
        if (calendarId == SlaCalendarIds.Default)
        {
            return Task.FromResult<Result<BusinessCalendar>>(DefaultSlaCalendar.Create());
        }

        if (calendarId == SlaCalendarIds.Continuous)
        {
            return Task.FromResult<Result<BusinessCalendar>>(ContinuousSlaCalendar.Create());
        }

        return Task.FromResult(Result.Failure<BusinessCalendar>(SlaCalendarErrors.NotFound(calendarId)));
    }

    public Task InvalidateAsync(Guid calendarId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
