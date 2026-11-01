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

        // Cancelar segue as regras do Ticket (janela do dono e tickets:cancel), conferidas pelo TicketSlaCycleService.Cancel
        // antes de qualquer mudança. As demais trocas de status (Em Andamento e Resolvido) exigem tickets:manage: o dono
        // só edita a descrição e cancela.
        if (!cancels && changesStatus && !canManage)
        {
            return Result.Failure(TicketErrors.StatusChangeForbidden);
        }

        // Os cronômetros só mudam com o status; quem o chamado aguarda é recalculado na troca de status.
        List<TicketSlaTimer> slaTimers = changesStatus
            ? await context.TicketSlaTimers.Where(t => t.TicketId == ticket.Id).ToListAsync(cancellationToken)
            : [];

        bool lastMessageIsFromAgent = changesStatus && await agentLookup.IsLastMessageFromAgentAsync(ticket, cancellationToken);

        var historyEntries = new List<TicketHistory>();

        // O status antes da descrição, como no histórico de antes; uma falha não muda nada.
        if (changesStatus)
        {
            var slaCycle = new TicketSlaCycleService();

            Result<TicketHistory> statusChange = command.Status switch
            {
                TicketStatus.InProgress => ticket.StartProgress(lastMessageIsFromAgent, callerId, nowUtc),
                TicketStatus.Resolved => slaCycle.Resolve(ticket, slaTimers, callerId, nowUtc),
                TicketStatus.Canceled => slaCycle.Cancel(ticket, slaTimers, canCancelAny, isOwnerWhoCanEdit, callerId, nowUtc),
                _ => Result.Failure<TicketHistory>(TicketErrors.InvalidStatusTransition(ticket.Status, command.Status))
            };

            if (statusChange.IsFailure)
            {
                return statusChange;
            }

            historyEntries.Add(statusChange.Value);
        }

        if (ticket.ChangeDescription(command.Description, callerId, nowUtc) is { } descriptionChange)
        {
            historyEntries.Add(descriptionChange);
        }

        context.TicketHistories.AddRange(historyEntries);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
