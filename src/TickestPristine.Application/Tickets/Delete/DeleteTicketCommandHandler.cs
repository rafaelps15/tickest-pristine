using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.TicketHistories;
using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
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

        // Só quem tem tickets:delete exclui; ser o dono ou atender a fila não basta (spec 016).
        bool canDelete = await permissionProvider.HasPermissionAsync(
            userContext.UserId,
            PermissionCodes.Tickets.Delete,
            cancellationToken);

        if (!canDelete)
        {
            return Result.Failure(UserErrors.Unauthorized());
        }

        if (!TicketStatusTransitions.IsActive(ticket.Status))
        {
            return Result.Failure(TicketErrors.NotActive(ticket.Id));
        }

        DateTime nowUtc = dateTimeProvider.UtcNow;

        ticket.DeletedAtUtc = nowUtc;

        ticket.Raise(new TicketDeletedDomainEvent(ticket.Id));

        // Os cronômetros do chamado excluído saem da fila do monitoramento, sem violação nem evento.
        List<TicketSlaTimer> slaTimers = await context.TicketSlaTimers
            .Where(t => t.TicketId == ticket.Id && t.NextActionOnUtc != null)
            .ToListAsync(cancellationToken);

        slaTimers.ForEach(TicketSlaClock.Dequeue);

        context.TicketHistories.Add(TicketHistoryEntries.Create(
            ticket.Id,
            userContext.UserId,
            TicketHistoryAction.Deleted,
            nowUtc));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
