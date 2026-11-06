using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.TicketHistories;

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

        Result<TicketHistoryEntry> closing = ticket.Close(userContext.UserId, dateTimeProvider.UtcNow);

        if (closing.IsFailure)
        {
            return closing;
        }

        context.TicketHistories.Add(closing.Value);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
