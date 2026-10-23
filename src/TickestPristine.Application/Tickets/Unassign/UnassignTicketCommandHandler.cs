using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.TicketHistories;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.Unassign;

/// <summary>
/// Deixa um chamado ativo sem responsável (o endpoint exige <c>tickets:manage</c>). O chamado inativo é recusado antes
/// de conferir o responsável, e remover de um chamado que já está sem responsável não muda nada. O status não muda.
/// </summary>
internal sealed class UnassignTicketCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UnassignTicketCommand>
{
    public async Task<Result> Handle(UnassignTicketCommand command, CancellationToken cancellationToken)
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

        if (ticket.AssignedToUserId is not Guid previousAssignedToUserId)
        {
            return Result.Success();
        }

        ticket.AssignedToUserId = null;

        ticket.Raise(new TicketUnassignedDomainEvent(
            ticket.Id,
            previousAssignedToUserId,
            userContext.UserId));

        context.TicketHistories.Add(TicketHistoryEntries.Unassigned(
            ticket.Id,
            userContext.UserId,
            previousAssignedToUserId,
            dateTimeProvider.UtcNow));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
