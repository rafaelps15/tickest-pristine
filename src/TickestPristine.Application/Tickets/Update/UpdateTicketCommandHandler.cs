using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.Update;

internal sealed class UpdateTicketCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    TicketAgentLookup agentLookup,
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

        Guid callerId = userContext.UserId;
        bool changesStatus = command.Status != ticket.Status;
        bool cancels = changesStatus && command.Status == TicketStatus.Canceled;
        bool changesDescription = !string.Equals(ticket.Description, command.Description, StringComparison.Ordinal);

        bool isOwnerWhoCanEdit = ticket.CreatedByUserId == callerId &&
            await permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Tickets.UpdateOwn, cancellationToken);
        bool canManage = await permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Tickets.Manage, cancellationToken);
        bool canCancelAny = cancels &&
            await permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Tickets.Cancel, cancellationToken);

        // Editar a descrição continua com o dono (tickets:update-own) e com quem atende a fila; quem só tem tickets:cancel
        // passa apenas para cancelar, sem mudar a descrição.
        bool canEdit = isOwnerWhoCanEdit || canManage;
        bool canAct = changesDescription ? canEdit : canEdit || canCancelAny;

        if (!canAct)
        {
            return Result.Failure(UserErrors.Unauthorized());
        }

        if (!ticket.IsActive)
        {
            return Result.Failure(TicketErrors.NotActive(ticket.Id));
        }

        if (changesStatus && !ticket.CanTransitionTo(command.Status))
        {
            return Result.Failure(TicketErrors.InvalidStatusTransition(ticket.Status, command.Status));
        }

        DateTime nowUtc = dateTimeProvider.UtcNow;

        // Cancelar segue o Ticket.EnsureCanBeCanceled. As demais trocas de status (Em Andamento e Resolvido) exigem
        // tickets:manage: o dono só edita a descrição e cancela.
        if (cancels)
        {
            Result cancellation = ticket.EnsureCanBeCanceled(canCancelAny, isOwnerWhoCanEdit, nowUtc);

            if (cancellation.IsFailure)
            {
                return cancellation;
            }
        }
        else if (changesStatus && !canManage)
        {
            return Result.Failure(TicketErrors.StatusChangeForbidden);
        }

        // Os cronômetros só mudam com o status; quem o chamado aguarda é recalculado na troca de status.
        List<TicketSlaTimer> slaTimers = changesStatus
            ? await context.TicketSlaTimers.Where(t => t.TicketId == ticket.Id).ToListAsync(cancellationToken)
            : [];

        bool lastMessageIsFromAgent = changesStatus && await agentLookup.IsLastMessageFromAgentAsync(ticket, cancellationToken);

        IReadOnlyList<TicketHistory> historyEntries = ticket.Update(
            command.Description,
            command.Status,
            slaTimers,
            lastMessageIsFromAgent,
            callerId,
            nowUtc);

        context.TicketHistories.AddRange(historyEntries);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
