using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.Abstractions.Pagination;

public static class PaginationExtensions
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>
    /// Executa a contagem e a página pedida da consulta. A consulta já deve vir filtrada e com uma ordenação estável.
    /// Página ausente ou menor que 1 vira 1; tamanho ausente ou menor que 1 vira <see cref="DefaultPageSize"/>,
    /// e o tamanho nunca passa de <see cref="MaxPageSize"/>.
    /// </summary>
    public static async Task<PagedResponse<T>> ToPagedResponseAsync<T>(
        this IQueryable<T> query,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken = default)
    {
        int currentPage = page is > 0 ? page.Value : 1;
        int currentPageSize = pageSize is > 0 ? Math.Min(pageSize.Value, MaxPageSize) : DefaultPageSize;

        int totalCount = await query.CountAsync(cancellationToken);

        List<T> items = await query
            .Skip((currentPage - 1) * currentPageSize)
            .Take(currentPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<T>(items, currentPage, currentPageSize, totalCount);
    }
}
