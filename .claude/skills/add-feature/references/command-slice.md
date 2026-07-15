# Command Slice Templates

Files go in `src/TickestPristine.Application/{Feature}/{UseCase}/`. Replace `{Feature}` (plural, e.g. `Todos`), `{Entity}` (e.g. `TodoItem`), and use-case names throughout.

## Command

Positional record for few parameters:

```csharp
using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Todos.Archive;

public sealed record ArchiveTodoCommand(Guid TodoItemId) : ICommand;
```

Class with init-style setters when there are many parameters (matches `CreateTodoCommand`):

```csharp
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Todos;

namespace TickestPristine.Application.Todos.Create;

public sealed class CreateTodoCommand : ICommand<Guid>
{
    public Guid UserId { get; set; }
    public string Description { get; set; }
    public DateTime? DueDate { get; set; }
    public List<string> Labels { get; set; } = [];
    public Priority Priority { get; set; }
}
```

- `ICommand` → handler returns `Result` (endpoint responds `204 NoContent`).
- `ICommand<TResponse>` → handler returns `Result<TResponse>` (endpoint responds `200 Ok`).

## Validator

Public class, same folder. Auto-registered and executed by `ValidationDecorator` before the handler runs.

```csharp
using FluentValidation;

namespace TickestPristine.Application.Todos.Create;

public class CreateTodoCommandValidator : AbstractValidator<CreateTodoCommand>
{
    public CreateTodoCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Priority).IsInEnum();
        RuleFor(c => c.Description).NotEmpty().MaximumLength(255);
        RuleFor(c => c.DueDate).GreaterThanOrEqualTo(DateTime.Today).When(x => x.DueDate.HasValue);
    }
}
```

## Handler

`internal sealed`, primary constructor, `IApplicationDbContext` for data access. Guard clauses return `Result.Failure` with Domain errors; the happy path mutates, raises a domain event, saves, and returns.

```csharp
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Todos;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Todos.Archive;

internal sealed class ArchiveTodoCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    IUserContext userContext)
    : ICommandHandler<ArchiveTodoCommand>
{
    public async Task<Result> Handle(ArchiveTodoCommand command, CancellationToken cancellationToken)
    {
        TodoItem? todoItem = await context.TodoItems
            .SingleOrDefaultAsync(
                t => t.Id == command.TodoItemId && t.UserId == userContext.UserId,
                cancellationToken);

        if (todoItem is null)
        {
            return Result.Failure(TodoItemErrors.NotFound(command.TodoItemId));
        }

        if (todoItem.IsArchived)
        {
            return Result.Failure(TodoItemErrors.AlreadyArchived(command.TodoItemId));
        }

        todoItem.IsArchived = true;
        todoItem.ArchivedAt = dateTimeProvider.UtcNow;

        todoItem.Raise(new TodoItemArchivedDomainEvent(todoItem.Id));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
```

Notes:
- Ownership: either filter by `userContext.UserId` in the query (preferred) or compare explicitly and return `Result.Failure(UserErrors.Unauthorized())`.
- `IDateTimeProvider` (from `SharedKernel`) for timestamps — never `DateTime.UtcNow` directly.
- If the command invalidates cached query data, inject `HybridCache` and call `cache.RemoveAsync({Feature}CacheKeys.X(...), cancellationToken)` after saving.
- For a returning command (`ICommand<Guid>`), return the value directly — `Result<T>` has an implicit conversion: `return todoItem.Id;`.

## Domain additions (if needed)

Error factory on the existing `{Entity}Errors` class in `src/TickestPristine.Domain/{Feature}/`:

```csharp
public static Error AlreadyArchived(Guid todoItemId) => Error.Problem(
    "TodoItems.AlreadyArchived",
    $"The todo item with Id = '{todoItemId}' is already archived.");
```

Error type → HTTP status (via `CustomResults.Problem`): `NotFound` → 404, `Conflict` → 409, `Problem`/`Validation` → 400, `Failure` → 500.

Domain event, one file each in `src/TickestPristine.Domain/{Feature}/`:

```csharp
using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Todos;

public sealed record TodoItemArchivedDomainEvent(Guid TodoItemId) : IDomainEvent;
```

Optional event handler (Application layer, in the use-case folder):

```csharp
using TickestPristine.Domain.Todos;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Todos.Archive;

internal sealed class TodoItemArchivedDomainEventHandler : IDomainEventHandler<TodoItemArchivedDomainEvent>
{
    public Task Handle(TodoItemArchivedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        // Side effects here (notifications, projections, ...)
        return Task.CompletedTask;
    }
}
```
