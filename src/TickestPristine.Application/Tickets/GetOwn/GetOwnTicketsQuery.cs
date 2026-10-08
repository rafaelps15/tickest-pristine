using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;

namespace TickestPristine.Application.Tickets.GetOwn;

/// <summary>
/// Chamados abertos pelo usuário logado, do mais novo para o mais antigo. O usuário vem do token, nunca da requisição:
/// os chamados de outra pessoa ficam na listagem geral (filtro de solicitante), para quem gerencia chamados.
/// </summary>
public sealed record GetOwnTicketsQuery(PageRequest Paging) : IQuery<PagedResponse<OwnTicketResponse>>;
