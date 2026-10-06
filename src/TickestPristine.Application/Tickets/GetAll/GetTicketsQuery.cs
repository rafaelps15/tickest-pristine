using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Tickets.GetAll;

/// <summary>
/// Painel de chamados, do prazo de SLA mais próximo para o mais distante (os atrasados primeiro). Sem status
/// informado, lista só os ativos (Aberto e Em andamento). A busca procura no título.
/// </summary>
public sealed record GetTicketsQuery(
    IReadOnlyCollection<TicketStatus> Statuses,
    TicketPriority? Priority,
    Guid? SectorId,
    TicketSlaStatus? SlaStatus,
    string? Search,
    PageRequest Paging) : IQuery<PagedResponse<TicketSummaryResponse>>;
