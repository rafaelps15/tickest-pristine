using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.Abstractions.Pagination;

/// <summary>
/// Toda a paginação do back-end passa por aqui: os endpoints só repassam página, tamanho e cursor, e os handlers
/// terminam a consulta com <see cref="ToPagedListAsync{T}"/> (offset) ou com <see cref="AfterCursor{T}"/> seguido
/// de <see cref="ToCursorPagedListAsync{T}"/> (cursor). Página e tamanho ausentes ou inválidos são ajustados aqui,
/// sem erro: página ausente ou menor que 1 vira 1, tamanho ausente ou menor que 1 vira <see cref="DefaultPageSize"/>
/// e tamanho maior que <see cref="MaxPageSize"/> vira o máximo (evita abuso).
/// </summary>
public static class PaginationExtensions
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>
    /// Paginação por offset: conta os itens e busca a página pedida. A consulta já deve vir filtrada, ordenada de
    /// forma estável e projetada no DTO. Sem itens, ou com a página pedida além da última, não faz a segunda consulta.
    /// </summary>
    public static async Task<PagedResponse<T>> ToPagedListAsync<T>(
        this IQueryable<T> query,
        PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(paging);

        int page = NormalizePage(paging.Page);
        int pageSize = NormalizePageSize(paging.PageSize);

        int totalCount = await query.CountAsync(cancellationToken);

        // Em long: uma página muito alta (ex.: int.MaxValue) não pode estourar o cálculo do deslocamento.
        long offset = (long)(page - 1) * pageSize;

        if (offset >= totalCount)
        {
            return new PagedResponse<T>([], page, pageSize, totalCount);
        }

        List<T> items = await query
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<T>(items, page, pageSize, totalCount);
    }

    /// <summary>
    /// Primeira parte da paginação por cursor composto (data + id): quando a página pedida traz cursor, mantém só os
    /// itens depois dele (<c>data &lt; cursor || data == cursor &amp;&amp; id &lt; cursorId</c>), e ordena do mais
    /// novo para o mais antigo (data e depois id). Filtro e ordenação saem dos mesmos seletores, então nunca
    /// divergem; o EF traduz tudo para SQL, com o cursor como parâmetro.
    /// </summary>
    public static IOrderedQueryable<T> AfterCursor<T>(
        this IQueryable<T> query,
        CursorPageRequest paging,
        Expression<Func<T, DateTime>> date,
        Expression<Func<T, Guid>> id)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(paging);
        ArgumentNullException.ThrowIfNull(date);
        ArgumentNullException.ThrowIfNull(id);

        if (paging.HasCursor)
        {
            DateTime cursorDate = paging.CursorDateUtc;
            Guid cursorId = paging.CursorId.GetValueOrDefault();

            // As duas expressões passam a usar o mesmo parâmetro; os valores do cursor entram por closure, para o
            // EF enviá-los como parâmetros SQL em vez de constantes.
            ParameterExpression item = date.Parameters[0];
            Expression itemId = new ParameterReplacer(id.Parameters[0], item).Visit(id.Body);
            Expression<Func<DateTime>> cursorDateValue = () => cursorDate;
            Expression<Func<Guid>> cursorIdValue = () => cursorId;

            Expression afterCursor = Expression.OrElse(
                Expression.LessThan(date.Body, cursorDateValue.Body),
                Expression.AndAlso(
                    Expression.Equal(date.Body, cursorDateValue.Body),
                    Expression.LessThan(itemId, cursorIdValue.Body)));

            query = query.Where(Expression.Lambda<Func<T, bool>>(afterCursor, item));
        }

        return query.OrderByDescending(date).ThenByDescending(id);
    }

    /// <summary>
    /// Segunda parte da paginação por cursor composto: busca um item a mais para saber se há próxima página e devolve
    /// o cursor do último item. A consulta já deve ter passado por <see cref="AfterCursor{T}"/> e estar projetada no
    /// DTO; os seletores dizem de onde sai o cursor de cada item.
    /// </summary>
    public static async Task<CursorPagedResponse<T>> ToCursorPagedListAsync<T>(
        this IQueryable<T> query,
        CursorPageRequest paging,
        Func<T, DateTime> cursorDate,
        Func<T, Guid> cursorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(paging);
        ArgumentNullException.ThrowIfNull(cursorDate);
        ArgumentNullException.ThrowIfNull(cursorId);

        int pageSize = NormalizePageSize(paging.PageSize);

        List<T> items = await query
            .Take(pageSize + 1) // Um a mais para saber se há próxima página
            .ToListAsync(cancellationToken);

        bool hasNextPage = items.Count > pageSize;

        if (hasNextPage)
        {
            items.RemoveAt(items.Count - 1);
        }

        // Testa a lista, e não o último item contra null, para funcionar também com DTOs struct.
        if (items.Count == 0)
        {
            return new CursorPagedResponse<T>(items, null, null, hasNextPage);
        }

        T last = items[^1];

        return new CursorPagedResponse<T>(items, cursorDate(last), cursorId(last), hasNextPage);
    }

    private static int NormalizePage(int? page) => page is > 0 ? page.Value : 1;

    private static int NormalizePageSize(int? pageSize) =>
        pageSize is > 0 ? Math.Min(pageSize.Value, MaxPageSize) : DefaultPageSize;

    /// <summary>
    /// Troca um parâmetro de expressão por outro, para combinar dois seletores numa única lambda.
    /// </summary>
    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            ReferenceEquals(node, from) ? to : base.VisitParameter(node);
    }
}
