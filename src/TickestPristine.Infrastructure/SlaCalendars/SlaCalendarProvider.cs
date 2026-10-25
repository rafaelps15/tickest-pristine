using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Sla;
using TickestPristine.Application.SlaCalendars;
using TickestPristine.Domain.SlaCalendars;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using TickestPristine.SharedKernel;

namespace TickestPristine.Infrastructure.SlaCalendars;

internal sealed class SlaCalendarProvider(IApplicationDbContext context, HybridCache cache) : ISlaCalendarProvider
{
    // Uma hora no cache; a cópia local de cada instância segue o minuto do padrão do projeto (DEC-13 da spec 004).
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(1)
    };

    public async Task<Result<BusinessCalendar>> GetAsync(Guid calendarId, CancellationToken cancellationToken = default)
    {
        if (calendarId == SlaCalendarIds.Default)
        {
            return DefaultSlaCalendar.Create();
        }

        SlaCalendarSnapshot? snapshot = await cache.GetOrCreateAsync(
            SlaCalendarCacheKeys.ForCalendar(calendarId),
            async cancellation => await LoadAsync(calendarId, cancellation),
            CacheOptions,
            cancellationToken: cancellationToken);

        if (snapshot is null)
        {
            return Result.Failure<BusinessCalendar>(SlaCalendarErrors.NotFound(calendarId));
        }

        HashSet<DateOnly> holidays = [.. snapshot.Holidays];

        return new BusinessCalendar(
            snapshot.WorkSchedule,
            TimeZoneInfo.FindSystemTimeZoneById(snapshot.TimeZoneId),
            snapshot.Is24x7,
            _ => holidays);
    }

    public async Task InvalidateAsync(Guid calendarId, CancellationToken cancellationToken = default) =>
        await cache.RemoveAsync(SlaCalendarCacheKeys.ForCalendar(calendarId), cancellationToken);

    private async Task<SlaCalendarSnapshot?> LoadAsync(Guid calendarId, CancellationToken cancellationToken)
    {
        SlaCalendar? calendar = await context.SlaCalendars
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == calendarId, cancellationToken);

        if (calendar is null)
        {
            return null;
        }

        List<DateOnly> holidays = await context.SlaCalendarHolidays
            .Where(h => h.SlaCalendarId == calendarId)
            .Select(h => h.Date)
            .ToListAsync(cancellationToken);

        return new SlaCalendarSnapshot(calendar.Name, calendar.TimeZoneId, calendar.Is24x7, calendar.WorkSchedule, holidays);
    }
}
