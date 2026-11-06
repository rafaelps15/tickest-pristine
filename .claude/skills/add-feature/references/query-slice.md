# Modelos de slice de query

Os arquivos vão em `src/Application/{Feature}/{UseCase}/`. Queries são leituras: sem validator, sem eventos de domínio, sem `SaveChangesAsync`. A consulta lê o banco com Dapper, pela `IDbConnectionFactory`, e nunca pelo `IApplicationDbContext`.

## Query

```csharp
using Application.Abstractions.Messaging;

namespace Application.Todos.Get;

public sealed record GetTodoItemsQuery : IQuery<IReadOnlyCollection<TodoItemResponse>>;
```

Com parâmetros: `public sealed record GetTodoItemByIdQuery(Guid TodoItemId) : IQuery<TodoItemResponse>;`. As propriedades da query são os parâmetros do SQL: o handler passa a própria query ao Dapper, e cada `@Nome` do SQL é uma propriedade dela.

## DTO de resposta

Fica ao lado da query. Plano, fácil de serializar, nunca uma entidade de domínio; como DTO, tem setters públicos, que o Dapper preenche pelo alias de cada coluna.

```csharp
namespace Application.Todos.Get;

public sealed class TodoItemResponse
{
    public Guid Id { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public bool IsCompleted { get; set; }
}
```

Cada pasta de caso de uso tem o próprio `TodoItemResponse` — não compartilhe DTOs entre slices, mesmo que hoje pareçam iguais.

## Handler

A consulta lê o banco com Dapper, pela `IDbConnectionFactory`: o SQL fica no handler, num literal `$"""`, com cada alias escrito por `nameof` da propriedade do DTO; a conexão é aberta com `await using`; os parâmetros vão num objeto, nunca concatenados no SQL. Coleção usa `QueryAsync<T>()` com `.AsList()`; item único usa `QuerySingleOrDefaultAsync<T>()` e devolve `Result.Failure<T>()` quando nada corresponde.

Os quatro modelos abaixo são obrigatórios: mantenha a estrutura, a formatação, a organização do SQL, o ciclo de vida da conexão, os métodos do Dapper e o tratamento do resultado. Os usings do handler são `System.Data.Common` (`DbConnection`), `Dapper`, `Application.Abstractions.Data` (`IDbConnectionFactory`), `Application.Abstractions.Messaging` e `SharedKernel`, mais o namespace do domínio dos erros (`Domain.Todos`) quando o handler devolve `Result.Failure`.

### Coleção

```csharp
internal sealed class GetTodoItemsQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetTodoItemsQuery, IReadOnlyCollection<TodoItemResponse>>
{
    public async Task<Result<IReadOnlyCollection<TodoItemResponse>>> Handle(
        GetTodoItemsQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        const string sql =
            $"""
             SELECT
                 id AS {nameof(TodoItemResponse.Id)},
                 title AS {nameof(TodoItemResponse.Title)},
                 description AS {nameof(TodoItemResponse.Description)},
                 is_completed AS {nameof(TodoItemResponse.IsCompleted)}
             FROM todo_items
             """;

        List<TodoItemResponse> todoItems = (await connection.QueryAsync<TodoItemResponse>(sql, request)).AsList();

        return todoItems;
    }
}
```

### Item único

Lê um item pelos parâmetros da query.

```csharp
internal sealed class GetTodoItemQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetTodoItemQuery, TodoItemResponse>
{
    public async Task<Result<TodoItemResponse>> Handle(
        GetTodoItemQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        const string sql =
            $"""
             SELECT
                 id AS {nameof(TodoItemResponse.Id)},
                 title AS {nameof(TodoItemResponse.Title)},
                 description AS {nameof(TodoItemResponse.Description)},
                 is_completed AS {nameof(TodoItemResponse.IsCompleted)}
             FROM todo_items
             WHERE title = @Title
             """;

        TodoItemResponse? todoItem = await connection.QuerySingleOrDefaultAsync<TodoItemResponse>(sql, request);

        if (todoItem is null)
        {
            return Result.Failure<TodoItemResponse>(TodoItemErrors.NotFound());
        }

        return todoItem;
    }
}
```

### Por identificador

Lê um item pelo id e devolve uma falha explícita quando ele não existe.

```csharp
internal sealed class GetTodoItemByIdQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetTodoItemByIdQuery, TodoItemResponse>
{
    public async Task<Result<TodoItemResponse>> Handle(
        GetTodoItemByIdQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        const string sql =
            $"""
             SELECT
                 id AS {nameof(TodoItemResponse.Id)},
                 title AS {nameof(TodoItemResponse.Title)},
                 description AS {nameof(TodoItemResponse.Description)},
                 is_completed AS {nameof(TodoItemResponse.IsCompleted)}
             FROM todo_items
             WHERE id = @TodoItemId
             """;

        TodoItemResponse? todoItem = await connection.QuerySingleOrDefaultAsync<TodoItemResponse>(sql, request);

        if (todoItem is null)
        {
            return Result.Failure<TodoItemResponse>(TodoItemErrors.NotFound(request.TodoItemId));
        }

        return todoItem;
    }
}
```

### Projeção

Quando o caso de uso precisa só de parte do item, uma projeção explícita em SQL vai direto para o DTO dela:

