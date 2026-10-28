using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Tickets.GetAll;

/// <summary>
/// Todos os chamados do sistema, para quem gerencia chamados. Sem status informado, lista todos os status; sem setor
/// informado, todos os setores. Os filtros de solicitante, responsável e setor permitem acompanhar um funcionário ou um
/// setor; a busca procura no título. <c>UnassignedOnly</c> lista só os chamados sem responsável e, quando ligado, ignora
/// o filtro de responsável. Os períodos de abertura e de resolução (primeira resolução) são datas locais de São Paulo,
/// inclusivas nas duas pontas e independentes entre si; quando há período de resolução, o chamado nunca resolvido fica
/// de fora. Ordena dos mais recentes para os mais antigos, ou do prazo de SLA mais próximo para o mais distante (painel
/// de SLA).
/// </summary>
public sealed record GetTicketsQuery(
    IReadOnlyCollection<TicketStatus> Statuses,
    TicketPriority? Priority,
    IReadOnlyCollection<Guid> SectorIds,
    Guid? RequesterId,
    Guid? AssignedToUserId,
    bool UnassignedOnly,
    TicketSlaStatus? SlaStatus,
    string? Search,
    DateOnly? CreatedFrom,
    DateOnly? CreatedTo,
    DateOnly? ResolvedFrom,
    DateOnly? ResolvedTo,
    TicketSortOrder Sort,
    PageRequest Paging) : IQuery<PagedResponse<TicketSummaryResponse>>;
