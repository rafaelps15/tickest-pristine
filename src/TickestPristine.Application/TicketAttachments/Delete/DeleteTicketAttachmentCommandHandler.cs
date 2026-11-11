using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Permissions;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.TicketAttachments;
using TickestPristine.Domain.TicketHistories;

namespace TickestPristine.Application.TicketAttachments.Delete;

internal sealed class DeleteTicketAttachmentCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeleteTicketAttachmentCommand>
{
    public async Task<Result> Handle(DeleteTicketAttachmentCommand command, CancellationToken cancellationToken)
    {
        // O filtro global do chamado exclui os excluídos: mensagem ou anexo de chamado excluído não é mais alterado.
        TicketAttachment? attachment = await context.TicketAttachments
            .SingleOrDefaultAsync(
                a => a.Id == command.AttachmentId && context.Tickets.Any(t => t.Id == a.TicketId),
                cancellationToken);

        if (attachment is null)
        {
            return Result.Failure(TicketAttachmentErrors.NotFound(command.AttachmentId));
        }

        bool canDeleteAny = await permissionProvider.HasPermissionAsync(
            userContext.UserId,
            PermissionCodes.Tickets.Manage,
            cancellationToken);

        Result<TicketHistoryEntry> removal = attachment.Delete(userContext.UserId, canDeleteAny, dateTimeProvider.UtcNow);

        if (removal.IsFailure)
        {
            return removal;
        }

        context.TicketHistories.Add(removal.Value);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

