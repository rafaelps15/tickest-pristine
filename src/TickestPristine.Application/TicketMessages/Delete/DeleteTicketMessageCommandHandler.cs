using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.Tickets;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketMessages.Delete;

internal sealed class DeleteTicketMessageCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    TicketAgentLookup agentLookup,
    ITicketActivityWriter activityWriter,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeleteTicketMessageCommand>
{
    public async Task<Result> Handle(DeleteTicketMessageCommand command, CancellationToken cancellationToken)
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

        bool canDeleteAny = await permissionProvider.HasPermissionAsync(
            userContext.UserId,
            PermissionCodes.Tickets.Manage,
            cancellationToken);

        Result<TicketHistory> removal = message.Delete(userContext.UserId, canDeleteAny, dateTimeProvider.UtcNow);

        if (removal.IsFailure)
        {
            return removal;
        }

        // Sem rastreio: a última atividade e a situação de espera são gravadas depois do commit, sem a versão do chamado.
        Ticket ticket = await context.Tickets.AsNoTracking().SingleAsync(t => t.Id == message.TicketId, cancellationToken);

        context.TicketHistories.Add(removal.Value);

        await context.SaveChangesAsync(cancellationToken);

        ticket.RecordMessageActivity(removal.Value, await agentLookup.IsLastMessageFromAgentAsync(ticket, cancellationToken));

        await activityWriter.WriteLastActivityAndWaitingOnAsync(ticket, cancellationToken);

        return Result.Success();
    }
}
