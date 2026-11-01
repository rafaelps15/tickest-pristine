using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;

namespace TickestPristine.Application.Tickets.GetOwn;

/// <summary>
/// Chamados abertos pelo usuário logado, da última atividade mais recente para a mais antiga. O usuário vem do token,
/// nunca da requisição: os chamados de outra pessoa ficam na listagem geral (filtro de solicitante), para quem gerencia
/// chamados. Sem grupo de status, lista todos os status; a busca procura no título ou na descrição, sem diferenciar maiúsculas.
/// </summary>
public sealed record GetOwnTicketsQuery(
    OwnTicketStatusGroup? StatusGroup,
    string? Search,
    PageRequest Paging) : IQuery<PagedResponse<OwnTicketResponse>>;
