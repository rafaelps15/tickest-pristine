# Templates de endpoint

Um arquivo por caso de uso em `src/Web.Api/Endpoints/{Feature}/{UseCase}.cs`. Os endpoints implementam `IEndpoint` e são descobertos automaticamente por `AddEndpoints`/`MapEndpoints` — não é preciso registrar nada.

## Command que devolve valor (POST → 200 + valor)

```csharp
using Application.Abstractions.Messaging;
using Application.Todos.Create;
using Domain.Todos;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Todos;

internal sealed class Create : IEndpoint
{
    public sealed class Request
    {
        public Guid UserId { get; set; }
        public string Description { get; set; }
        public DateTime? DueDate { get; set; }
        public List<string> Labels { get; set; } = [];
        public int Priority { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("todos", async (
            Request request,
            ICommandHandler<CreateTodoCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateTodoCommand
            {
                UserId = request.UserId,
                Description = request.Description,
                DueDate = request.DueDate,
                Labels = request.Labels,
                Priority = (Priority)request.Priority
            };

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Todos)
        .RequireAuthorization();
    }
}
```

## Command sem retorno, com parâmetro de rota (PUT/DELETE → 204)

```csharp
using Application.Abstractions.Messaging;
using Application.Todos.Archive;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Todos;

internal sealed class Archive : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("todos/{id:guid}/archive", async (
            Guid id,
            ICommandHandler<ArchiveTodoCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new ArchiveTodoCommand(id);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Todos)
        .RequireAuthorization();
    }
}
```

## Query (GET → 200)

```csharp
using Application.Abstractions.Messaging;
using Application.Todos.GetOverdue;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Todos;

internal sealed class GetOverdue : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("todos/overdue", async (
            IQueryHandler<GetOverdueTodosQuery, List<TodoResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetOverdueTodosQuery();

            Result<List<TodoResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Todos)
        .RequireAuthorization();
    }
}
```

## Query paginada (GET → 200 + `PagedResponse<T>`)

Os filtros vêm como parâmetros opcionais da URL, e a página num único parâmetro, `[AsParameters] PageRequest paging`, que vira `?page=&pageSize=`. Nunca declare `page`/`pageSize` soltos nem escreva `if (pageSize > 100)` ou parecidos no endpoint: ele só repassa o objeto, e a `PaginationExtensions` ajusta os valores no handler:

```csharp
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Todos.GetAll;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Todos;

internal sealed class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("todos", async (
            bool? isCompleted,
            string? search,
            [AsParameters] PageRequest paging,
            IQueryHandler<GetTodosQuery, PagedResponse<TodoSummaryResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTodosQuery(isCompleted, search, paging);

            Result<PagedResponse<TodoSummaryResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Todos)
        .RequireAuthorization();
    }
}
```

## Query por cursor (GET → 200 + página por cursor)

Troque o `PageRequest` pelo `[AsParameters] CursorPageRequest paging`, que vira `?cursorDate=&cursorId=&pageSize=`. Sem cursor, a API devolve a primeira página. O endpoint também só repassa o objeto:

```csharp
app.MapGet("todos/{todoItemId:guid}/comments", async (
    Guid todoItemId,
    [AsParameters] CursorPageRequest paging,
    IQueryHandler<GetTodoCommentsQuery, CursorPagedResponse<TodoCommentResponse>> handler,
    CancellationToken cancellationToken) =>
{
    var query = new GetTodoCommentsQuery(todoItemId, paging);

    Result<CursorPagedResponse<TodoCommentResponse>> result = await handler.Handle(query, cancellationToken);

    return result.Match(Results.Ok, CustomResults.Problem);
})
.WithTags(Tags.Todos)
.RequireAuthorization();
```

## Regras

- Rotas em minúsculas, no plural, sem barra inicial: `todos`, `todos/{id:guid}`, `users/{userId:guid}/todos`. Use restrição de tipo (`:guid`) em todo parâmetro tipado.
- A classe aninhada `Request` só existe quando há corpo JSON; ela é mapeada campo a campo para o command dentro da lambda (enums chegam como `int` e são convertidos).
- Receba a interface do handler (`ICommandHandler<...>` / `IQueryHandler<...>`) direto como parâmetro da lambda — a instância injetada já vem com os decorators.
- Termine sempre com `.RequireAuthorization()` e `.WithTags(Tags.{Feature})`. Quando o acesso depende só de uma permissão, use `.HasPermission(Permissions.{Feature}{Action})` antes do `.WithTags(...)`, com a constante no `Permissions.cs` da pasta do endpoint (`internal const string TodosManage = PermissionCodes.Todos.Manage;`). Código de permissão novo entra em `PermissionCodes` e ganha a sua instância em `Permission` (Domain), e exige migration. Se a feature for nova, adicione a constante em `Tags.cs`.
- Falhas nunca ganham resposta montada à mão — o `CustomResults.Problem` converte o `Error` em ProblemDetails (RFC 7807) com o status correto.
