using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;

namespace TickestPristine.Application.Tickets.GetAssignees;

/// <summary>
/// Responsáveis que podem ser atribuídos a um chamado (<see cref="TicketAssigneeEligibility"/>), para quem atende a
/// fila escolher no filtro de responsável sem precisar de <c>users:read</c>. <see cref="Search"/> procura cada palavra
/// no nome ou no sobrenome, sem diferenciar maiúsculas; <see cref="SectorId"/> filtra pelo setor do responsável.
/// </summary>
public sealed record GetTicketAssigneesQuery(
    string? Search,
    Guid? SectorId,
    PageRequest Paging) : IQuery<PagedResponse<TicketAssigneeResponse>>;
