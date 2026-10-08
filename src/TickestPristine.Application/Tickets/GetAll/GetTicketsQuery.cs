using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Tickets.GetAll;

/// <summary>
/// Todos os chamados do sistema, para quem gerencia chamados. Sem status informado, lista todos os status. Os filtros
/// de solicitante, responsável e setor permitem acompanhar um funcionário ou um setor; a busca procura no título.
/// Ordena dos mais recentes para os mais antigos, ou do prazo de SLA mais próximo para o mais distante (painel de SLA).
/// </summary>
public sealed record GetTicketsQuery(
    IReadOnlyCollection<TicketStatus> Statuses,
    TicketPriority? Priority,
    Guid? SectorId,
    Guid? RequesterId,
    Guid? AssignedToUserId,
    TicketSlaStatus? SlaStatus,
    string? Search,
    TicketSortOrder Sort,
    PageRequest Paging) : IQuery<PagedResponse<TicketSummaryResponse>>;
