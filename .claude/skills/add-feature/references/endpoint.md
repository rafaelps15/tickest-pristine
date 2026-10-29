# Modelos de endpoint

Um arquivo por caso de uso em `src/Web.Api/Endpoints/{Feature}/{UseCase}.cs`. Os endpoints implementam `IEndpoint` e são descobertos automaticamente por `AddEndpoints`/`MapEndpoints` — sem registro.

## Command com corpo na resposta (POST → 200 + valor)

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

## Command sem retorno a partir de parâmetro de rota (PUT/DELETE → 204)

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

## Regras

- Rotas em minúsculas, no plural, sem barra no início: `todos`, `todos/{id:guid}`, `users/{userId:guid}/todos`. Restrições de rota (`:guid`) em todo parâmetro tipado.
- A classe `Request` aninhada existe só quando há corpo JSON; ela é mapeada 1:1 para o command dentro da lambda (valores de enum chegam como `int` e são convertidos).
- Resolva a interface do handler (`ICommandHandler<...>` / `IQueryHandler<...>`) direto como parâmetro da lambda — a instância decorada é injetada.
- Termine sempre com `.WithTags(Tags.{Feature})` e `.RequireAuthorization()` (ou `.HasPermission(Permissions.X)` quando existir a constante de permissão, que recebe uma única permissão). Acrescente a constante da feature em `Tags.cs` se for nova.
- Falhas nunca ganham resposta feita à mão — `CustomResults.Problem` converte o `Error` em ProblemDetails (RFC 7807) com o status certo.
