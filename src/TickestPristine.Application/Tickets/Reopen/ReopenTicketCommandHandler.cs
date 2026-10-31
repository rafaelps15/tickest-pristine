using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Sla;
using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Application.Tickets.Reopen;

internal sealed class ReopenTicketCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    ISlaCalendarProvider slaCalendarProvider,
    TicketAgentLookup agentLookup,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ReopenTicketCommand>
{
    public async Task<Result> Handle(ReopenTicketCommand command, CancellationToken cancellationToken)
    {
        Ticket? ticket = await context.Tickets.SingleOrDefaultAsync(t => t.Id == command.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure(TicketErrors.NotFound(command.TicketId));
        }

        // O dono reabre com tickets:reopen-own; os demais, com tickets:manage.
        string reopenPermission = ticket.CreatedByUserId == userContext.UserId
            ? PermissionCodes.Tickets.ReopenOwn
            : PermissionCodes.Tickets.Manage;

        if (!await permissionProvider.HasPermissionAsync(userContext.UserId, reopenPermission, cancellationToken))
        {
            return Result.Failure(UserErrors.Unauthorized());
        }

        if (ticket.IsActive)
        {
            return Result.Failure(TicketErrors.AlreadyActive(ticket.Id));
        }

        if (!ticket.CanTransitionTo(TicketStatus.Open))
        {
            return Result.Failure(TicketErrors.InvalidStatusTransition(ticket.Status, TicketStatus.Open));
        }

        Result<BusinessCalendar> calendar = await slaCalendarProvider.GetAsync(ticket.SlaPlan.CalendarId, cancellationToken);

        if (calendar.IsFailure)
        {
            return calendar;
        }

        DateTime nowUtc = dateTimeProvider.UtcNow;
        Guid callerId = userContext.UserId;
        string? reason = command.Reason?.Trim();
        bool hasReason = !string.IsNullOrEmpty(reason);

        List<TicketSlaTimer> slaTimers = await context.TicketSlaTimers
            .Where(t => t.TicketId == ticket.Id)
            .ToListAsync(cancellationToken);

        // Com motivo, a mensagem de quem reabriu passa a ser a última e decide quem o chamado aguarda (abaixo).
        bool lastMessageIsFromAgent = !hasReason &&
            await agentLookup.IsLastMessageFromAgentAsync(ticket, cancellationToken);

        // A reabertura abre um novo ciclo de resolução com a meta integral do plano; os ciclos anteriores e a primeira
        // resposta não mudam.
        (TicketHistory reopening, TicketSlaTimer resolutionTimer) = ticket.Reopen(
            slaTimers,
            calendar.Value,
            lastMessageIsFromAgent,
            callerId,
            nowUtc);

        context.TicketHistories.Add(reopening);
        context.TicketSlaTimers.Add(resolutionTimer);

        // Com motivo, a mensagem de quem reabriu entra no mesmo SaveChanges da reabertura: as duas ou nenhuma.
        if (hasReason)
        {
            bool callerIsAgent = await agentLookup.IsAgentAsync(callerId, cancellationToken);

            (TicketMessage message, TicketHistory posting) = ticket.PostMessage(callerId, reason!, callerIsAgent, nowUtc);

            context.TicketMessages.Add(message);
            context.TicketHistories.Add(posting);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
