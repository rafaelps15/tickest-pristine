using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;

namespace TickestPristine.Application.AuditLogs.GetHistory;

/// <summary>
/// Trilha de auditoria de uma entidade, da mudança mais nova para a mais antiga, paginada por cursor composto: o
/// horário e o id do último registro da página anterior. Sem cursor, devolve a primeira página.
/// </summary>
public sealed record GetAuditHistoryQuery(
    string EntityName,
    string EntityId,
    CursorPageRequest Paging) : IQuery<CursorPagedResponse<AuditLogResponse>>;
