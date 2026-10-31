using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Tickets;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketMessages.Edit;

internal sealed class EditTicketMessageCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    TicketAgentLookup agentLookup,
    ITicketActivityWriter activityWriter,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<EditTicketMessageCommand>
{
    public async Task<Result> Handle(EditTicketMessageCommand command, CancellationToken cancellationToken)
    {
        // O filtro global do chamado exclui os excluídos: mensagem ou anexo de chamado excluído não é mais alterado.
        TicketMessage? message = await context.TicketMessages
            .SingleOrDefaultAsync(
                m => m.Id == command.MessageId && context.Tickets.Any(t => t.Id == m.TicketId),
                cancellationToken);

        if (message is null)
        {
            return Result.Failure(TicketMessageErrors.NotFound(command.MessageId));
        }

        Result<TicketHistory> edition = message.Edit(command.Content, userContext.UserId, dateTimeProvider.UtcNow);

        if (edition.IsFailure)
        {
            return edition;
        }

        // Sem rastreio: a última atividade e a situação de espera são gravadas depois do commit, sem a versão do chamado.
        Ticket ticket = await context.Tickets.AsNoTracking().SingleAsync(t => t.Id == message.TicketId, cancellationToken);

        context.TicketHistories.Add(edition.Value);

        await context.SaveChangesAsync(cancellationToken);

        ticket.RecordMessageActivity(edition.Value, await agentLookup.IsLastMessageFromAgentAsync(ticket, cancellationToken));

        await activityWriter.WriteLastActivityAndWaitingOnAsync(ticket, cancellationToken);

        return Result.Success();
    }
}
