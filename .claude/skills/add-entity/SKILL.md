---
name: add-entity
description: Adiciona uma nova entidade de domínio ao template de Clean Architecture — classe da entidade, catálogo de erros, eventos de domínio, configuração do EF Core, ligação no DbContext e migration. Use quando o usuário pedir para adicionar uma entidade, agregado, modelo de domínio ou tabela.
argument-hint: <descrição da entidade, ex.: "TodoItem com descrição, dono e lista de etiquetas">
---

# Adicionar uma entidade de domínio

Crie uma entidade nova e ligue-a em todas as camadas, seguindo o padrão do `TodoItem`.

## Arquivos a criar/alterar

1. **Entidade** — `src/Domain/{Feature}/{Entity}.cs`

```csharp
using SharedKernel;

namespace Domain.Todos;

public sealed class TodoItem : Entity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

`sealed class`, herda de `Entity` (que dá a ela `DomainEvents` + `Raise(...)`), `Guid Id`, propriedades simples com setter e coleções inicializadas com `= [];`.

2. **Catálogo de erros** — `src/Domain/{Feature}/{Entity}Errors.cs`

```csharp
using SharedKernel;

namespace Domain.Todos;

public static class TodoItemErrors
{
    public static Error NotFound(Guid todoItemId) => Error.NotFound(
        "TodoItems.NotFound",
        $"A tarefa com o Id = '{todoItemId}' não foi encontrada.");
}
```

Os códigos seguem `"{FeaturePlural}.{Reason}"`. Escolha a fábrica pelo significado: `Error.NotFound` (404), `Error.Conflict` (409), `Error.Problem` (400), `Error.Failure` (500).

3. **Eventos de domínio** — um record por arquivo, `src/Domain/{Feature}/{Entity}{PastTenseVerb}DomainEvent.cs`

```csharp
using SharedKernel;

namespace Domain.Todos;

public sealed record TodoItemCreatedDomainEvent(Guid TodoItemId) : IDomainEvent;
```

Crie no mínimo o evento `Created`; acrescente outros conforme os commands precisarem. Os eventos carregam ids, não entidades.

4. **Configuração do EF** — `src/Infrastructure/{Feature}/{Entity}Configuration.cs`

```csharp
using Domain.Todos;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Todos;

internal sealed class TodoItemConfiguration : IEntityTypeConfiguration<TodoItem>
{
    public void Configure(EntityTypeBuilder<TodoItem> builder)
    {
        builder.HasKey(t => t.Id);

        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId);
    }
}
```

Os relacionamentos são configurados sem navegação (`HasOne<User>().WithMany()`) — as entidades guardam os ids das chaves estrangeiras, não propriedades de navegação. As configurações são carregadas automaticamente pelo `ApplyConfigurationsFromAssembly`.

5. **Ligação no DbContext** — adicione `DbSet<{Entity}> {Plural}` nos **dois**:
   - `src/Application/Abstractions/Data/IApplicationDbContext.cs`
   - `src/Infrastructure/Database/ApplicationDbContext.cs`

   Adicione também o `DbSet` em `tests/Application.UnitTests/Abstractions/TestDbContext.cs`, para que os handlers continuem testáveis por testes unitários.

6. **Migration** — a partir da raiz do repositório:

```
dotnet ef migrations add Add_{Plural} --project src/Infrastructure --startup-project src/Web.Api
```

Os nomes de migration seguem `PascalCase_With_Underscores` (veja `Add_RefreshTokens`).

## Regras

- O projeto Domain referencia só o `SharedKernel` — nada de EF, nada de tipos da Application. Detalhes de persistência (chaves, conversões, relacionamentos) ficam exclusivamente na configuração da Infrastructure.
- Rode `dotnet build` e `dotnet test` ao terminar — os `ArchitectureTests` garantem as regras entre camadas.
- Se o usuário também quiser casos de uso para a entidade, continue com a skill `add-feature`.
