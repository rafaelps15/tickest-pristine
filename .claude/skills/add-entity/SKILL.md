---
name: add-entity
description: Adiciona uma nova entidade de domínio ao template de Clean Architecture — classe da entidade, catálogo de erros, eventos de domínio, configuração do EF Core, ligação no DbContext e migration. Use quando o usuário pedir para adicionar uma entidade, agregado, modelo de domínio ou tabela.
argument-hint: <descrição da entidade, por exemplo "Project com nome, dono e lista de tarefas">
---

# Adicionar uma entidade de domínio

Crie uma entidade nova e ligue-a em todas as camadas, seguindo o padrão do `TodoItem`.

## Arquivos a criar ou alterar

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

`sealed class`, herda de `Entity` (que dá `DomainEvents` + `Raise(...)`), `Guid Id`, propriedades simples com setter e coleções inicializadas com `= [];`.

2. **Catálogo de erros** — `src/Domain/{Feature}/{Entity}Errors.cs`

```csharp
using SharedKernel;

namespace Domain.Projects;

public static class ProjectErrors
{
    public static Error NotFound(Guid projectId) => Error.NotFound(
        "Projects.NotFound",
        $"O projeto com o Id = '{projectId}' não foi encontrado");
}
```

Os códigos seguem `"{FeaturePlural}.{Reason}"` (em inglês, estáveis); as descrições são mostradas ao usuário e ficam em português do Brasil. Escolha o método pelo significado: `Error.NotFound` (404), `Error.Conflict` (409), `Error.Forbidden` (403), `Error.Problem` (400), `Error.Failure` (500).

3. **Eventos de domínio** — um record por arquivo, `src/Domain/{Feature}/{Entity}{PastTenseVerb}DomainEvent.cs`

```csharp
using SharedKernel;

namespace Domain.Projects;

public sealed record ProjectCreatedDomainEvent(Guid ProjectId) : IDomainEvent;
```

Crie pelo menos o evento `Created`; adicione os outros conforme os commands precisarem. Eventos carregam ids, não entidades.

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

Os relacionamentos são configurados sem propriedade de navegação (`HasOne<User>().WithMany()`) — as entidades guardam os ids das chaves estrangeiras, não referências a outras entidades. As configurações são carregadas automaticamente pelo `ApplyConfigurationsFromAssembly`.

5. **Ligação no DbContext** — adicione `DbSet<{Entity}> {Plural}` em **ambos**:
   - `src/Application/Abstractions/Data/IApplicationDbContext.cs`
   - `src/Infrastructure/Database/ApplicationDbContext.cs`

   Adicione o `DbSet` também em `tests/Application.UnitTests/Abstractions/TestDbContext.cs`, para os handlers continuarem testáveis.

6. **Migration** — a partir da raiz do repositório:

```
dotnet ef migrations add Add_{Plural} --project src/Infrastructure --startup-project src/Web.Api --output-dir Database/Migrations
```

Os nomes de migration seguem `PascalCase_With_Underscores` (ex.: `Add_RefreshTokens`). Revise a migration gerada antes de aplicar: o EF avisa quando uma operação pode perder dados, e colunas obrigatórias novas com índice único em tabelas que já têm linhas precisam de tratamento (preencher ou limpar os dados antes).

## Regras

- O projeto Domain referencia só o `SharedKernel` — nada de EF nem de tipos da Application. Detalhes de persistência (chaves, conversões, relacionamentos) ficam exclusivamente na configuração da Infrastructure.
- Rode `dotnet build` e `dotnet test` ao terminar — o `ArchitectureTests` garante as regras entre camadas.
- Se o usuário também quiser casos de uso para a entidade, continue com a skill `add-feature`.
