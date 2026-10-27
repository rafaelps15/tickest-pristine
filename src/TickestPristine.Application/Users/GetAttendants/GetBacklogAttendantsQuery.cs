using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;

namespace TickestPristine.Application.Users.GetAttendants;

/// <summary>
/// Atendentes que podem ser responsáveis por um chamado do Backlog (Aberto ou Em andamento), para escolher na
/// atribuição. <see cref="SearchTerm"/> busca cada palavra no nome, sobrenome, e-mail, setor ou departamento;
/// <see cref="SectorId"/> filtra pelo setor e <see cref="PrioritySectorId"/> só põe os desse setor primeiro.
/// </summary>
public sealed record GetBacklogAttendantsQuery(
    string? SearchTerm,
    Guid? SectorId,
    Guid? PrioritySectorId,
    PageRequest Paging) : IQuery<PagedResponse<AttendantResponse>>;
