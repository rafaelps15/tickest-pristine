using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Storage;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketAttachments.Upload;

internal sealed class UploadTicketAttachmentCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IDateTimeProvider dateTimeProvider,
    IFileStorage fileStorage)
    : ICommandHandler<UploadTicketAttachmentCommand, Guid>
{
    public async Task<Result<Guid>> Handle(UploadTicketAttachmentCommand command, CancellationToken cancellationToken)
    {
        Ticket? ticket = await context.Tickets.SingleOrDefaultAsync(t => t.Id == command.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<Guid>(TicketErrors.NotFound(command.TicketId));
        }

        bool isParticipant = ticket.CreatedByUserId == userContext.UserId || ticket.AssignedToUserId == userContext.UserId;

        if (!isParticipant)
        {
            bool canManageTickets = await permissionProvider.HasPermissionAsync(
                userContext.UserId,
                PermissionCodes.Tickets.Manage,
                cancellationToken);

            if (!canManageTickets)
            {
                return Result.Failure<Guid>(TicketErrors.Unauthorized());
            }
        }

        if (command.FileSizeBytes > TicketAttachmentPolicy.MaxFileSizeBytes)
        {
            return Result.Failure<Guid>(TicketAttachmentErrors.FileTooLarge(TicketAttachmentPolicy.MaxFileSizeBytes));
        }

        if (!TicketAttachmentPolicy.AllowedContentTypes.Contains(command.ContentType))
        {
            return Result.Failure<Guid>(TicketAttachmentErrors.UnsupportedContentType(command.ContentType));
        }

        string storageKey = await fileStorage.SaveAsync(command.Content, command.FileName, cancellationToken);

        var attachment = TicketAttachment.Create(
            ticket.Id,
            userContext.UserId,
            command.FileName,
            command.ContentType,
            command.FileSizeBytes,
            storageKey,
            dateTimeProvider.UtcNow);

        context.TicketAttachments.Add(attachment);

        await context.SaveChangesAsync(cancellationToken);

        return attachment.Id;
    }
}
