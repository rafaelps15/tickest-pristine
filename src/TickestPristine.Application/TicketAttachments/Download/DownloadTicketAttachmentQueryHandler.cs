using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Storage;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketAttachments.Download;

internal sealed class DownloadTicketAttachmentQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IFileStorage fileStorage)
    : IQueryHandler<DownloadTicketAttachmentQuery, TicketAttachmentDownloadResponse>
{
    public async Task<Result<TicketAttachmentDownloadResponse>> Handle(
        DownloadTicketAttachmentQuery query,
        CancellationToken cancellationToken)
    {
        TicketAttachment? attachment = await context.TicketAttachments
            .SingleOrDefaultAsync(a => a.Id == query.AttachmentId, cancellationToken);

        if (attachment is null)
        {
            return Result.Failure<TicketAttachmentDownloadResponse>(TicketAttachmentErrors.NotFound(query.AttachmentId));
        }

        Ticket? ticket = await context.Tickets.SingleOrDefaultAsync(t => t.Id == attachment.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<TicketAttachmentDownloadResponse>(TicketErrors.NotFound(attachment.TicketId));
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
                return Result.Failure<TicketAttachmentDownloadResponse>(TicketErrors.Unauthorized());
            }
        }

        Stream content = await fileStorage.OpenReadAsync(attachment.StorageKey, cancellationToken);

        return new TicketAttachmentDownloadResponse
        {
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            Content = content
        };
    }
}
