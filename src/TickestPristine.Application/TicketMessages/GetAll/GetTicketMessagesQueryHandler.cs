using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Authorization;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketMessages.GetAll;

internal sealed class GetTicketMessagesQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : IQueryHandler<GetTicketMessagesQuery, CursorPagedResponse<TicketMessageResponse>>
{
    public async Task<Result<CursorPagedResponse<TicketMessageResponse>>> Handle(
        GetTicketMessagesQuery query,
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
            return Result.Failure<CursorPagedResponse<TicketMessageResponse>>(access.Error);
        }

        CursorPagedResponse<TicketMessageResponse> messages = await context.TicketMessages
            .Where(m => m.TicketId == query.TicketId)
            .AfterCursor(query.Paging, m => m.CreatedAtUtc, m => m.Id)
            .Select(m => new TicketMessageResponse
            {
                Id = m.Id,
                TicketId = m.TicketId,
                AuthorUserId = m.AuthorUserId,
                Content = m.Content,
                CreatedAtUtc = m.CreatedAtUtc,
                EditedAtUtc = m.EditedAtUtc
            })
            .ToCursorPagedListAsync(query.Paging, m => m.CreatedAtUtc, m => m.Id, cancellationToken);

        return messages;
    }
}
