using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Sla;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.SlaCalendars;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

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

        string calendarName = TicketSlaClock.CodeCalendarName(plan.CalendarId)
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
            Objectives = TicketSlaClock.ToObjectives(timers, calendar.Value, nowUtc)
        };
    }
}
