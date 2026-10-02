namespace TickestPristine.Application.Abstractions.Pagination;

/// <summary>
/// Uma página de uma listagem paginada por offset, com os dados de navegação calculados a partir do total.
/// </summary>
public sealed record PagedResponse<T>(
    List<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasNextPage => Page < TotalPages;

    public bool HasPreviousPage => Page > 1;
}
