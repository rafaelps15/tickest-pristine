# Modelos de paginação

Toda coleção que cresce sem limite é paginada. Só os catálogos de tamanho fixo ou administrado (roles, permissões, pequenas tabelas de apoio) devolvem lista. As peças ficam em `src/Application/Abstractions/Pagination/`: `PageRequest`/`PagedResponse<T>` (offset), `CursorPageRequest`/`CursorPagedResponse<T>` (cursor) e `PaginationExtensions`, que faz as duas leituras com Dapper. Sem repositório e sem serviço genérico de paginação.

## Peças

Os pedidos chegam do endpoint e ajustam os valores ausentes ou inválidos: página menor que 1 vira 1, tamanho menor que 1 vira 20 e tamanho maior que 100 vira 100.

```csharp
namespace Application.Abstractions.Pagination;

public sealed record PageRequest(int? Page, int? PageSize)
{
    internal int PageNumber => Page is > 0 ? Page.Value : 1;

    internal int Size => PaginationExtensions.NormalizePageSize(PageSize);

    // Em long: uma página muito alta (ex.: int.MaxValue) não pode estourar o cálculo do deslocamento.
    internal long Offset => (long)(PageNumber - 1) * Size;
}

public sealed record PagedResponse<T>(List<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasNextPage => Page < TotalPages;

    public bool HasPreviousPage => Page > 1;
}

public sealed record CursorPageRequest(DateTime? CursorDate, Guid? CursorId, int? PageSize)
{
    public bool HasCursor => CursorDate.HasValue && CursorId.HasValue;

    public DateTime CursorDateUtc => CursorDate.GetValueOrDefault() is { Kind: DateTimeKind.Local } local
        ? local.ToUniversalTime()
        : DateTime.SpecifyKind(CursorDate.GetValueOrDefault(), DateTimeKind.Utc);

    internal int Size => PaginationExtensions.NormalizePageSize(PageSize);
}

public sealed record CursorPagedResponse<T>(List<T> Items, DateTime? NextCursorDate, Guid? NextCursorId, bool HasNextPage);
```

A `PaginationExtensions` junta os parâmetros da página aos do handler e lê o resultado:

```csharp
using System.Data.Common;
using Dapper;

namespace Application.Abstractions.Pagination;

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
```

## Query paginada por offset

Para telas administrativas que pulam para qualquer página. A query carrega o `PageRequest` como último parâmetro:

```csharp
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.Todos.GetPage;

public sealed record GetTodoItemsPageQuery(bool? IsCompleted, PageRequest Paging)
    : IQuery<PagedResponse<TodoItemResponse>>;
```

O SQL tem dois comandos com o mesmo filtro, escrito uma vez: a contagem e depois a página, com ordem estável e `LIMIT @Limit OFFSET @Offset`. O handler passa os próprios parâmetros, e o `QueryPagedAsync` junta os da página:

```csharp
internal sealed class GetTodoItemsPageQueryHandler(IDbConnectionFactory dbConnectionFactory, IUserContext userContext)
    : IQueryHandler<GetTodoItemsPageQuery, PagedResponse<TodoItemResponse>>
{
    public async Task<Result<PagedResponse<TodoItemResponse>>> Handle(
        GetTodoItemsPageQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        const string filter = "user_id = @UserId AND (@IsCompleted::boolean IS NULL OR is_completed = @IsCompleted)";

        const string sql =
            $"""
             SELECT COUNT(*)::int
             FROM todo_items
             WHERE {filter};

             SELECT
                 id AS {nameof(TodoItemResponse.Id)},
                 title AS {nameof(TodoItemResponse.Title)},
                 is_completed AS {nameof(TodoItemResponse.IsCompleted)},
                 created_on_utc AS {nameof(TodoItemResponse.CreatedOnUtc)}
             FROM todo_items
             WHERE {filter}
             ORDER BY created_on_utc DESC, id DESC
             LIMIT @Limit OFFSET @Offset
             """;

        PagedResponse<TodoItemResponse> todoItems = await connection.QueryPagedAsync<TodoItemResponse>(
            sql,
            new { userContext.UserId, request.IsCompleted },
            request.Paging);

        return todoItems;
    }
}
```

## Query paginada por cursor

Para linhas do tempo, feeds, rolagem infinita e tabelas grandes, em que o cliente só avança. O cursor é composto (data + chave, porque a data pode repetir):

