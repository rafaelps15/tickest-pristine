using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.Reopen;

internal sealed class ReopenTicketCommandHandler(IApplicationDbContext context)
    : ICommandHandler<ReopenTicketCommand>
{
    public async Task<Result> Handle(ReopenTicketCommand command, CancellationToken cancellationToken)
    {
        Ticket? ticket = await context.Tickets.SingleOrDefaultAsync(t => t.Id == command.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure(TicketErrors.NotFound(command.TicketId));
        }

        if (TicketStatusTransitions.IsActive(ticket.Status))
        {
            return Result.Failure(TicketErrors.AlreadyActive(ticket.Id));
        }

        if (!TicketStatusTransitions.CanTransition(ticket.Status, TicketStatus.Open))
        {
            return Result.Failure(TicketErrors.InvalidStatusTransition(ticket.Status, TicketStatus.Open));
        }

        ticket.Status = TicketStatus.Open;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
