# Modelos de entidade rica

Os arquivos vão em `src/Domain/{Feature}/`, como nos passos da `add-entity`. Troque `{Feature}` e `{Entity}` em todo o texto.

## Entidade

```csharp
using SharedKernel;

namespace Domain.Todos;

public sealed class TodoItem : Entity
{
    private readonly List<string> _labels = [];

    private TodoItem(Guid userId, string description, Priority priority, DateTime? dueDate, IEnumerable<string> labels, DateTime createdAt)
    {
        Ensure.NotEmpty(userId, "O usuário é obrigatório.", nameof(userId));
        Ensure.NotEmpty(description, "A descrição é obrigatória.", nameof(description));
        Ensure.NotEmpty(createdAt, "A data de criação é obrigatória.", nameof(createdAt));

        Id = Guid.NewGuid();
        UserId = userId;
        Description = description;
        Priority = priority;
        DueDate = dueDate;
        CreatedAt = createdAt;
        _labels.AddRange(labels);
    }

    // Exigido pelo EF Core.
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

        var todoItem = new TodoItem(userId, description, priority, dueDate, labels, createdAt);

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

- `sealed`, herda `Entity`. O construtor privado protege os argumentos; o construtor privado sem parâmetros existe só para o EF Core.
- `private set` em toda propriedade. A coleção fica num campo privado e é exposta como `IReadOnlyCollection<T>`; o EF Core a mapeia pelo campo (`persistence.md`), e só os métodos da entidade a alteram.
- `Create` devolve `Result<TodoItem>` porque uma regra de negócio pode recusar a criação; quando a criação não pode falhar, `Create` devolve a própria entidade (`Project.Create`, padrão 2 da `domain-modeling`). Nos dois casos, dispara o evento `Created`.
- Os métodos de comportamento devolvem `Result` e disparam os próprios eventos. A hora atual é parâmetro.
- Um `<summary>` curto em português só onde explica algo: em toda interface e nos métodos cujo comportamento não é óbvio pelo nome e pela assinatura (o que acontece em falha, quando devolve `false`, efeitos colaterais). Nada em propriedades, construtores, DTOs, endpoints, testes e métodos simples; nunca vazio. O único comentário comum explica o construtor do EF Core.

## Filho criado pelo agregado

```csharp
public sealed class ProjectMember : Entity
{
    internal ProjectMember(Guid projectId, Guid userId)
    {
        Ensure.NotEmpty(projectId, "O projeto é obrigatório.", nameof(projectId));
        Ensure.NotEmpty(userId, "O usuário é obrigatório.", nameof(userId));

        Id = Guid.NewGuid();
        ProjectId = projectId;
        UserId = userId;
    }

    // Exigido pelo EF Core.
    private ProjectMember()
    {
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid UserId { get; private set; }
}
```

- Construtor `internal`: só `Project.AddMember(userId)` o cria (padrão 7 da `domain-modeling`).

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

