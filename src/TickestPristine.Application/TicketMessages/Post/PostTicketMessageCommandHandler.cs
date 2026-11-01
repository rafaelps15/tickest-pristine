using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.Tickets;
using TickestPristine.Domain.Audit;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketMessages.Post;

internal sealed class PostTicketMessageCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    TicketAgentLookup agentLookup,
    ITicketActivityWriter activityWriter,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<PostTicketMessageCommand, Guid>
{
    public async Task<Result<Guid>> Handle(PostTicketMessageCommand command, CancellationToken cancellationToken)
    {
        // Sem rastreio: a última atividade e a situação de espera são gravadas depois do commit, sem a versão do chamado.
        Ticket? ticket = await context.Tickets
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == command.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<Guid>(TicketErrors.NotFound(command.TicketId));
        }

        // Participantes (solicitante e responsável) sempre podem; os demais precisam de tickets:manage.
        if (!ticket.IsParticipant(userContext.UserId) &&
            !await permissionProvider.HasPermissionAsync(userContext.UserId, PermissionCodes.Tickets.Manage, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.Unauthorized());
        }

        DateTime nowUtc = dateTimeProvider.UtcNow;

        bool authorIsAgent = await agentLookup.IsAgentAsync(userContext.UserId, cancellationToken);

        (TicketMessage message, TicketHistory posting) = ticket.PostMessage(
            userContext.UserId,
            command.Content,
            authorIsAgent,
            nowUtc);

        context.TicketMessages.Add(message);
        context.TicketHistories.Add(posting);

        // A primeira mensagem de quem não é o solicitante e tem tickets:manage conclui a primeira resposta em aberto, no
        // instante da mensagem.
        TicketSlaTimer? firstResponse = ticket.CreatedByUserId == userContext.UserId
            ? null
            : await context.TicketSlaTimers
                .Where(t => t.TicketId == ticket.Id && t.Metric == SlaMetric.FirstResponse)
                .OrderByDescending(t => t.Cycle)
                .FirstOrDefaultAsync(cancellationToken);

        bool completesFirstResponse = firstResponse is { IsOpen: true } &&
            await permissionProvider.HasPermissionAsync(userContext.UserId, PermissionCodes.Tickets.Manage, cancellationToken) &&
            firstResponse.Complete(nowUtc, SlaCompletionCause.Message);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) when (completesFirstResponse)
        {
            // Outra mensagem concluiu a primeira resposta ao mesmo tempo: vale a conclusão dela, e esta mensagem é
            // gravada sem mexer no cronômetro. Os registros de auditoria da tentativa que falhou são descartados para
            // não sair em dobro.
            context.Entry(firstResponse!).State = EntityState.Detached;

            foreach (AuditLogEntry auditEntry in context.AuditLogs.Local.ToList())
            {
                if (context.Entry(auditEntry).State == EntityState.Added)
                {
                    context.Entry(auditEntry).State = EntityState.Detached;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        await activityWriter.WriteLastActivityAndWaitingOnAsync(ticket, cancellationToken);

        return message.Id;
    }
}
