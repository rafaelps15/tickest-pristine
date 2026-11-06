using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.TicketHistories;
using TickestPristine.Domain.TicketMessages;

namespace TickestPristine.Application.TicketMessages.Edit;

internal sealed class EditTicketMessageCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
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

        Result<TicketHistoryEntry> edition = message.Edit(command.Content, userContext.UserId, dateTimeProvider.UtcNow);

        if (edition.IsFailure)
        {
            return edition;
        }

        context.TicketHistories.Add(edition.Value);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
