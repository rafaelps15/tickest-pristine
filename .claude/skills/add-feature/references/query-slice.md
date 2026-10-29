# Modelos de slice de query

Os arquivos vão em `src/Application/{Feature}/{UseCase}/`. Queries são leituras: sem validator, sem eventos de domínio, sem `SaveChangesAsync`.

## Query

```csharp
using Application.Abstractions.Messaging;

namespace Application.Todos.GetOverdue;

public sealed record GetOverdueTodosQuery : IQuery<List<TodoResponse>>;
```

Com parâmetros: `public sealed record GetTodoByIdQuery(Guid TodoItemId) : IQuery<TodoResponse>;`

## DTO de resposta

Fica ao lado da query. Plano, fácil de serializar, nunca uma entidade de domínio; como DTO, tem setters públicos.

```csharp
namespace Application.Todos.GetOverdue;

public sealed class TodoResponse
{
    public Guid Id { get; set; }

    public string Description { get; set; }

    public DateTime? DueDate { get; set; }

    public List<string> Labels { get; set; } = [];
}
```

Cada pasta de caso de uso tem o próprio `TodoResponse` — não compartilhe DTOs entre slices, mesmo que hoje pareçam iguais.

## Handler

Limite ao usuário atual, filtre com um predicado que o EF Core consiga traduzir e projete com `.Select` direto no DTO:

```csharp
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Todos;
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
            .Where(todoItem => todoItem.UserId == userContext.UserId)
            .Where(TodoItem.OverdueAt(dateTimeProvider.UtcNow))
            .Select(todoItem => new TodoResponse
            {
                Id = todoItem.Id,
                Description = todoItem.Description,
                DueDate = todoItem.DueDate,
                Labels = todoItem.Labels.ToList()
            })
            .ToListAsync(cancellationToken);

        return todos;
    }
}
```

Em queries de um único item, devolva `Result.Failure<TodoResponse>(TodoItemErrors.NotFound(id))` quando nada corresponder.

Para coleções sem limite, pagine com `PageRequest` (offset) ou `CursorPageRequest` (cursor): [pagination.md](pagination.md).

## Regras em consultas

Um método da entidade (`todoItem.IsOverdue(utcNow)`) funciona num objeto já carregado; o EF Core não consegue traduzi-lo para SQL, então uma consulta nunca o chama dentro de `Where` ou `Select`. Quando uma consulta precisa da mesma regra, ela usa um predicado:

- escrito na própria consulta, quando só uma consulta o usa; ou
- um `Expression<Func<T, bool>>` na entidade, quando várias consultas o compartilham:

```csharp
/// <summary>Predicado traduzível das tarefas atrasadas, para consultas.</summary>
public static Expression<Func<TodoItem, bool>> OverdueAt(DateTime utcNow) =>
    todoItem => !todoItem.IsCompleted && todoItem.DueDate < utcNow;
```

- O predicado e o método da entidade expressam a mesma regra; os testes unitários da entidade cobrem o método, e o teste de integração cobre a consulta traduzida.

## Cache (opcional, só para leituras frequentes)

**Adaptação do Tickest:** nenhuma query da Application usa cache; o único uso de `HybridCache` é o `PermissionProvider` (Infrastructure). Os modelos abaixo não se aplicam aqui sem uma decisão explícita.

Envolva a consulta ao banco em `HybridCache.GetOrCreateAsync`, com uma chave da classe de chaves de cache da feature:

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

Todo command que altera os dados em cache precisa invalidar a mesma chave com `cache.RemoveAsync(...)`. Se não der para listar as chaves afetadas, não use cache.
