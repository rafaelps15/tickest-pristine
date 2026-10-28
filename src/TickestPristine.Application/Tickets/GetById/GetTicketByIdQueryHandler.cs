using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Sla;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.SlaCalendars;
using TickestPristine.Application.Tickets.GetSla;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetById;

internal sealed class GetTicketByIdQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    ISlaCalendarProvider slaCalendarProvider,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTicketByIdQuery, TicketResponse>
{
    public async Task<Result<TicketResponse>> Handle(GetTicketByIdQuery query, CancellationToken cancellationToken)
    {
        Result access = await TicketAccessGuard.EnsureCanViewAsync(
            context,
            permissionProvider,
            userContext.UserId,
            query.TicketId,
            cancellationToken);

        if (access.IsFailure)
        {
            return Result.Failure<TicketResponse>(access.Error);
        }

        DateTime nowUtc = dateTimeProvider.UtcNow;

        TicketResponse? ticket = await context.Tickets
            .Where(t => t.Id == query.TicketId)
            .Select(t => new TicketResponse
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Priority = t.Priority,
                Status = t.Status,
                OpenedByUserId = t.CreatedByUserId,
                OpenedByName = context.Users
                    .Where(u => u.Id == t.CreatedByUserId)
                    .Select(u => u.FirstName + " " + u.LastName)
                    .FirstOrDefault()!,
                AssignedToUserId = t.AssignedToUserId,
                AssignedToName = context.Users
                    .Where(u => u.Id == t.AssignedToUserId)
                    .Select(u => u.FirstName + " " + u.LastName)
                    .FirstOrDefault(),
                SectorId = t.SectorId,
                SectorName = context.Sectors
                    .Where(s => s.Id == t.SectorId)
                    .Select(s => s.Name)
                    .FirstOrDefault()!,
                CreatedAtUtc = t.CreatedAtUtc,
                DueAtUtc = t.DueAtUtc,
                SlaWarningAtUtc = t.SlaWarningAtUtc,
                ResolvedAtUtc = t.ResolvedAtUtc,
                SlaStatus = TicketSla.GetStatus(t.Status, t.DueAtUtc, t.SlaWarningAtUtc, t.ResolvedAtUtc, nowUtc)
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<TicketResponse>(TicketErrors.NotFound(query.TicketId));
        }

        TicketSlaPlan plan = await context.Tickets
            .Where(t => t.Id == query.TicketId)
            .Select(t => t.SlaPlan)
            .SingleAsync(cancellationToken);

        Result<BusinessCalendar> calendar = await slaCalendarProvider.GetAsync(plan.CalendarId, cancellationToken);

        if (calendar.IsFailure)
        {
            return Result.Failure<TicketResponse>(calendar.Error);
        }

        List<TicketSlaTimer> timers = await context.TicketSlaTimers
            .AsNoTracking()
            .Where(t => t.TicketId == query.TicketId)
            .ToListAsync(cancellationToken);

        Guid callerId = userContext.UserId;
        bool canCancelAny = await permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Tickets.Cancel, cancellationToken);
        bool isOwnerWhoCanEdit = ticket.OpenedByUserId == callerId &&
            await permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Tickets.UpdateOwn, cancellationToken);

        ticket.OwnerCancelDeadlineUtc = TicketCancellationPolicy.OwnerDeadline(ticket.CreatedAtUtc);
        ticket.CanCancel = TicketCancellationPolicy.CanCancel(
            ticket.Status,
            ticket.CreatedAtUtc,
            nowUtc,
            canCancelAny,
            isOwnerWhoCanEdit);

        string calendarName = TicketSlaClock.CodeCalendarName(plan.CalendarId)
            ?? await context.SlaCalendars.Where(c => c.Id == plan.CalendarId).Select(c => c.Name).SingleAsync(cancellationToken);

        // O mesmo instante do SlaStatus, para o bloco sla e os campos atuais concordarem.
        ticket.Sla = new TicketSlaResponse
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

        return ticket;
    }
}
