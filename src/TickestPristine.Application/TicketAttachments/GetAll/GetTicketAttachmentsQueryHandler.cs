using System.Data.Common;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketAttachments.GetAll;

internal sealed class GetTicketAttachmentsQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IUserContext userContext,
    TicketViewAccess ticketViewAccess)
    : IQueryHandler<GetTicketAttachmentsQuery, CursorPagedResponse<TicketAttachmentResponse>>
{
    public async Task<Result<CursorPagedResponse<TicketAttachmentResponse>>> Handle(
        GetTicketAttachmentsQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        Result access = await ticketViewAccess.EnsureCanViewAsync(connection, userContext.UserId, request.TicketId, cancellationToken);

        if (access.IsFailure)
        {
            return Result.Failure<CursorPagedResponse<TicketAttachmentResponse>>(access.Error);
        }

        const string sql =
            $"""
             SELECT
                 a.id AS {nameof(TicketAttachmentResponse.Id)},
                 a.ticket_id AS {nameof(TicketAttachmentResponse.TicketId)},
                 a.uploaded_by_user_id AS {nameof(TicketAttachmentResponse.UploadedByUserId)},
                 a.file_name AS {nameof(TicketAttachmentResponse.FileName)},
                 a.content_type AS {nameof(TicketAttachmentResponse.ContentType)},
                 a.file_size_bytes AS {nameof(TicketAttachmentResponse.FileSizeBytes)},
                 a.uploaded_at_utc AS {nameof(TicketAttachmentResponse.UploadedAtUtc)}
             FROM public.ticket_attachments a
             WHERE a.ticket_id = @TicketId
               AND a.deleted_at_utc IS NULL
               AND (@CursorDate::timestamptz IS NULL OR (a.uploaded_at_utc, a.id) < (@CursorDate::timestamptz, @CursorId::uuid))
             ORDER BY a.uploaded_at_utc DESC, a.id DESC
             LIMIT @Limit
             """;

        CursorPagedResponse<TicketAttachmentResponse> attachments =
            await connection.QueryCursorPagedAsync<TicketAttachmentResponse>(
                sql,
                new { request.TicketId },
                request.Paging,
                attachment => attachment.UploadedAtUtc,
                attachment => attachment.Id,
                cancellationToken);

        return attachments;
    }
}
