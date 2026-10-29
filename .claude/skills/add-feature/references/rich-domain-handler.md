# Modelos de handler com domínio rico

Os arquivos vão em `src/Application/{Feature}/{UseCase}/`, como em [command-slice.md](command-slice.md). A entidade guarda as próprias regras (veja a skill `domain-modeling`): o handler carrega, autoriza, chama a entidade, adiciona o que ela devolveu e salva.

## Command que chama um método da entidade

Antes — a regra mora no handler (o formato a remover ao enriquecer uma entidade):

```csharp
if (todoItem.IsCompleted)
{
    return Result.Failure(TodoItemErrors.AlreadyCompleted(command.TodoItemId));
}

todoItem.IsCompleted = true;
todoItem.CompletedAt = dateTimeProvider.UtcNow;

todoItem.Raise(new TodoItemCompletedDomainEvent(todoItem.Id));
```

Depois — o handler chama a entidade:

```csharp
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Todos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.Todos.Complete;

internal sealed class CompleteTodoCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    IUserContext userContext,
    HybridCache cache)
    : ICommandHandler<CompleteTodoCommand>
{
    public async Task<Result> Handle(CompleteTodoCommand command, CancellationToken cancellationToken)
    {
        TodoItem? todoItem = await context.TodoItems
            .SingleOrDefaultAsync(t => t.Id == command.TodoItemId && t.UserId == userContext.UserId, cancellationToken);

        if (todoItem is null)
        {
            return Result.Failure(TodoItemErrors.NotFound(command.TodoItemId));
        }

        Result result = todoItem.Complete(dateTimeProvider.UtcNow);

        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(TodoCacheKeys.ById(todoItem.UserId, todoItem.Id), cancellationToken);

        return Result.Success();
    }
}
```

- Nenhuma atribuição de propriedade e nenhum `Raise` no handler: `Complete` faz as duas coisas.
- A hora atual vem do `IDateTimeProvider` e é passada para a entidade.
- Em falha, o handler devolve antes do `SaveChangesAsync`; a entidade deixou o estado intacto.
- A invalidação de cache fica no handler.

## Command que insere o registro que a entidade devolve

```csharp
bool alreadyMember = await context.ProjectMembers
    .AnyAsync(m => m.ProjectId == project.Id && m.UserId == command.UserId, cancellationToken);

if (alreadyMember)
{
    return Result.Failure(ProjectErrors.MemberAlreadyAdded(command.UserId));
}

ProjectMember member = project.AddMember(command.UserId);

context.ProjectMembers.Add(member);

await context.SaveChangesAsync(cancellationToken);
```

- A conferência que precisa de consulta fica no handler; a entidade cria o filho e dispara o evento; um único `SaveChangesAsync` salva os dois.

Um registro de histórico segue o mesmo formato: o método da entidade que muda o estado também cria a entrada, e o handler a adiciona antes do único save.

```csharp
Result<TodoItemHistoryEntry> result = todoItem.Reopen(userContext.UserId, dateTimeProvider.UtcNow);

if (result.IsFailure)
{
    return result;
}

context.TodoItemHistory.Add(result.Value);

await context.SaveChangesAsync(cancellationToken);
```

- A mudança e o histórico são salvos juntos ou não são salvos; um handler de evento de domínio rodaria depois do save, em outra transação.

## Objeto de valor criado no handler

```csharp
Result<ProjectName> nameResult = ProjectName.Create(command.Name);

if (nameResult.IsFailure)
{
    return Result.Failure<Guid>(nameResult.Error);
}

var project = Project.Create(userContext.UserId, nameResult.Value);

context.Projects.Add(project);

await context.SaveChangesAsync(cancellationToken);

return project.Id;
```

