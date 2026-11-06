using System.Data.Common;
using Dapper;

namespace TickestPristine.Application.Abstractions.Pagination;

/// <summary>
/// Toda a paginação do back-end passa por aqui: os endpoints só repassam página, tamanho e cursor, e os handlers
/// terminam a consulta em SQL com <see cref="QueryPagedAsync{T}"/> (offset) ou com <see cref="QueryCursorPagedAsync{T}"/>
/// (cursor). Página e tamanho ausentes ou inválidos são ajustados pelos pedidos, sem erro: página ausente ou menor que 1
/// vira 1, tamanho ausente ou menor que 1 vira <see cref="DefaultPageSize"/> e tamanho maior que
/// <see cref="MaxPageSize"/> vira o máximo (evita abuso).
/// </summary>
public static class PaginationExtensions
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>
    /// Paginação por offset: o SQL traz a contagem e depois a página, com os mesmos filtros, numa ida só ao banco;
    /// além da última página, devolve os itens vazios e o total.
    /// </summary>
    public static async Task<PagedResponse<T>> QueryPagedAsync<T>(
        this DbConnection connection,
        string sql,
        object? parameters,
        PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        var sqlParameters = new DynamicParameters(parameters);
        sqlParameters.Add("Limit", paging.Size);
        sqlParameters.Add("Offset", paging.Offset);

        await using SqlMapper.GridReader results = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, sqlParameters, cancellationToken: cancellationToken));

        int totalCount = await results.ReadSingleAsync<int>();
        List<T> items = (await results.ReadAsync<T>()).AsList();

        return new PagedResponse<T>(items, paging.PageNumber, paging.Size, totalCount);
    }

    /// <summary>
    /// Paginação por cursor composto (data + id): busca um item a mais que a página para saber se há próxima e devolve
    /// o cursor do último item; sem o cursor completo, os parâmetros do cursor vão nulos e a listagem começa na primeira
    /// página.
    /// </summary>
    public static async Task<CursorPagedResponse<T>> QueryCursorPagedAsync<T>(
        this DbConnection connection,
        string sql,
        object? parameters,
        CursorPageRequest paging,
        Func<T, DateTime> cursorDate,
        Func<T, Guid> cursorId,
        CancellationToken cancellationToken = default)
    {
        List<T> items = (await connection.QueryAsync<T>(
            new CommandDefinition(sql, CursorParameters(parameters, paging), cancellationToken: cancellationToken))).AsList();

        return ToCursorPage(items, paging.Size, cursorDate, cursorId);
    }

    /// <summary>
    /// A mesma paginação por cursor, com cada linha montada pelo <paramref name="map"/> a partir dos dois objetos que o
    /// <paramref name="splitOn"/> separa (multi-mapping); o cursor sai do item montado.
    /// </summary>
    public static async Task<CursorPagedResponse<TResult>> QueryCursorPagedAsync<TFirst, TSecond, TResult>(
        this DbConnection connection,
        string sql,
        object? parameters,
        CursorPageRequest paging,
        Func<TFirst, TSecond, TResult> map,
        string splitOn,
        Func<TResult, DateTime> cursorDate,
        Func<TResult, Guid> cursorId,
        CancellationToken cancellationToken = default)
    {
        List<TResult> items = (await connection.QueryAsync(
            new CommandDefinition(sql, CursorParameters(parameters, paging), cancellationToken: cancellationToken),
            map,
            splitOn)).AsList();

        return ToCursorPage(items, paging.Size, cursorDate, cursorId);
    }

    // Os parâmetros do handler mais os do cursor: sem o cursor completo, eles vão nulos.
    private static DynamicParameters CursorParameters(object? parameters, CursorPageRequest paging)
    {
        var sqlParameters = new DynamicParameters(parameters);
        sqlParameters.Add("CursorDate", paging.HasCursor ? paging.CursorDateUtc : null);
        sqlParameters.Add("CursorId", paging.HasCursor ? paging.CursorId : null);
        sqlParameters.Add("Limit", paging.Size + 1); // Um a mais para saber se há próxima página

        return sqlParameters;
    }

    private static CursorPagedResponse<T> ToCursorPage<T>(
        List<T> items,
        int pageSize,
        Func<T, DateTime> cursorDate,
        Func<T, Guid> cursorId)
    {
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

    internal static int NormalizePageSize(int? pageSize) =>
        pageSize is > 0 ? Math.Min(pageSize.Value, MaxPageSize) : DefaultPageSize;
}
