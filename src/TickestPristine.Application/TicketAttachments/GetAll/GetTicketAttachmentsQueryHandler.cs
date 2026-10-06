using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Authorization;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketAttachments.GetAll;

internal sealed class GetTicketAttachmentsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : IQueryHandler<GetTicketAttachmentsQuery, CursorPagedResponse<TicketAttachmentResponse>>
{
    public async Task<Result<CursorPagedResponse<TicketAttachmentResponse>>> Handle(
        GetTicketAttachmentsQuery query,
        CancellationToken cancellationToken)
    {
        Result access = await TicketAccessGuard.EnsureCanViewAsync(
            context,
            permissionProvider,
            userContext.UserId,
            query.TicketId,
            cancellationToken);

        if (access.IsFailure)
        {
            return Result.Failure<CursorPagedResponse<TicketAttachmentResponse>>(access.Error);
        }

        CursorPagedResponse<TicketAttachmentResponse> attachments = await context.TicketAttachments
            .Where(a => a.TicketId == query.TicketId)
            .AfterCursor(query.Paging, a => a.UploadedAtUtc, a => a.Id)
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
            .ToCursorPagedListAsync(query.Paging, a => a.UploadedAtUtc, a => a.Id, cancellationToken);

        return attachments;
    }
}
