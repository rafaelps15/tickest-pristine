using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.Update;

internal sealed class UpdateTicketCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : ICommandHandler<UpdateTicketCommand>
{
    public async Task<Result> Handle(UpdateTicketCommand command, CancellationToken cancellationToken)
    {
        Ticket? ticket = await context.Tickets.SingleOrDefaultAsync(t => t.Id == command.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure(TicketErrors.NotFound(command.TicketId));
        }

        if (!TicketStatusTransitions.IsActive(ticket.Status))
        {
            return Result.Failure(TicketErrors.NotActive(ticket.Id));
        }

        bool isOwner = ticket.CreatedByUserId == userContext.UserId;
        string requiredPermission = isOwner ? PermissionCodes.Tickets.UpdateOwn : PermissionCodes.Tickets.Manage;

        bool hasPermission = await permissionProvider.HasPermissionAsync(userContext.UserId, requiredPermission, cancellationToken);

        if (!hasPermission)
        {
            return Result.Failure(TicketErrors.Unauthorized());
        }

        if (command.Status != ticket.Status && !TicketStatusTransitions.CanTransition(ticket.Status, command.Status))
        {
            return Result.Failure(TicketErrors.InvalidStatusTransition(ticket.Status, command.Status));
        }

        ticket.Update(command.Description, command.Status);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
