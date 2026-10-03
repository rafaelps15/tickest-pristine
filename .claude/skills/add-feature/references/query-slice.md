# Templates de slice de query

Os arquivos ficam em `src/Application/{Feature}/{UseCase}/`. Queries são leituras: sem validator, sem eventos de domínio, sem `SaveChangesAsync`.

## Query

```csharp
using Application.Abstractions.Messaging;

namespace Application.Todos.GetOverdue;

public sealed record GetOverdueTodosQuery : IQuery<List<TodoResponse>>;
```

Com parâmetros: `public sealed record GetTodoByIdQuery(Guid TodoItemId) : IQuery<TodoResponse>;`

## DTO de resposta

Fica ao lado da query. É plano, fácil de serializar e nunca é uma entidade de domínio.

```csharp
namespace Application.Todos.GetOverdue;

public sealed class TodoResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Description { get; set; }
    public DateTime? DueDate { get; set; }
    public List<string> Labels { get; set; } = [];
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
```

Cada pasta de caso de uso tem o seu próprio `TodoResponse` — não compartilhe DTOs entre slices, mesmo que hoje pareçam iguais.

## Handler

Restrinja ao usuário atual e projete com `.Select` direto no DTO:

```csharp
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Todos.GetOverdue;

internal sealed class GetOverdueTodosQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetOverdueTodosQuery, List<TodoResponse>>
{
    public async Task<Result<List<TodoResponse>>> Handle(
        GetOverdueTodosQuery query,
        CancellationToken cancellationToken)
    {
        List<TodoResponse> todos = await context.TodoItems
            .Where(todoItem => todoItem.UserId == userContext.UserId &&
                               !todoItem.IsCompleted &&
                               todoItem.DueDate < dateTimeProvider.UtcNow)
            .Select(todoItem => new TodoResponse
            {
                Id = todoItem.Id,
                UserId = todoItem.UserId,
                Description = todoItem.Description,
                DueDate = todoItem.DueDate,
                Labels = todoItem.Labels,
                IsCompleted = todoItem.IsCompleted,
                CreatedAt = todoItem.CreatedAt,
                CompletedAt = todoItem.CompletedAt
            })
            .ToListAsync(cancellationToken);

        return todos;
    }
}
```

Em queries de um único item, retorne `Result.Failure<TodoResponse>(TodoItemErrors.NotFound(id))` quando nada for encontrado.

## Cache (opcional, só para leituras frequentes)

Envolva a consulta ao banco em `HybridCache.GetOrCreateAsync`, com uma chave vinda da classe de chaves de cache da feature (o `GetTodoByIdQueryHandler` é o exemplo real):

```csharp
namespace Application.Todos;

internal static class TodoCacheKeys
{
    internal static string ById(Guid userId, Guid todoItemId) => $"todos-{userId}-{todoItemId}";
}
```

```csharp
TodoResponse? todo = await cache.GetOrCreateAsync(
    TodoCacheKeys.ById(userId, query.TodoItemId),
    async cancellation => await context.TodoItems
        .Where(...)
        .Select(...)
        .SingleOrDefaultAsync(cancellation),
    cancellationToken: cancellationToken);
```

Todo command que altera o dado em cache precisa invalidar a mesma chave com `cache.RemoveAsync(...)`. Se não for possível listar as chaves afetadas, não use cache.
