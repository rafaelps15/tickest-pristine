using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.TicketHistories;
using TickestPristine.Application.Tickets;
using TickestPristine.Domain.Audit;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketMessages.Post;

internal sealed class PostTicketMessageCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<PostTicketMessageCommand, Guid>
{
    public async Task<Result<Guid>> Handle(PostTicketMessageCommand command, CancellationToken cancellationToken)
    {
        Result access = await TicketAccessGuard.EnsureCanViewAsync(
            context,
            permissionProvider,
            userContext.UserId,
            command.TicketId,
            cancellationToken);

        if (access.IsFailure)
        {
            return Result.Failure<Guid>(access.Error);
        }

        DateTime nowUtc = dateTimeProvider.UtcNow;

        TicketMessage message = TicketMessageEntries.Add(context, command.TicketId, userContext.UserId, command.Content, nowUtc);

        TicketSlaTimer? firstResponse = await CompleteFirstResponseAsync(command.TicketId, nowUtc, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) when (firstResponse is not null)
        {
            // Outra mensagem concluiu a primeira resposta ao mesmo tempo: vale a conclusão dela, e esta mensagem é
            // gravada sem mexer no cronômetro. Os registros de auditoria da tentativa que falhou são descartados para
            // não sair em dobro.
            context.Entry(firstResponse).State = EntityState.Detached;

            foreach (AuditLogEntry auditEntry in context.AuditLogs.Local.ToList())
            {
                if (context.Entry(auditEntry).State == EntityState.Added)
                {
                    context.Entry(auditEntry).State = EntityState.Detached;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        return message.Id;
    }

    /// <summary>
    /// A primeira mensagem de quem não é o solicitante e tem <c>tickets:manage</c> conclui a primeira resposta em
    /// aberto, no instante da mensagem. Devolve o cronômetro concluído, ou nulo se esta mensagem não o concluiu.
    /// </summary>
    private async Task<TicketSlaTimer?> CompleteFirstResponseAsync(Guid ticketId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        Guid requesterId = await context.Tickets
            .Where(t => t.Id == ticketId)
            .Select(t => t.CreatedByUserId)
            .SingleAsync(cancellationToken);

        if (requesterId == userContext.UserId)
        {
            return null;
        }

        TicketSlaTimer? firstResponse = await context.TicketSlaTimers
            .Where(t => t.TicketId == ticketId && t.Metric == SlaMetric.FirstResponse)
            .OrderByDescending(t => t.Cycle)
            .FirstOrDefaultAsync(cancellationToken);

        if (firstResponse is null || !TicketSlaClock.IsOpen(firstResponse))
        {
            return null;
        }

        bool canManageTickets = await permissionProvider.HasPermissionAsync(
            userContext.UserId,
            PermissionCodes.Tickets.Manage,
            cancellationToken);

        if (!canManageTickets)
        {
            return null;
        }

        TicketSlaClock.Complete(firstResponse, nowUtc, SlaCompletionCause.Message);

        return firstResponse;
    }
}
