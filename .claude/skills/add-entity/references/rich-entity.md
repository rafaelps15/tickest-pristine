# Modelos de entidade rica

Os arquivos vão em `src/Domain/{Feature}/`, como nos passos da `add-entity`. Troque `{Feature}` e `{Entity}` em todo o texto.

## Entidade

```csharp
using SharedKernel;

namespace Domain.Todos;

public sealed class TodoItem : Entity
{
    private readonly List<string> _labels = [];

    private TodoItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Description { get; private set; }

    public DateTime? DueDate { get; private set; }

    public IReadOnlyCollection<string> Labels => _labels;

    public bool IsCompleted { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public Priority Priority { get; private set; }

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

    public bool IsOverdue(DateTime utcNow) => !IsCompleted && DueDate < utcNow;
}
```

- `sealed`, herda `Entity`. Um único construtor privado sem parâmetros, usado pelo EF Core e pelo `Create`; o `Create` monta a entidade com inicializador de objeto, o único lugar em que ele aparece (os métodos de comportamento alteram a instância que já existe).
- `private set` em toda propriedade. A coleção fica num campo privado e é exposta como `IReadOnlyCollection<T>`; o EF Core a mapeia pelo campo (`persistence.md`), e só os métodos da entidade a alteram.
- `Create` devolve `Result<TodoItem>` porque uma regra de negócio pode recusar a criação; quando a criação não pode falhar, `Create` devolve a própria entidade (`Project.Create`, padrão 2 da `domain-modeling`). Nos dois casos, dispara o evento `Created`.
- Os métodos de comportamento devolvem `Result` e disparam os próprios eventos. A hora atual é parâmetro.
- Um `<summary>` curto em português só onde explica algo: em toda interface e nos métodos cujo comportamento não é óbvio pelo nome e pela assinatura (o que acontece em falha, quando devolve `false`, efeitos colaterais). Nada em propriedades, construtores, DTOs, endpoints, testes e métodos simples; nunca vazio.

## Filho criado pelo agregado

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

- `internal static Create`: só `Project.AddMember(userId)` o cria (padrão 7 da `domain-modeling`).

## Configuração do objeto de valor

```csharp
builder.OwnsOne(p => p.Name, nameBuilder =>
{
    nameBuilder.WithOwner();

    nameBuilder.Property(name => name.Value)
        .HasColumnName(nameof(Project.Name))
        .HasMaxLength(ProjectName.MaxLength)
        .IsRequired();
});
```

- A coluna usa o nome do objeto de valor e o `MaxLength` dele; nenhum número se repete na configuração.

