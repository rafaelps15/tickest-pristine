# Modelos de slice de command

Os arquivos ficam em `src/Application/{Feature}/{UseCase}/`. Troque `{Feature}` (no plural, ex.: `Todos`), `{Entity}` (ex.: `TodoItem`) e os nomes do caso de uso em todo o texto.

## Command

Record posicional para poucos parâmetros:

```csharp
using Application.Abstractions.Messaging;

namespace Application.Todos.Archive;

public sealed record ArchiveTodoCommand(Guid TodoItemId) : ICommand;
```

Classe com setters quando há muitos parâmetros (igual ao `CreateTodoCommand`):

```csharp
using Application.Abstractions.Messaging;
using Domain.Todos;

namespace Application.Todos.Create;

public sealed class CreateTodoCommand : ICommand<Guid>
{
    public Guid UserId { get; set; }
    public string Description { get; set; }
    public DateTime? DueDate { get; set; }
    public List<string> Labels { get; set; } = [];
    public Priority Priority { get; set; }
}
```

- `ICommand` → o handler retorna `Result` (o endpoint responde `204 NoContent`).
- `ICommand<TResponse>` → o handler retorna `Result<TResponse>` (o endpoint responde `200 Ok`).

## Validator

Classe pública, na mesma pasta. Registrado automaticamente e executado pelo `ValidationDecorator` antes de o handler rodar.

```csharp
using FluentValidation;

namespace Application.Todos.Create;

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

`internal sealed`, construtor primário, `IApplicationDbContext` para acesso a dados. As guard clauses retornam `Result.Failure` com erros do Domain; o caminho feliz altera, dispara um evento de domínio, salva e retorna.

```csharp
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Todos;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Todos.Archive;

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

Observações:
- Dono do dado: filtre por `userContext.UserId` na consulta (preferível) ou compare explicitamente e retorne `Result.Failure(UserErrors.Unauthorized())`.
- `IDateTimeProvider` (do `SharedKernel`) para datas e horas — nunca `DateTime.UtcNow` direto.
- Se o command invalida dados de query em cache, injete `HybridCache` e chame `cache.RemoveAsync({Feature}CacheKeys.X(...), cancellationToken)` depois de salvar.
- Para um command com retorno (`ICommand<Guid>`), retorne o valor direto — `Result<T>` tem conversão implícita: `return todoItem.Id;`.

## Acréscimos no Domain (se necessário)

Método de fábrica do erro na classe `{Entity}Errors` existente, em `src/Domain/{Feature}/`:

```csharp
public static Error AlreadyArchived(Guid todoItemId) => Error.Problem(
    "TodoItems.AlreadyArchived",
    $"The todo item with Id = '{todoItemId}' is already archived.");
```

Tipo de erro → status HTTP (pelo `CustomResults.Problem`): `NotFound` → 404, `Conflict` → 409, `Problem`/`Validation` → 400, `Failure` → 500.

Evento de domínio, um arquivo cada em `src/Domain/{Feature}/`:

```csharp
using SharedKernel;

namespace Domain.Todos;

public sealed record TodoItemArchivedDomainEvent(Guid TodoItemId) : IDomainEvent;
```

Handler de evento opcional (camada Application, na pasta do caso de uso):

```csharp
using Domain.Todos;
using SharedKernel;

namespace Application.Todos.Archive;

internal sealed class TodoItemArchivedDomainEventHandler : IDomainEventHandler<TodoItemArchivedDomainEvent>
{
    public Task Handle(TodoItemArchivedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        // Side effects here (notifications, projections, ...)
        return Task.CompletedTask;
    }
}
```
