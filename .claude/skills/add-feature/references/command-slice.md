# Modelos de slice de command

Os arquivos vão em `src/Application/{Feature}/{UseCase}/`. Troque `{Feature}` (plural, ex.: `Todos`), `{Entity}` (ex.: `TodoItem`) e os nomes do caso de uso em todo o texto.

## Command

Record posicional para poucos parâmetros:

```csharp
using Application.Abstractions.Messaging;

namespace Application.Todos.Archive;

public sealed record ArchiveTodoCommand(Guid TodoItemId) : ICommand;
```

Classe com propriedades graváveis quando há muitos parâmetros (como o `CreateTodoCommand`); um command é um DTO, então setters públicos são aceitáveis aqui:

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

- `ICommand` → o handler devolve `Result` (o endpoint responde `204 NoContent`).
- `ICommand<TResponse>` → o handler devolve `Result<TResponse>` (o endpoint responde `200 Ok`).

## Validator

Classe pública, na mesma pasta. Registrado automaticamente e executado pelo `ValidationDecorator` antes do handler.

```csharp
using FluentValidation;

namespace Application.Todos.Create;

public class CreateTodoCommandValidator : AbstractValidator<CreateTodoCommand>
{
    public CreateTodoCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Priority).IsInEnum();
        RuleFor(c => c.Description).NotEmpty();
    }
}
```

- O validator confere o formato da entrada (`NotEmpty()`, `EmailAddress()`, `MinimumLength(6)`). O tamanho das colunas não se repete aqui: ele fica só na configuração do EF (`HasMaxLength`). Uma regra sobre o estado da entidade ou sobre o tempo (prazo anterior à criação) é da entidade (`TodoItem.Create` devolve `Result<TodoItem>`).

## Handler

`internal sealed`, construtor primário, `IApplicationDbContext` para o acesso a dados. O handler carrega, autoriza, chama o método da entidade, devolve a falha dele e salva; a entidade muda o próprio estado e dispara o próprio evento.

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

        Result result = todoItem.Archive(dateTimeProvider.UtcNow);

        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
```

Observações:
- Dono: filtre por `userContext.UserId` na consulta (preferido) ou compare explicitamente e devolva `Result.Failure(UserErrors.Unauthorized())` ([authorization.md](authorization.md)).
- `IDateTimeProvider` (do `SharedKernel`) para datas — nunca `DateTime.UtcNow` direto; o handler passa a hora para a entidade.
- Se o command invalidar dados de query em cache, injete `HybridCache` e chame `cache.RemoveAsync({Feature}CacheKeys.X(...), cancellationToken)` depois de salvar.
- Num command com retorno (`ICommand<Guid>`), devolva o valor direto — `Result<T>` tem conversão implícita: `return todoItem.Id;`.
- Outros formatos (filho, histórico, objetos de valor): [rich-domain-handler.md](rich-domain-handler.md).

## Acréscimos no domínio (se necessário)

O método da entidade que o handler chama, em `src/Domain/{Feature}/{Entity}.cs` (padrão 1 da `domain-modeling`):

```csharp
/// <summary>Arquiva a tarefa; em falha, nada muda e nenhum evento é disparado.</summary>
public Result Archive(DateTime utcNow)
{
    if (IsArchived)
    {
        return Result.Failure(TodoItemErrors.AlreadyArchived(Id));
    }

    IsArchived = true;
    ArchivedAt = utcNow;

    Raise(new TodoItemArchivedDomainEvent(Id));

    return Result.Success();
}
```

Fábrica de erro na classe `{Entity}Errors` existente em `src/Domain/{Feature}/`:

```csharp
public static Error AlreadyArchived(Guid todoItemId) => Error.Problem(
    "TodoItems.AlreadyArchived",
    $"A tarefa com o Id = '{todoItemId}' já está arquivada.");
```

Tipo do erro → status HTTP (pelo `CustomResults.Problem`): `NotFound` → 404, `Conflict` → 409, `Problem`/`Validation` → 400, `Failure` → 500.

Evento de domínio, um arquivo cada em `src/Domain/{Feature}/`:

```csharp
using SharedKernel;

namespace Domain.Todos;

public sealed record TodoItemArchivedDomainEvent(Guid TodoItemId) : IDomainEvent;
```

Uma reação ao evento (notificação, projeção) é um handler de evento de domínio em `src/Application/{Feature}/{Event}/`, com o nome `{Action}On{Event}DomainEventHandler`: [domain-event-handler.md](domain-event-handler.md).
