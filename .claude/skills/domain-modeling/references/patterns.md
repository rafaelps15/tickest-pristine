# Modelos de modelagem de domínio

Os arquivos vão em `src/Domain/{Feature}/`; as peças compartilhadas, em `src/SharedKernel/`. Troque `{Feature}` (plural, ex.: `Todos`) e `{Entity}` (ex.: `TodoItem`) em todo o texto.

## 1. Método de comportamento que devolve `Result` e dispara o próprio evento

```csharp
/// <summary>Conclui a tarefa; em falha, nada muda e nenhum evento é disparado.</summary>
public Result Complete(DateTime utcNow)
{
    if (IsCompleted)
    {
        return Result.Failure(TodoItemErrors.AlreadyCompleted(Id));
    }

    if (utcNow < CreatedAt)
    {
        return Result.Failure(TodoItemErrors.CompletedBeforeCreation(Id));
    }

    IsCompleted = true;
    CompletedAt = utcNow;

    Raise(new TodoItemCompletedDomainEvent(Id));

    return Result.Success();
}
```

- Toda conferência roda antes da primeira atribuição. Em falha, o estado e os eventos pendentes ficam exatamente como antes da chamada.
- Cada conferência devolve o próprio erro de `{Entity}Errors`.
- O evento é disparado dentro do método, nunca pelo handler.
- A hora atual chega como parâmetro (o handler passa `dateTimeProvider.UtcNow`).

## 2. Fábrica `Create` que dispara o evento `Created`

Quando a criação não pode falhar, `Create` devolve a entidade:

```csharp
public sealed class Project : Entity
{
    private Project()
    {
    }

    public Guid Id { get; private set; }

    public Guid OwnerId { get; private set; }

    public ProjectName Name { get; private set; }

    public static Project Create(User owner, ProjectName name)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            OwnerId = owner.Id,
            Name = name
        };

        project.Raise(new ProjectCreatedDomainEvent(project.Id));

        return project;
    }
}
```

Quando uma regra de negócio pode recusar a criação, `Create` devolve `Result<T>` e só dispara o evento no sucesso:

```csharp
/// <summary>Cria a tarefa, ou devolve o erro quando o prazo é anterior à criação.</summary>
public static Result<TodoItem> Create(Guid userId, string description, Priority priority, DateTime? dueDate, IEnumerable<string> labels, DateTime createdAt)
{
    if (dueDate < createdAt)
    {
        return Result.Failure<TodoItem>(TodoItemErrors.DueDateBeforeCreation);
    }

    var todoItem = new TodoItem
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Description = description,
        Priority = priority,
        DueDate = dueDate,
        CreatedAt = createdAt
    };

    todoItem._labels.AddRange(labels);

    todoItem.Raise(new TodoItemCreatedDomainEvent(todoItem.Id));

    return todoItem;
}
```

- O único construtor é privado e sem parâmetros: o `Create` é a única entrada e monta a entidade com inicializador de objeto, então nenhuma entidade existe num estado inválido.

## 3. A entidade não repete o validator

- O validator confere o formato da entrada (`NotEmpty()`, `EmailAddress()`, `MinimumLength(6)`); o `Create` e os métodos de comportamento confiam nele e não acrescentam guardas.
- Uma falha que o usuário pode provocar e que o validator não enxerga (prazo anterior à criação, tarefa já concluída) é um `Result.Failure(...)` com um erro de `{Entity}Errors`, nunca uma exceção.

## 4. Objeto de valor com fábrica `Create`

`SharedKernel/ValueObject.cs`:

```csharp
namespace SharedKernel;

public abstract class ValueObject : IEquatable<ValueObject>
{
    public static bool operator ==(ValueObject? a, ValueObject? b) =>
        a is null ? b is null : a.Equals(b);

    public static bool operator !=(ValueObject? a, ValueObject? b) => !(a == b);

    public bool Equals(ValueObject? other) =>
        other is not null && GetType() == other.GetType() && GetAtomicValues().SequenceEqual(other.GetAtomicValues());

    public override bool Equals(object? obj) => obj is ValueObject other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hashCode = default;

        foreach (object value in GetAtomicValues())
        {
            hashCode.Add(value);
        }

        return hashCode.ToHashCode();
    }

    protected abstract IEnumerable<object> GetAtomicValues();
}
```

