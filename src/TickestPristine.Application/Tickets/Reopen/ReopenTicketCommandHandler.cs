using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.Reopen;

internal sealed class ReopenTicketCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
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

        if (TicketStatusTransitions.IsActive(ticket.Status))
        {
            return Result.Failure(TicketErrors.AlreadyActive(ticket.Id));
        }

        if (!TicketStatusTransitions.CanTransition(ticket.Status, TicketStatus.Open))
        {
            return Result.Failure(TicketErrors.InvalidStatusTransition(ticket.Status, TicketStatus.Open));
        }

        TicketStatus previousStatus = ticket.Status;

        ticket.Status = TicketStatus.Open;

        ticket.Raise(new TicketReopenedDomainEvent(ticket.Id, userContext.UserId));

        context.TicketHistories.Add(TicketHistoryEntries.Create(
            ticket.Id,
            userContext.UserId,
            TicketHistoryAction.Reopened,
            TicketHistoryDescriptions.Reopened,
            dateTimeProvider.UtcNow,
            oldValue: previousStatus.ToString(),
            newValue: TicketStatus.Open.ToString()));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
