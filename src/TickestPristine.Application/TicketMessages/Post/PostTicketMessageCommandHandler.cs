using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Permissions;
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
        // Sem rastreio: publicar a mensagem não muda o chamado; a última atividade e quem ele aguarda saem da projeção,
        // depois do commit.
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

        TicketMessagePosting posting = ticket.PostMessage(userContext.UserId, command.Content, nowUtc);

        context.TicketMessages.Add(posting.Message);
        context.TicketHistories.Add(posting.History);

        // A primeira mensagem de quem atende (lido do banco pelo IAgentDirectory) conclui a primeira resposta em aberto,
        // no instante da mensagem.
        TicketSlaTimer? firstResponse = await context.TicketSlaTimers
            .Where(t => t.TicketId == ticket.Id && t.Metric == SlaMetric.FirstResponse)
            .OrderByDescending(t => t.Cycle)
            .FirstOrDefaultAsync(cancellationToken);

        bool authorIsAgent = await agentDirectory.IsAgentAsync(userContext.UserId, cancellationToken);

        bool completesFirstResponse = firstResponse is not null &&
            firstResponse.CompleteFirstResponse(ticket, userContext.UserId, authorIsAgent, nowUtc);

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
