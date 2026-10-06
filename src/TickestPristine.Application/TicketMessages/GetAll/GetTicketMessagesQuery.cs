using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;

namespace TickestPristine.Application.TicketMessages.GetAll;

/// <summary>
/// Mensagens de um chamado, das mais novas para as mais antigas, paginadas por cursor composto: a data e o id da
/// última mensagem da página anterior. Sem cursor, devolve a primeira página.
/// </summary>
public sealed record GetTicketMessagesQuery(
    Guid TicketId,
    CursorPageRequest Paging) : IQuery<CursorPagedResponse<TicketMessageResponse>>;