`SharedKernel/ResultExtensions.cs` — os passos que o `Create` de um objeto de valor encadeia:

```csharp
namespace SharedKernel;

public static class ResultExtensions
{
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Error error)
    {
        if (result.IsFailure)
        {
            return result;
        }

        return predicate(result.Value) ? result : Result.Failure<T>(error);
    }

    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> func) =>
        result.IsSuccess ? func(result.Value) : Result.Failure<TOut>(result.Error);
}
```

Acrescentado em `SharedKernel/Result.cs`:

```csharp
/// <summary>Sucesso com o valor, ou o erro quando o valor é nulo.</summary>
public static Result<TValue> Create<TValue>(TValue? value, Error error)
    where TValue : class =>
    value is null ? Failure<TValue>(error) : Success(value);

/// <summary>Devolve a primeira falha da lista, ou sucesso quando não há falha.</summary>
public static Result FirstFailureOrSuccess(params Result[] results)
{
    foreach (Result result in results)
    {
        if (result.IsFailure)
        {
            return result;
        }
    }

    return Success();
}
```

`Domain/Projects/ProjectName.cs`:

```csharp
public sealed class ProjectName : ValueObject
{
    public const int MaxLength = 100;

    private ProjectName(string value) => Value = value;

    public string Value { get; }

    public static implicit operator string(ProjectName name) => name.Value;

    /// <summary>Cria o nome, ou devolve o erro de nome vazio ou longo demais.</summary>
    public static Result<ProjectName> Create(string? name) =>
        Result.Create(name, ProjectErrors.NameIsEmpty)
            .Ensure(n => !string.IsNullOrWhiteSpace(n), ProjectErrors.NameIsEmpty)
            .Ensure(n => n.Length <= MaxLength, ProjectErrors.NameTooLong)
            .Map(n => new ProjectName(n));

    public override string ToString() => Value;

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return Value;
    }
}
```

- `MaxLength` é público: a configuração do EF o usa na coluna (`add-entity`).
- Com vários objetos de valor, o handler devolve a primeira falha:

```csharp
Result<ProjectName> nameResult = ProjectName.Create(command.Name);
Result<ProjectCode> codeResult = ProjectCode.Create(command.Code);

Result firstFailureOrSuccess = Result.FirstFailureOrSuccess(nameResult, codeResult);

if (firstFailureOrSuccess.IsFailure)
{
    return Result.Failure<Guid>(firstFailureOrSuccess.Error);
}
```

## 5. Interface declarada no Domain, passada como argumento

`Domain/Users/IPasswordHashChecker.cs`:

```csharp
namespace Domain.Users;

/// <summary>Confere uma senha contra o seu hash; implementado na Infrastructure.</summary>
public interface IPasswordHashChecker
{
    bool HashesMatch(string passwordHash, string providedPassword);
}
```

```csharp
/// <summary>Indica se a senha informada é a do usuário.</summary>
public bool VerifyPassword(string password, IPasswordHashChecker passwordHashChecker) =>
    !string.IsNullOrWhiteSpace(password) && passwordHashChecker.HashesMatch(PasswordHash, password);
```

- A implementação fica na Infrastructure; uma classe pode implementar as duas interfaces do mesmo trabalho: `internal sealed class PasswordHasher : IPasswordHasher, IPasswordHashChecker`.
- O handler recebe a interface e a passa ao método.

## 6. Serviço de domínio para uma regra entre agregados

```csharp
public sealed class TodoItemTransferService
{
    /// <summary>Transfere a tarefa, ou devolve o erro quando a origem ou o destino não permitem.</summary>
    public Result Transfer(TodoItem todoItem, Project from, Project to)
    {
        if (todoItem.ProjectId != from.Id)
        {
            return Result.Failure(TodoItemErrors.NotInProject(todoItem.Id, from.Id));
        }

        if (to.IsArchived)
        {
            return Result.Failure(ProjectErrors.Archived(to.Id));
        }

        return todoItem.MoveTo(to.Id);
    }
}
```

