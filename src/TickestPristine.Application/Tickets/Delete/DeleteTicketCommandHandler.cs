using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.TicketHistories;

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
        Ticket? ticket = await context.Tickets
            .Include(t => t.SlaTimers)
            .SingleOrDefaultAsync(t => t.Id == command.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure(TicketErrors.NotFound(command.TicketId));
        }

        // Só quem tem tickets:delete exclui; ser o dono ou atender a fila não basta.
        bool canDelete = await permissionProvider.HasPermissionAsync(
            userContext.UserId,
            PermissionCodes.Tickets.Delete,
            cancellationToken);

        if (!canDelete)
        {
            return Result.Failure(UserErrors.Unauthorized());
        }

        if (!ticket.IsActive)
        {
            return Result.Failure(TicketErrors.NotActive(ticket.Id));
        }

        Result<TicketHistoryEntry> deletion = ticket.Delete(userContext.UserId, dateTimeProvider.UtcNow);

        if (deletion.IsFailure)
        {
            return deletion;
        }

        context.TicketHistories.Add(deletion.Value);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
