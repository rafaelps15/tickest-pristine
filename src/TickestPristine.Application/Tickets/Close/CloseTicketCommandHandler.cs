using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.Close;

/// <summary>
/// Encerra um chamado resolvido. Só quem atende a fila fecha (o endpoint exige <c>tickets:manage</c>); o chamado
/// fechado ainda pode ser reaberto.
/// </summary>
internal sealed class CloseTicketCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CloseTicketCommand>
{
    public async Task<Result> Handle(CloseTicketCommand command, CancellationToken cancellationToken)
    {
        Ticket? ticket = await context.Tickets.SingleOrDefaultAsync(t => t.Id == command.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure(TicketErrors.NotFound(command.TicketId));
        }

        if (!ticket.CanTransitionTo(TicketStatus.Closed))
        {
            return Result.Failure(TicketErrors.InvalidStatusTransition(ticket.Status, TicketStatus.Closed));
        }

        context.TicketHistories.Add(ticket.Close(userContext.UserId, dateTimeProvider.UtcNow));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
