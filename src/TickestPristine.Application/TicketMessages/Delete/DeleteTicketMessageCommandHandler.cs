using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.TicketHistories;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

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

        Result access = await TicketAccessGuard.EnsureOwnerOrManagerAsync(
            permissionProvider,
            userContext.UserId,
            message.AuthorUserId,
            ownPermission: null,
            cancellationToken);

        if (access.IsFailure)
        {
            return access;
        }

        DateTime nowUtc = dateTimeProvider.UtcNow;

        message.DeletedAtUtc = nowUtc;

        message.Raise(new TicketMessageDeletedDomainEvent(message.Id, message.TicketId));

        context.TicketHistories.Add(TicketHistoryEntries.Create(
            message.TicketId,
            userContext.UserId,
            TicketHistoryAction.MessageRemoved,
            nowUtc));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
