namespace TickestPristine.Application.Abstractions.Pagination;

/// <summary>
/// Página pedida numa listagem paginada por offset. No endpoint entra com <c>[AsParameters]</c> e vira os parâmetros
/// <c>page</c> e <c>pageSize</c> da URL; valores ausentes ou inválidos são ajustados aqui, sem erro, e a consulta em SQL
/// termina com <see cref="PaginationExtensions.QueryPagedAsync{T}"/>.
/// </summary>
public sealed record PageRequest(int? Page, int? PageSize)
{
    internal int PageNumber => Page is > 0 ? Page.Value : 1;

    internal int Size => PaginationExtensions.NormalizePageSize(PageSize);

    // Em long: uma página muito alta (ex.: int.MaxValue) não pode estourar o cálculo do deslocamento.
    internal long Offset => (long)(PageNumber - 1) * Size;
}