```csharp
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.Todos.GetFeed;

public sealed record GetTodoItemFeedQuery(CursorPageRequest Paging) : IQuery<CursorPagedResponse<TodoItemResponse>>;
```

O SQL mantém só os itens depois do cursor, com uma comparação de linha, ordena do mais novo para o mais antigo (data e depois chave, as duas descendentes) e termina em `LIMIT @Limit`. O `QueryCursorPagedAsync` preenche `@CursorDate`, `@CursorId` e `@Limit`, e os seletores dizem de onde sai o cursor de cada item:

```csharp
internal sealed class GetTodoItemFeedQueryHandler(IDbConnectionFactory dbConnectionFactory, IUserContext userContext)
    : IQueryHandler<GetTodoItemFeedQuery, CursorPagedResponse<TodoItemResponse>>
{
    public async Task<Result<CursorPagedResponse<TodoItemResponse>>> Handle(
        GetTodoItemFeedQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        const string sql =
            $"""
             SELECT
                 id AS {nameof(TodoItemResponse.Id)},
                 title AS {nameof(TodoItemResponse.Title)},
                 is_completed AS {nameof(TodoItemResponse.IsCompleted)},
                 created_on_utc AS {nameof(TodoItemResponse.CreatedOnUtc)}
             FROM todo_items
             WHERE user_id = @UserId
               AND (@CursorDate::timestamptz IS NULL OR (created_on_utc, id) < (@CursorDate::timestamptz, @CursorId::uuid))
             ORDER BY created_on_utc DESC, id DESC
             LIMIT @Limit
             """;

        CursorPagedResponse<TodoItemResponse> todoItems = await connection.QueryCursorPagedAsync<TodoItemResponse>(
            sql,
            new { userContext.UserId },
            request.Paging,
            todoItem => todoItem.CreatedOnUtc,
            todoItem => todoItem.Id);

        return todoItems;
    }
}
```

Os seletores rodam sobre o DTO, então o `{X}Response` precisa expor a data e a chave do cursor (aqui, `CreatedOnUtc` e `Id`). O `QueryCursorPagedAsync` devolve `CursorPagedResponse<T>(Items, NextCursorDate, NextCursorId, HasNextPage)`, sem total.

Quando cada item precisa de valores que o DTO não tem, para montar um campo calculado em memória, a sobrecarga com multi-mapping lê as colunas em dois objetos, separados pelo `splitOn`, e monta o item no `map`, como no multi-mapping de coleção filha; o cursor sai do item montado. A primeira coluna do segundo objeto nunca é nula (a chave da linha, por exemplo): com ela nula, o Dapper entrega o segundo objeto nulo.

## Endpoint

O endpoint recebe um único parâmetro de paginação — `[AsParameters] PageRequest paging` (`page`, `pageSize`) ou `[AsParameters] CursorPageRequest paging` (`cursorDate`, `cursorId`, `pageSize`) — e o repassa para a query:

```csharp
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Todos.GetFeed;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Todos;

internal sealed class GetFeed : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("todos/feed", async (
            [AsParameters] CursorPageRequest paging,
            IQueryHandler<GetTodoItemFeedQuery, CursorPagedResponse<TodoItemResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTodoItemFeedQuery(paging);

            Result<CursorPagedResponse<TodoItemResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Todos)
        .RequireAuthorization();
    }
}
```

A autorização segue o [endpoint.md](endpoint.md): listagens administrativas (normalmente as de offset) usam `.HasPermission(Permissions.X)` no lugar do `.RequireAuthorization()`.

## Regras

- Os filtros vêm antes da paginação, iguais na contagem e na página: uma `const string filter` local, interpolada nos dois comandos.
- A ordenação é estável, com a chave por último.
- Toda ordenação paginada tem um índice que a acompanhe na configuração do EF Core (filtro, data descendente e id).
- Os valores da página (`@Limit`, `@Offset`) e os do cursor (`@CursorDate`, `@CursorId`) vêm da `PaginationExtensions`; o handler nunca calcula o deslocamento, o item a mais nem o `DateTime.SpecifyKind`.
- Valores ausentes ou inválidos são ajustados pelos pedidos, nunca no endpoint ou no handler.
- O total (`COUNT`) só é calculado quando a tela mostra o número de páginas.
- Contagens para gráficos e painéis vêm de uma query de resumo própria, nunca da listagem paginada.
