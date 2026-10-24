using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Authorization;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketHistories.GetAll;

internal sealed class GetTicketHistoryQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : IQueryHandler<GetTicketHistoryQuery, CursorPagedResponse<TicketHistoryResponse>>
{
    public async Task<Result<CursorPagedResponse<TicketHistoryResponse>>> Handle(
        GetTicketHistoryQuery query,
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
            return Result.Failure<CursorPagedResponse<TicketHistoryResponse>>(access.Error);
        }

        CursorPagedResponse<TicketHistoryResponse> history = await context.TicketHistories
            .Where(h => h.TicketId == query.TicketId)
            .AfterCursor(query.Paging, h => h.OccurredAtUtc, h => h.Id)
            .Select(h => new TicketHistoryResponse
            {
                Id = h.Id,
                TicketId = h.TicketId,
                ChangedByUserId = h.ChangedByUserId,
                Action = h.Action,
                Description = TicketHistoryDescriptions.For(h.Action, h.OldValue, h.NewValue),
                OldValue = h.OldValue,
                NewValue = h.NewValue,
                OccurredAtUtc = h.OccurredAtUtc
            })
            .ToCursorPagedListAsync(query.Paging, h => h.OccurredAtUtc, h => h.Id, cancellationToken);

        return history;
    }
}
