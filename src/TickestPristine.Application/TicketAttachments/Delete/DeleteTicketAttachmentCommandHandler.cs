using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

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

        Result access = await TicketAccessGuard.EnsureOwnerOrManagerAsync(
            permissionProvider,
            userContext.UserId,
            attachment.UploadedByUserId,
            ownPermission: null,
            cancellationToken);

        if (access.IsFailure)
        {
            return access;
        }

        DateTime nowUtc = dateTimeProvider.UtcNow;

        attachment.DeletedAtUtc = nowUtc;

        attachment.Raise(new TicketAttachmentDeletedDomainEvent(attachment.Id, attachment.TicketId));

        context.TicketHistories.Add(TicketHistoryEntries.Create(
            attachment.TicketId,
            userContext.UserId,
            TicketHistoryAction.AttachmentRemoved,
            TicketHistoryDescriptions.AttachmentRemoved,
            nowUtc));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
