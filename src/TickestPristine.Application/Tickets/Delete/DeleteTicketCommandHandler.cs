using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.TicketHistories;
using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.Delete;

internal sealed class DeleteTicketCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeleteTicketCommand>
{
    public async Task<Result> Handle(DeleteTicketCommand command, CancellationToken cancellationToken)
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
            PermissionCodes.Tickets.DeleteOwn,
            cancellationToken);

        if (access.IsFailure)
        {
            return access;
        }

        if (!TicketStatusTransitions.IsActive(ticket.Status))
        {
            return Result.Failure(TicketErrors.NotActive(ticket.Id));
        }

        DateTime nowUtc = dateTimeProvider.UtcNow;

        ticket.DeletedAtUtc = nowUtc;

        ticket.Raise(new TicketDeletedDomainEvent(ticket.Id));

        context.TicketHistories.Add(TicketHistoryEntries.Create(
            ticket.Id,
            userContext.UserId,
            TicketHistoryAction.Deleted,
            TicketHistoryDescriptions.Deleted,
            nowUtc));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
