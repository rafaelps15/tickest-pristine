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

        if (!TicketStatusTransitions.IsActive(ticket.Status))
        {
            return Result.Failure(TicketErrors.NotActive(ticket.Id));
        }

        if (changesStatus && !TicketStatusTransitions.CanTransition(ticket.Status, command.Status))
        {
            return Result.Failure(TicketErrors.InvalidStatusTransition(ticket.Status, command.Status));
        }

        DateTime nowUtc = dateTimeProvider.UtcNow;

        if (changesStatus)
        {
            Result statusChange = EnsureCanChangeStatus(ticket, command.Status, nowUtc, isOwnerWhoCanEdit, canManage, canCancelAny);

            if (statusChange.IsFailure)
            {
                return statusChange;
            }
        }

        TicketStatus previousStatus = ticket.Status;
        string previousDescription = ticket.Description;

        ticket.Description = command.Description;
        ticket.Status = command.Status;

        // O SLA é medido até a primeira resolução; reaberturas não mudam o resultado.
        if (command.Status == TicketStatus.Resolved && ticket.ResolvedAtUtc is null)
        {
            ticket.ResolvedAtUtc = nowUtc;
        }

        if (changesStatus)
        {
            await UpdateSlaTimersAsync(ticket.Id, command.Status, nowUtc, cancellationToken);
        }

        ticket.Raise(new TicketUpdatedDomainEvent(ticket.Id));

        if (changesStatus)
        {
            ticket.Raise(new TicketStatusChangedDomainEvent(ticket.Id, previousStatus, command.Status, userContext.UserId));

            context.TicketHistories.Add(TicketHistoryEntries.StatusChanged(
                ticket.Id,
                userContext.UserId,
                previousStatus,
                command.Status,
                nowUtc));
        }

        if (changesDescription)
        {
            context.TicketHistories.Add(TicketHistoryEntries.DescriptionChanged(
                ticket.Id,
                userContext.UserId,
                previousDescription,
                command.Description,
                nowUtc));
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// Cancelar: quem tem <c>tickets:cancel</c>, sempre; o dono, só dentro da janela de
    /// <see cref="TicketCancellationPolicy"/>. As demais trocas de status (Em Andamento e Resolvido) exigem
    /// <c>tickets:manage</c>: o dono só edita a descrição e cancela (spec 016).
    /// </summary>
    private static Result EnsureCanChangeStatus(
        Ticket ticket,
        TicketStatus newStatus,
        DateTime nowUtc,
        bool isOwnerWhoCanEdit,
        bool canManage,
        bool canCancelAny)
    {
        if (newStatus != TicketStatus.Canceled)
        {
            return canManage ? Result.Success() : Result.Failure(TicketErrors.StatusChangeForbidden);
        }

        if (TicketCancellationPolicy.CanCancel(ticket.Status, ticket.CreatedAtUtc, nowUtc, canCancelAny, isOwnerWhoCanEdit))
        {
            return Result.Success();
        }

        return isOwnerWhoCanEdit
            ? Result.Failure(TicketErrors.CancelWindowExpired(TicketCancellationPolicy.OwnerWindowMinutes))
            : Result.Failure(TicketErrors.CancelForbidden);
    }

    /// <summary>
    /// Resolver conclui o ciclo corrente de resolução e, se ainda estiver em aberto, a primeira resposta, no mesmo
    /// instante; cancelar para os cronômetros em aberto. Os demais status não mexem nos cronômetros. As mudanças são
    /// gravadas no mesmo SaveChanges do chamado, sob o token de concorrência dele.
    /// </summary>
    private async Task UpdateSlaTimersAsync(Guid ticketId, TicketStatus newStatus, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (newStatus is not (TicketStatus.Resolved or TicketStatus.Canceled))
        {
            return;
        }

        List<TicketSlaTimer> timers = await context.TicketSlaTimers
            .Where(t => t.TicketId == ticketId)
            .ToListAsync(cancellationToken);

        if (newStatus == TicketStatus.Canceled)
        {
            timers.ForEach(timer => TicketSlaClock.Stop(timer, nowUtc));

            return;
        }

        if (TicketSlaClock.CurrentCycle(timers, SlaMetric.Resolution) is { } resolution)
        {
            TicketSlaClock.Complete(resolution, nowUtc, SlaCompletionCause.Resolution);
        }

        // Resolver sem ter respondido também conclui a primeira resposta, mas com a causa "resolução": a média de primeira
        // resposta conta só as concluídas por mensagem (spec 027).
        if (TicketSlaClock.CurrentCycle(timers, SlaMetric.FirstResponse) is { } firstResponse)
        {
            TicketSlaClock.Complete(firstResponse, nowUtc, SlaCompletionCause.Resolution);
        }
    }
}
