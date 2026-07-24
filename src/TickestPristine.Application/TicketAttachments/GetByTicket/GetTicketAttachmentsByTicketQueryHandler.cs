using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketAttachments.GetByTicket;

internal sealed class GetTicketAttachmentsByTicketQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : IQueryHandler<GetTicketAttachmentsByTicketQuery, List<TicketAttachmentResponse>>
{
    public async Task<Result<List<TicketAttachmentResponse>>> Handle(
        GetTicketAttachmentsByTicketQuery query,
        CancellationToken cancellationToken)
    {
        Ticket? ticket = await context.Tickets.SingleOrDefaultAsync(t => t.Id == query.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<List<TicketAttachmentResponse>>(TicketErrors.NotFound(query.TicketId));
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
                return Result.Failure<List<TicketAttachmentResponse>>(TicketErrors.Unauthorized());
            }
        }

        List<TicketAttachmentResponse> attachments = await context.TicketAttachments
            .Where(a => a.TicketId == query.TicketId)
            .OrderBy(a => a.UploadedAtUtc)
            .Select(a => new TicketAttachmentResponse
            {
                Id = a.Id,
                TicketId = a.TicketId,
                UploadedByUserId = a.UploadedByUserId,
                FileName = a.FileName,
                ContentType = a.ContentType,
                FileSizeBytes = a.FileSizeBytes,
                UploadedAtUtc = a.UploadedAtUtc
            })
            .ToListAsync(cancellationToken);

        return attachments;
    }
}
