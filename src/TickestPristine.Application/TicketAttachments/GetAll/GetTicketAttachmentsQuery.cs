using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;

namespace TickestPristine.Application.TicketAttachments.GetAll;

/// <summary>
/// Anexos de um chamado, dos mais novos para os mais antigos, paginados por cursor composto: a data e o id do último
/// anexo da página anterior. Sem cursor, devolve a primeira página.
/// </summary>
public sealed record GetTicketAttachmentsQuery(
    Guid TicketId,
    CursorPageRequest Paging) : IQuery<CursorPagedResponse<TicketAttachmentResponse>>;
