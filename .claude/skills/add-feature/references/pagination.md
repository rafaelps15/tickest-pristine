# Modelos de paginação

> Exemplos ilustrativos do modelo de referência (Todos). Use os caminhos, namespaces e referências vivas do `CLAUDE.md` do projeto.

Toda coleção que cresce sem limite é paginada. Só os catálogos de tamanho fixo ou administrado (roles, permissões, pequenas tabelas de apoio) devolvem lista. As peças ficam em `src/Application/Abstractions/Pagination/`: `PageRequest`/`PagedResponse<T>` (offset), `CursorPageRequest`/`CursorPagedResponse<T>` (cursor) e `PaginationExtensions`.

## Query paginada por offset

Para telas administrativas que pulam para qualquer página. A query carrega o `PageRequest` como último parâmetro:

```csharp
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.Todos.GetAll;

public sealed record GetTodosQuery(bool? IsCompleted, PageRequest Paging) : IQuery<PagedResponse<TodoResponse>>;
```

O handler filtra, ordena de forma estável (chave por último), projeta no DTO e termina com `ToPagedListAsync`, que faz a contagem e a página:

```csharp
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using SharedKernel;

namespace Application.Todos.GetAll;

internal sealed class GetTodosQueryHandler(IApplicationDbContext context, IUserContext userContext)
    : IQueryHandler<GetTodosQuery, PagedResponse<TodoResponse>>
{
    public async Task<Result<PagedResponse<TodoResponse>>> Handle(
        GetTodosQuery query,
        CancellationToken cancellationToken)
    {
        PagedResponse<TodoResponse> todos = await context.TodoItems
            .Where(todoItem => todoItem.UserId == userContext.UserId &&
                               (query.IsCompleted == null || todoItem.IsCompleted == query.IsCompleted))
            .OrderByDescending(todoItem => todoItem.CreatedAt)
            .ThenByDescending(todoItem => todoItem.Id)
            .Select(todoItem => new TodoResponse
            {
                Id = todoItem.Id,
                Description = todoItem.Description,
                IsCompleted = todoItem.IsCompleted,
                CreatedAt = todoItem.CreatedAt
            })
            .ToPagedListAsync(query.Paging, cancellationToken);

        return todos;
    }
}
```

## Query paginada por cursor

Para linhas do tempo, feeds, rolagem infinita e tabelas grandes, em que o cliente só avança. O cursor é composto (data + chave, porque a data pode repetir):

```csharp
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.Todos.GetFeed;

public sealed record GetTodoFeedQuery(CursorPageRequest Paging) : IQuery<CursorPagedResponse<TodoResponse>>;
```

O handler aplica os filtros próprios, chama `AfterCursor` (filtra depois do cursor e ordena do mais novo para o mais antigo), projeta no DTO e termina com `ToCursorPagedListAsync`, com os mesmos seletores de data e chave:

```csharp
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using SharedKernel;

namespace Application.Todos.GetFeed;

internal sealed class GetTodoFeedQueryHandler(IApplicationDbContext context, IUserContext userContext)
    : IQueryHandler<GetTodoFeedQuery, CursorPagedResponse<TodoResponse>>
{
    public async Task<Result<CursorPagedResponse<TodoResponse>>> Handle(
        GetTodoFeedQuery query,
        CancellationToken cancellationToken)
    {
        CursorPagedResponse<TodoResponse> todos = await context.TodoItems
            .Where(todoItem => todoItem.UserId == userContext.UserId)
            .AfterCursor(query.Paging, todoItem => todoItem.CreatedAt, todoItem => todoItem.Id)
            .Select(todoItem => new TodoResponse
            {
                Id = todoItem.Id,
                Description = todoItem.Description,
                IsCompleted = todoItem.IsCompleted,
                CreatedAt = todoItem.CreatedAt
            })
            .ToCursorPagedListAsync(query.Paging, todo => todo.CreatedAt, todo => todo.Id, cancellationToken);

        return todos;
    }
}
```

Os seletores do `ToCursorPagedListAsync` rodam sobre o DTO já projetado, então o `{X}Response` precisa expor a data e a chave do cursor (aqui, `CreatedAt` e `Id`). `ToCursorPagedListAsync` busca um item a mais para saber se há próxima página e devolve `CursorPagedResponse<T>(Items, NextCursorDate, NextCursorId, HasNextPage)`, sem total. Nunca escreva à mão o filtro `date < cursor || ...` nem o `DateTime.SpecifyKind` do cursor.

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
            IQueryHandler<GetTodoFeedQuery, CursorPagedResponse<TodoResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTodoFeedQuery(paging);

            Result<CursorPagedResponse<TodoResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Todos)
        .RequireAuthorization();
    }
}
```

Valores ausentes ou inválidos são ajustados dentro da `PaginationExtensions` (página mínima 1, tamanho padrão 20, máximo 100), nunca no endpoint ou no handler.

A autorização segue o [endpoint.md](endpoint.md): listagens administrativas (normalmente as de offset) usam `.HasPermission(Permissions.X)` no lugar do `.RequireAuthorization()`.

## Regras

- Os filtros vêm antes da paginação.
- A ordenação é estável, com a chave por último.
- Toda ordenação paginada tem um índice que a acompanhe na configuração do EF (filtro, data descendente e id).
- O total (`COUNT`) só é calculado quando a tela mostra o número de páginas.
- Contagens para gráficos e painéis vêm de uma query de resumo própria, nunca da listagem paginada.
