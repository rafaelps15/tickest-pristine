---
name: add-entity
description: Adiciona uma nova entidade de domínio ao projeto — classe da entidade, catálogo de erros, eventos de domínio, configuração do EF Core, ligação no DbContext e migration. Use quando o usuário pedir para adicionar uma entidade, agregado, modelo de domínio ou tabela.
argument-hint: <descrição da entidade, ex.: "Project with a name, owner, and list of todos">
---

# Adicionar uma entidade de domínio

> **Contexto do projeto primeiro.** Antes de agir, leia a seção "Contexto do projeto" do `CLAUDE.md` do
> repositório. Os caminhos, namespaces, nomes e exemplos desta skill vêm do modelo de referência (Todos) e
> são ilustrativos. Quando o contexto do projeto declarar caminhos, referências vivas ou desvios, eles
> prevalecem. Se essa seção não existir, inspecione o repositório, siga o slice real mais parecido e
> pergunte antes de assumir uma convenção.

Crie uma entidade nova e ligue-a em todas as camadas, seguindo o padrão de `TodoItem`.

## Arquivos a criar/alterar

1. **Entidade** — `src/Domain/{Feature}/{Entity}.cs`

```csharp
using SharedKernel;

namespace Domain.Projects;

public sealed class Project : Entity
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

`sealed class`, herda `Entity` (que dá `DomainEvents` + `Raise(...)`), `Guid Id`, propriedades simples com setter e coleções inicializadas com `= [];`.

2. **Catálogo de erros** — `src/Domain/{Feature}/{Entity}Errors.cs`

```csharp
using SharedKernel;

namespace Domain.Projects;

public static class ProjectErrors
{
    public static Error NotFound(Guid projectId) => Error.NotFound(
        "Projects.NotFound",
        $"The project with the Id = '{projectId}' was not found");
}
```

Os códigos seguem `"{FeaturePlural}.{Reason}"`. Escolha a fábrica pelo significado: `Error.NotFound` (404), `Error.Conflict` (409), `Error.Problem` (400), `Error.Failure` (500).

3. **Eventos de domínio** — um record por arquivo, `src/Domain/{Feature}/{Entity}{PastTenseVerb}DomainEvent.cs`

```csharp
using SharedKernel;

namespace Domain.Projects;

public sealed record ProjectCreatedDomainEvent(Guid ProjectId) : IDomainEvent;
```

Crie no mínimo o evento `Created`; acrescente outros conforme os commands precisarem. Eventos carregam ids, não entidades.

4. **Configuração do EF** — `src/Infrastructure/{Feature}/{Entity}Configuration.cs`

```csharp
using Domain.Projects;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Projects;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(p => p.Id);

        builder.HasOne<User>().WithMany().HasForeignKey(p => p.OwnerId);
    }
}
```

Os relacionamentos são configurados sem navegação (`HasOne<User>().WithMany()`) — as entidades guardam ids de chave estrangeira, não propriedades de navegação. As configurações são carregadas automaticamente pelo `ApplyConfigurationsFromAssembly`.

5. **Ligação no DbContext** — adicione `DbSet<{Entity}> {Plural}` em **ambos**:
   - `src/Application/Abstractions/Data/IApplicationDbContext.cs`
   - `src/Infrastructure/Database/ApplicationDbContext.cs`

   Adicione também o `DbSet` em `tests/Application.UnitTests/Abstractions/TestDbContext.cs`, para que os handlers continuem testáveis por teste unitário.

6. **Migration** — a partir da raiz do repositório:

```
dotnet ef migrations add Add_{Plural} --project src/Infrastructure --startup-project src/Web.Api
```

Os nomes de migration seguem `PascalCase_With_Underscores` (veja `Add_RefreshTokens`).

## Regras

- O projeto Domain referencia só o `SharedKernel` — nada de EF nem de tipos da Application. Detalhes de persistência (chaves, conversões, relacionamentos) ficam só na configuração da Infrastructure.
- Rode `dotnet build` e `dotnet test` ao terminar — os `ArchitectureTests` garantem as regras de camadas.
- Se o usuário também quiser casos de uso para a entidade, continue com a skill `add-feature`.
