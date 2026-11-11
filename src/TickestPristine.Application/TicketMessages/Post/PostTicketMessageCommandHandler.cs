using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Permissions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketMessages.Post;

internal sealed class PostTicketMessageCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IAgentDirectory agentDirectory,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<PostTicketMessageCommand, Guid>
{
    public async Task<Result<Guid>> Handle(PostTicketMessageCommand command, CancellationToken cancellationToken)
    {
        Ticket? ticket = await context.Tickets
            .Include(t => t.SlaTimers)
            .SingleOrDefaultAsync(t => t.Id == command.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<Guid>(TicketErrors.NotFound(command.TicketId));
        }

        bool canManageTickets = await permissionProvider.HasPermissionAsync(
            userContext.UserId,
            PermissionCodes.Tickets.Manage,
            cancellationToken);

        if (!ticket.CanBeViewedBy(userContext.UserId, canManageTickets))
        {
            return Result.Failure<Guid>(UserErrors.Unauthorized());
        }

        DateTime nowUtc = dateTimeProvider.UtcNow;

        bool authorIsAgent = await agentDirectory.IsAgentAsync(userContext.UserId, cancellationToken);
        TicketSlaTimer? firstResponse = ticket.SlaTimers.FirstOrDefault(timer => timer.Metric == SlaMetric.FirstResponse);
        bool completesFirstResponse = ticket.IsAgentReply(userContext.UserId, authorIsAgent) && firstResponse?.IsOpen == true;

        TicketMessagePosting posting = ticket.PostMessage(userContext.UserId, command.Content, authorIsAgent, nowUtc);

        context.TicketMessages.Add(posting.Message);
        context.TicketHistories.Add(posting.History);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) when (completesFirstResponse)
        {
            // Outra mensagem concluiu a primeira resposta ao mesmo tempo: vale a conclusão dela, e esta mensagem é
            // gravada sem mexer no cronômetro. Os registros de auditoria da tentativa que falhou já foram descartados pelo
            // interceptor, então nada sai em dobro.
            context.Entry(firstResponse!).State = EntityState.Detached;

            await context.SaveChangesAsync(cancellationToken);
        }

        return posting.Message.Id;
    }
}

