namespace TickestPristine.Application.Abstractions.Pagination;

/// <summary>
/// Página pedida numa listagem paginada por offset. No endpoint entra com <c>[AsParameters]</c> e vira os parâmetros
/// <c>page</c> e <c>pageSize</c> da URL; valores ausentes ou inválidos são ajustados por
/// <see cref="PaginationExtensions.ToPagedListAsync{T}"/>.
/// </summary>
public sealed record PageRequest(int? Page, int? PageSize);
