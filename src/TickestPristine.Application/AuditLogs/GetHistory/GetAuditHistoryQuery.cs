using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.AuditLogs.GetHistory;

public sealed record GetAuditHistoryQuery(string EntityName, string EntityId) : IQuery<List<AuditLogResponse>>;
