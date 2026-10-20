using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.Update;

internal sealed class UpdateTicketCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateTicketCommand>
{
    public async Task<Result> Handle(UpdateTicketCommand command, CancellationToken cancellationToken)
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
            PermissionCodes.Tickets.UpdateOwn,
            cancellationToken);

        if (access.IsFailure)
        {
            return access;
        }

        if (!TicketStatusTransitions.IsActive(ticket.Status))
        {
            return Result.Failure(TicketErrors.NotActive(ticket.Id));
        }

        if (command.Status != ticket.Status && !TicketStatusTransitions.CanTransition(ticket.Status, command.Status))
        {
            return Result.Failure(TicketErrors.InvalidStatusTransition(ticket.Status, command.Status));
        }

        TicketStatus previousStatus = ticket.Status;

        ticket.Description = command.Description;
        ticket.Status = command.Status;

        // O SLA é medido até a primeira resolução; reaberturas não mudam o resultado.
        if (command.Status == TicketStatus.Resolved && ticket.ResolvedAtUtc is null)
        {
            ticket.ResolvedAtUtc = dateTimeProvider.UtcNow;
        }

        ticket.Raise(new TicketUpdatedDomainEvent(ticket.Id));

        if (previousStatus != command.Status)
        {
            ticket.Raise(new TicketStatusChangedDomainEvent(ticket.Id, previousStatus, command.Status, userContext.UserId));
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