- Classe `sealed` de instância no Domain, criada pelo handler (`new TodoItemTransferService()`); devolve `Result` e nunca lança exceção para falha de negócio.

## 7. O agregado cria um registro filho; o handler o insere no mesmo save

```csharp
/// <summary>Cria o membro e dispara o evento; o handler o adiciona antes de salvar.</summary>
public ProjectMember AddMember(Guid userId)
{
    var member = ProjectMember.Create(Id, userId);

    Raise(new ProjectMemberAddedDomainEvent(Id, member.Id));

    return member;
}
```

```csharp
public sealed class ProjectMember : Entity
{
    private ProjectMember()
    {
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>Cria o membro; chamado só por <c>Project.AddMember</c>, por isso é interno.</summary>
    internal static ProjectMember Create(Guid projectId, Guid userId)
    {
        return new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = userId
        };
    }
}
```

O handler confere o que precisa de consulta, depois adiciona o filho e salva uma vez:

```csharp
if (await context.ProjectMembers
    .AnyAsync(m => m.ProjectId == project.Id && m.UserId == command.UserId, cancellationToken))
{
    return Result.Failure(ProjectErrors.MemberAlreadyAdded(command.UserId));
}

ProjectMember member = project.AddMember(command.UserId);

context.ProjectMembers.Add(member);

await context.SaveChangesAsync(cancellationToken);
```

- O mesmo formato vale para um registro de histórico ou de auditoria: o método do agregado o cria e o handler o adiciona antes do único `SaveChangesAsync`.
- Quando o método do agregado ainda pode falhar, ele devolve `Result<TChild>` e o handler devolve a falha antes de adicionar o filho.

## 8. `Result` para mudanças que podem falhar, `bool` para mudanças que podem não mudar nada

```csharp
/// <summary>Troca a descrição; devolve <c>false</c> quando ela já era a mesma.</summary>
public bool ChangeDescription(string description)
{
    if (description == Description)
    {
        return false;
    }

    string previousDescription = Description;

    Description = description;

    Raise(new TodoItemDescriptionChangedDomainEvent(Id, previousDescription));

    return true;
}
```

- `false` significa que nada mudou e nenhum evento foi disparado; não é uma falha.
- Um evento de alteração leva o id e o valor anterior.
- Um método nunca devolve as duas coisas: ou pode falhar (`Result`), ou pode não fazer nada (`bool`).
- Quando o handler já conferiu o estado antes da chamada (ele precisa, para devolver esse erro antes de uma consulta), o método é `void` e não repete a conferência:

```csharp
public void Archive()
{
    IsArchived = true;

    Raise(new ProjectArchivedDomainEvent(Id));
}
```

- O handler faz `if (project.IsArchived) return Result.Failure(ProjectErrors.AlreadyArchived(project.Id));`, roda as consultas e depois chama `project.Archive()`.

## 9. Regras que só leem o estado da entidade ficam na entidade

```csharp
public bool IsOverdue(DateTime utcNow) => !IsCompleted && DueDate < utcNow;
```

- Usada numa entidade já carregada: pelos métodos dela e pelos command handlers. Nenhuma classe estática responde perguntas sobre uma entidade.
- Uma consulta não pode chamá-la: o EF Core não traduz métodos da entidade para SQL. A consulta escreve o predicado nela mesma, ou usa um `Expression<Func<T, bool>>` na entidade quando várias consultas o compartilham (`TodoItem.OverdueAt(utcNow)`, `query-slice.md` da `add-feature`).

## 10. Eventos de domínio levam ids

```csharp
public sealed record TodoItemCompletedDomainEvent(Guid TodoItemId) : IDomainEvent;

public sealed record TodoItemDescriptionChangedDomainEvent(Guid TodoItemId, string PreviousDescription) : IDomainEvent;
```

- Um `sealed record`, um por arquivo, com o nome `{Entity}{PastTenseVerb}DomainEvent`, disparado só pelos métodos da própria entidade.
- Leva ids, nunca a entidade; um evento de alteração leva também o valor anterior.
- Sem `OccurredOnUtc`: um handler que precisa de hora usa o momento em que processa o evento (`IDateTimeProvider`).
