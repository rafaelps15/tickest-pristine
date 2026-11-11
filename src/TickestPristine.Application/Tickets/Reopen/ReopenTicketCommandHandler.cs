using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Sla;
using TickestPristine.Domain.Permissions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.SlaCalendars;
using TickestPristine.Domain.TicketHistories;

namespace TickestPristine.Application.Tickets.Reopen;

internal sealed class ReopenTicketCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    ISlaCalendarProvider slaCalendarProvider,
    TicketAgentLookup agentLookup,
    IAgentDirectory agentDirectory,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ReopenTicketCommand>
{
    public async Task<Result> Handle(ReopenTicketCommand command, CancellationToken cancellationToken)
    {
        Ticket? ticket = await context.Tickets
            .Include(t => t.SlaTimers)
            .SingleOrDefaultAsync(t => t.Id == command.TicketId, cancellationToken);

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

        // O chamado ativo é recusado antes de carregar o calendário; as demais conferências são feitas pelo agregado.
        if (ticket.IsActive)
        {
            return Result.Failure(TicketErrors.AlreadyActive(ticket.Id));
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

        // Com motivo, a mensagem de quem reabriu passa a ser a última e decide quem o chamado aguarda; sem motivo, decide
        // a última mensagem válida.
        bool authorIsAgent = await agentDirectory.IsAgentAsync(callerId, cancellationToken);
        bool lastMessageIsFromAgent = hasReason
            ? ticket.IsAgentReply(callerId, authorIsAgent)
            : await agentLookup.IsLastValidMessageFromAgentAsync(ticket, cancellationToken);

        Result<TicketHistoryEntry> reopening = ticket.Reopen(
            calendar.Value,
            lastMessageIsFromAgent,
            callerId,
            nowUtc);

        if (reopening.IsFailure)
        {
            return reopening;
        }

        context.TicketHistories.Add(reopening.Value);
        // Com motivo, a mensagem de quem reabriu entra no mesmo SaveChanges da reabertura: as duas ou nenhuma.
        if (hasReason)
        {
            TicketMessagePosting posting = ticket.PostMessage(callerId, reason!, authorIsAgent, nowUtc);

            context.TicketMessages.Add(posting.Message);
            context.TicketHistories.Add(posting.History);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

