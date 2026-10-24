using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;

namespace TickestPristine.Application.TicketHistories.GetAll;

/// <summary>
/// Histórico de um chamado, do mais novo para o mais antigo, paginado por cursor composto: a data e o id do último
/// registro da página anterior. Sem cursor, devolve a primeira página.
/// </summary>
public sealed record GetTicketHistoryQuery(
    Guid TicketId,
    CursorPageRequest Paging) : IQuery<CursorPagedResponse<TicketHistoryResponse>>;
