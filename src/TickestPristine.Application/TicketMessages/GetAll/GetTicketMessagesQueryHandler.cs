using System.Data.Common;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketMessages.GetAll;

internal sealed class GetTicketMessagesQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IUserContext userContext,
    TicketViewAccess ticketViewAccess)
    : IQueryHandler<GetTicketMessagesQuery, CursorPagedResponse<TicketMessageResponse>>
{
    public async Task<Result<CursorPagedResponse<TicketMessageResponse>>> Handle(
        GetTicketMessagesQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        Result access = await ticketViewAccess.EnsureCanViewAsync(connection, userContext.UserId, request.TicketId, cancellationToken);

        if (access.IsFailure)
        {
            return Result.Failure<CursorPagedResponse<TicketMessageResponse>>(access.Error);
        }

        const string sql =
            $"""
             SELECT
                 m.id AS {nameof(TicketMessageResponse.Id)},
                 m.ticket_id AS {nameof(TicketMessageResponse.TicketId)},
                 m.author_user_id AS {nameof(TicketMessageResponse.AuthorUserId)},
                 m.content AS {nameof(TicketMessageResponse.Content)},
                 m.created_at_utc AS {nameof(TicketMessageResponse.CreatedAtUtc)},
                 m.edited_at_utc AS {nameof(TicketMessageResponse.EditedAtUtc)}
             FROM public.ticket_messages m
             WHERE m.ticket_id = @TicketId
               AND m.deleted_at_utc IS NULL
               AND (@CursorDate::timestamptz IS NULL OR (m.created_at_utc, m.id) < (@CursorDate::timestamptz, @CursorId::uuid))
             ORDER BY m.created_at_utc DESC, m.id DESC
             LIMIT @Limit
             """;

        CursorPagedResponse<TicketMessageResponse> messages = await connection.QueryCursorPagedAsync<TicketMessageResponse>(
            sql,
            new { request.TicketId },
            request.Paging,
            message => message.CreatedAtUtc,
            message => message.Id,
            cancellationToken);

        return messages;
    }
}
