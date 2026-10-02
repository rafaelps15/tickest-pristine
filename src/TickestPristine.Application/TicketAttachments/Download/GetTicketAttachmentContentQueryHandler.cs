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

internal sealed class GetTicketAttachmentContentQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IFileStorage fileStorage)
    : IQueryHandler<GetTicketAttachmentContentQuery, TicketAttachmentResponse>
{
    public async Task<Result<TicketAttachmentResponse>> Handle(
        GetTicketAttachmentContentQuery query,
        CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;
        bool canManageTickets = await permissionProvider.HasPermissionAsync(
            userId,
            PermissionCodes.Tickets.Manage,
            cancellationToken);

        TicketAttachmentResponse? attachment = await context.TicketAttachments
            .Where(a => a.Id == query.AttachmentId &&
                        context.Tickets.Any(t => t.Id == a.TicketId &&
                            (canManageTickets || t.CreatedByUserId == userId || t.AssignedToUserId == userId)))
            .Select(a => new TicketAttachmentResponse
            {
                FileName = a.FileName,
                ContentType = a.ContentType,
                StorageKey = a.StorageKey
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (attachment is null)
        {
            return Result.Failure<TicketAttachmentResponse>(TicketAttachmentErrors.NotFound(query.AttachmentId));
        }

        attachment.Content = await fileStorage.OpenReadAsync(attachment.StorageKey, cancellationToken);

        return attachment;
    }
}