```csharp
internal sealed class GetTodoItemSummaryQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetTodoItemSummaryQuery, TodoItemSummaryResponse>
{
    public async Task<Result<TodoItemSummaryResponse>> Handle(
        GetTodoItemSummaryQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        const string sql =
            $"""
             SELECT
                 id AS {nameof(TodoItemSummaryResponse.Id)},
                 title AS {nameof(TodoItemSummaryResponse.Title)},
                 is_completed AS {nameof(TodoItemSummaryResponse.IsCompleted)}
             FROM todo_items
             WHERE id = @TodoItemId
             """;

        TodoItemSummaryResponse? todoItem = await connection.QuerySingleOrDefaultAsync<TodoItemSummaryResponse>(
            sql,
            request);

        if (todoItem is null)
        {
            return Result.Failure<TodoItemSummaryResponse>(TodoItemErrors.NotFound(request.TodoItemId));
        }

        return todoItem;
    }
}
```

- A projeção é uma referência a mais: não traz abstração nova nem skill nova.
- Quando o SQL precisa de valores que a query não traz (o usuário atual, a hora atual), os parâmetros vão num objeto anônimo: `new { request.TodoItemId, userContext.UserId }`.
- Para coleções sem limite, pagine com `PageRequest` (offset) ou `CursorPageRequest` (cursor): [pagination.md](pagination.md).

## Convenções de SQL

- Tabelas e colunas em snake_case, como as migrations as criam; um alias por coluna, com `nameof` da propriedade do DTO, nunca `SELECT *`.
- Exclusão lógica: todo SQL que lê uma tabela com exclusão lógica escreve ele mesmo a condição do filtro global (`deleted = false`); o filtro global do EF Core não vale no Dapper.
- Uma listagem tem `ORDER BY` estável, com a chave por último.
- Enum gravado como texto o Dapper lê pelo nome; como parâmetro, ele vai como texto (`request.Priority?.ToString()`), comparado com a coluna de texto.
- Um parâmetro anulável que só aparece em `@Nome IS NULL OR ...` ganha cast explícito (`@DueBefore::timestamptz IS NULL`), para o banco saber o tipo dele quando chega vazio.

## Conexão

A `IDbConnectionFactory` fica em `Application/Abstractions/Data/` e abre a conexão da mesma fonte de dados que o EF Core usa (um pool só):

```csharp
using System.Data.Common;

namespace Application.Abstractions.Data;

/// <summary>Abre conexões com o banco para as consultas em SQL; quem chama fecha a conexão com await using.</summary>
public interface IDbConnectionFactory
{
    ValueTask<DbConnection> OpenConnectionAsync();
}
```

```csharp
using System.Data.Common;
using Application.Abstractions.Data;
using Npgsql;

namespace Infrastructure.Database;

internal sealed class DbConnectionFactory(NpgsqlDataSource dataSource) : IDbConnectionFactory
{
    public async ValueTask<DbConnection> OpenConnectionAsync() => await dataSource.OpenConnectionAsync();
}
```

```csharp
services.AddSingleton(new NpgsqlDataSourceBuilder(connectionString).Build());
services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
```

- Um tipo que o Dapper não lê como o EF Core grava (um valor guardado em JSON, por exemplo) ganha um `SqlMapper.TypeHandler<T>` na Infrastructure, registrado uma vez com `SqlMapper.AddTypeHandler(...)` junto da fábrica de conexões. Sem repositório e sem abstração genérica de persistência.

## Regras em consultas

Um método da entidade (`todoItem.IsOverdue(utcNow)`) funciona num objeto já carregado; o SQL de uma consulta não o chama. Quando uma consulta precisa da mesma regra, ela a escreve em SQL:

- na própria consulta, quando só uma consulta a usa; ou
- num trecho de SQL com nome, uma constante de uma `internal static class {Entity}Sql` na pasta da feature, quando várias consultas a compartilham:

```csharp
namespace Application.Todos;

internal static class TodoItemSql
{
    /// <summary>Tarefa atrasada, sobre todo_items com o alias t; espera o parâmetro @UtcNow.</summary>
    internal const string IsOverdue = "t.is_completed = false AND t.due_date < @UtcNow";
}
```

```csharp
const string sql =
    $"""
     SELECT
         t.id AS {nameof(TodoItemResponse.Id)},
         t.title AS {nameof(TodoItemResponse.Title)}
     FROM todo_items t
     WHERE t.user_id = @UserId AND {TodoItemSql.IsOverdue}
     """;
```

- O trecho só tem constantes: nenhum método, nenhuma extensão de `IQueryable`. O summary dele diz o alias e os parâmetros que ele espera.
- O trecho e o método da entidade expressam a mesma regra; os testes unitários da entidade cobrem o método, e um teste de integração de concordância confere que o SQL com o trecho devolve exatamente as linhas que o método aprova.
- Um command, que lê pelo `IApplicationDbContext`, usa para a mesma regra o `Expression<Func<T, bool>>` da entidade (padrão 9 da `domain-modeling`).

## Cache (opcional, só para leituras frequentes)

Envolva a consulta ao banco em `HybridCache.GetOrCreateAsync`, com uma chave da classe de chaves de cache da feature (veja o `GetTodoByIdQueryHandler` como exemplo real):

```csharp
namespace Application.Todos;

internal static class TodoCacheKeys
{
    internal static string ById(Guid userId, Guid todoItemId) => $"todos-{userId}-{todoItemId}";
}
```

```csharp
TodoItemResponse? todoItem = await cache.GetOrCreateAsync(
    TodoCacheKeys.ById(userId, request.TodoItemId),
    async _ =>
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<TodoItemResponse>(sql, request);
    },
    cancellationToken: cancellationToken);
```

Todo command que altera os dados em cache precisa invalidar a mesma chave com `cache.RemoveAsync(...)`. Se não der para listar as chaves afetadas, não use cache.
