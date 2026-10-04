using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.AuditLogs.GetHistory;

internal sealed class GetAuditHistoryQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetAuditHistoryQuery, List<AuditLogResponse>>
{
    public async Task<Result<List<AuditLogResponse>>> Handle(
        GetAuditHistoryQuery query,
        CancellationToken cancellationToken)
    {
        List<AuditLogResponse> history = await context.AuditLogs
            .Where(a => a.EntityName == query.EntityName &&
                        a.EntityId == query.EntityId)
            .OrderByDescending(a => a.Timestamp)
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
            .ToListAsync(cancellationToken);

        return history;
    }
}
