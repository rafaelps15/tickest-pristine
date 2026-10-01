using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
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
        TicketAttachment? attachment = await context.TicketAttachments
            .SingleOrDefaultAsync(a => a.Id == command.AttachmentId, cancellationToken);

        if (attachment is null)
        {
            return Result.Failure(TicketAttachmentErrors.NotFound(command.AttachmentId));
        }

        bool isUploader = attachment.UploadedByUserId == userContext.UserId;

        if (!isUploader)
        {
            bool canManageTickets = await permissionProvider.HasPermissionAsync(
                userContext.UserId,
                PermissionCodes.Tickets.Manage,
                cancellationToken);

            if (!canManageTickets)
            {
                return Result.Failure(UserErrors.Unauthorized());
            }
        }

        attachment.DeletedAtUtc = dateTimeProvider.UtcNow;

        attachment.Raise(new TicketAttachmentDeletedDomainEvent(attachment.Id, attachment.TicketId));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
