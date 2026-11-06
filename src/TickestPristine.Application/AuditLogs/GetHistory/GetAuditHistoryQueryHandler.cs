using System.Data.Common;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.AuditLogs.GetHistory;

internal sealed class GetAuditHistoryQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetAuditHistoryQuery, CursorPagedResponse<AuditLogResponse>>
{
    public async Task<Result<CursorPagedResponse<AuditLogResponse>>> Handle(
        GetAuditHistoryQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            $"""
             SELECT
                 a.id AS {nameof(AuditLogResponse.Id)},
                 a.entity_name AS {nameof(AuditLogResponse.EntityName)},
                 a.entity_id AS {nameof(AuditLogResponse.EntityId)},
                 a.action AS {nameof(AuditLogResponse.Action)},
                 a.user_id AS {nameof(AuditLogResponse.UserId)},
                 a.timestamp AS {nameof(AuditLogResponse.Timestamp)},
                 a.old_values AS {nameof(AuditLogResponse.OldValues)},
                 a.new_values AS {nameof(AuditLogResponse.NewValues)},
                 a.affected_columns AS {nameof(AuditLogResponse.AffectedColumns)}
             FROM public.audit_logs a
             WHERE a.entity_name = @EntityName
               AND a.entity_id = @EntityId
               AND (@CursorDate::timestamptz IS NULL OR (a.timestamp, a.id) < (@CursorDate::timestamptz, @CursorId::uuid))
             ORDER BY a.timestamp DESC, a.id DESC
             LIMIT @Limit
             """;

        CursorPagedResponse<AuditLogResponse> history = await connection.QueryCursorPagedAsync<AuditLogResponse>(
            sql,
            new { request.EntityName, request.EntityId },
            request.Paging,
            auditLog => auditLog.Timestamp,
            auditLog => auditLog.Id,
            cancellationToken);

        return history;
    }
}
