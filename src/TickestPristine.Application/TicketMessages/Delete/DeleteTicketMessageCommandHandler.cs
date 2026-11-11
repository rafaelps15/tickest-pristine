using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Permissions;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.TicketHistories;
using TickestPristine.Domain.TicketMessages;

namespace TickestPristine.Application.TicketMessages.Delete;

internal sealed class DeleteTicketMessageCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
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

        Result<TicketHistoryEntry> removal = message.Delete(userContext.UserId, canDeleteAny, dateTimeProvider.UtcNow);

        if (removal.IsFailure)
        {
            return removal;
        }

        context.TicketHistories.Add(removal.Value);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

