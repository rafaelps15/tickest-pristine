using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.AuditLogs.GetHistory;

internal sealed class GetAuditHistoryQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetAuditHistoryQuery, CursorPagedResponse<AuditLogResponse>>
{
    public async Task<Result<CursorPagedResponse<AuditLogResponse>>> Handle(
        GetAuditHistoryQuery query,
        CancellationToken cancellationToken)
    {
        CursorPagedResponse<AuditLogResponse> history = await context.AuditLogs
            .Where(a => a.EntityName == query.EntityName &&
                        a.EntityId == query.EntityId)
            .AfterCursor(query.Paging, a => a.Timestamp, a => a.Id)
            .Select(a => new AuditLogResponse
            {
                Id = a.Id,
                EntityName = a.EntityName,
                EntityId = a.EntityId,
                Action = a.Action,
                UserId = a.UserId,
                Timestamp = a.Timestamp,
                OldValues = a.OldValues,
                NewValues = a.NewValues,
                AffectedColumns = a.AffectedColumns
            })
            .ToCursorPagedListAsync(query.Paging, a => a.Timestamp, a => a.Id, cancellationToken);

        return history;
    }
}
