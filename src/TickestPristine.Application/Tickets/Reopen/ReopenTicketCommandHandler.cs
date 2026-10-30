using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Sla;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.SlaCalendars;
using TickestPristine.Application.TicketMessages;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.Reopen;

internal sealed class ReopenTicketCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    ISlaCalendarProvider slaCalendarProvider,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ReopenTicketCommand>
{
    public async Task<Result> Handle(ReopenTicketCommand command, CancellationToken cancellationToken)
    {
        Ticket? ticket = await context.Tickets.SingleOrDefaultAsync(t => t.Id == command.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure(TicketErrors.NotFound(command.TicketId));
        }

        Result access = await TicketAccessGuard.EnsureOwnerOrManagerAsync(
            permissionProvider,
            userContext.UserId,
            ticket.CreatedByUserId,
            PermissionCodes.Tickets.ReopenOwn,
            cancellationToken);

        if (access.IsFailure)
        {
            return access;
        }

        if (ticket.IsActive)
        {
            return Result.Failure(TicketErrors.AlreadyActive(ticket.Id));
        }

        if (!ticket.CanTransitionTo(TicketStatus.Open))
        {
            return Result.Failure(TicketErrors.InvalidStatusTransition(ticket.Status, TicketStatus.Open));
        }

        Result<BusinessCalendar> calendar = await slaCalendarProvider.GetAsync(ticket.SlaPlan.CalendarId, cancellationToken);

        if (calendar.IsFailure)
        {
            return calendar;
        }

        DateTime nowUtc = dateTimeProvider.UtcNow;

        context.TicketHistories.Add(ticket.Reopen(userContext.UserId, nowUtc));

        // A reabertura abre um novo ciclo de resolução com a meta integral do plano; os ciclos anteriores e a primeira
        // resposta não mudam.
        int lastResolutionCycle = await context.TicketSlaTimers
            .Where(t => t.TicketId == ticket.Id && t.Metric == SlaMetric.Resolution)
            .MaxAsync(t => (int?)t.Cycle, cancellationToken) ?? 0;

        context.TicketSlaTimers.Add(TicketSlaClock.StartResolutionCycle(
            ticket.Id,
            ticket.SlaPlan,
            lastResolutionCycle + 1,
            calendar.Value,
            nowUtc));

        // Com motivo, a mensagem de quem reabriu entra no mesmo SaveChanges da reabertura: as duas ou nenhuma.
        string? reason = command.Reason?.Trim();

        if (!string.IsNullOrEmpty(reason))
        {
            TicketMessageEntries.Add(context, ticket.Id, userContext.UserId, reason, nowUtc);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
