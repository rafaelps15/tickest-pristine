using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.TicketHistories;
using TickestPristine.Application.Permissions;
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
        string previousDescription = ticket.Description;
        DateTime nowUtc = dateTimeProvider.UtcNow;

        ticket.Description = command.Description;
        ticket.Status = command.Status;

        // O SLA é medido até a primeira resolução; reaberturas não mudam o resultado.
        if (command.Status == TicketStatus.Resolved && ticket.ResolvedAtUtc is null)
        {
            ticket.ResolvedAtUtc = nowUtc;
        }

        if (previousStatus != command.Status)
        {
            await UpdateSlaTimersAsync(ticket.Id, command.Status, nowUtc, cancellationToken);
        }

        ticket.Raise(new TicketUpdatedDomainEvent(ticket.Id));

        if (previousStatus != command.Status)
        {
            ticket.Raise(new TicketStatusChangedDomainEvent(ticket.Id, previousStatus, command.Status, userContext.UserId));

            context.TicketHistories.Add(TicketHistoryEntries.StatusChanged(
                ticket.Id,
                userContext.UserId,
                previousStatus,
                command.Status,
                nowUtc));
        }

        if (!string.Equals(previousDescription, command.Description, StringComparison.Ordinal))
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
            TicketSlaClock.Complete(resolution, nowUtc);
        }

        if (TicketSlaClock.CurrentCycle(timers, SlaMetric.FirstResponse) is { } firstResponse)
        {
            TicketSlaClock.Complete(firstResponse, nowUtc);
        }
    }
}
