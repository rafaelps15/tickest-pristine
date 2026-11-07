# Modelos de regra de negócio

O `IBusinessRule` vai em `src/SharedKernel/`, o `CheckRule` em `src/SharedKernel/Entity.cs`, e cada regra em `src/Domain/{Feature}/Rules/{Name}Rule.cs`, ao lado do agregado que a confere.

## Contrato e conferência

```csharp
namespace SharedKernel;

/// <summary>
/// Regra de negócio com nome próprio, conferida pelo método do agregado antes da primeira mudança.
/// </summary>
public interface IBusinessRule
{
    bool IsBroken();

    Error Error { get; }
}
```

Na `Entity`:

```csharp
/// <summary>
/// Falha com o erro da regra quando ela está quebrada; nunca lança exceção.
/// </summary>
protected static Result CheckRule(IBusinessRule rule) =>
    rule.IsBroken() ? Result.Failure(rule.Error) : Result.Success();
```

- Os dois chegam com a primeira classe de regra do projeto, como as outras peças do domínio rico no `SharedKernel`.
- O `CheckRule` devolve `Result`: regra quebrada é falha esperada, devolvida como qualquer outra; nada lança exceção.
- Ele é `static` porque só lê a regra.

## Regras

Uma regra que recebe dado de fora — a resposta que o handler obteve do provedor de permissões:

```csharp
namespace Domain.Projects.Rules;

internal sealed class OnlyProjectManagerCanRemoveMaintainersRule(bool callerManagesProjects) : IBusinessRule
{
    public bool IsBroken() => !callerManagesProjects;

    public Error Error => ProjectErrors.CannotRemoveMaintainers;
}
```

Uma regra sobre os próprios filhos do agregado — o número de mantenedores que resta depois da mudança:

```csharp
namespace Domain.Projects.Rules;

internal sealed class ProjectMustHaveAtLeastOneMaintainerRule(int remainingMaintainers) : IBusinessRule
{
    public bool IsBroken() => remainingMaintainers == 0;

    public Error Error => ProjectErrors.LastMaintainer;
}
```

- Uma classe por invariante, com o nome dela (referência de nomes no `SKILL.md`); o erro vem do `{Entity}Errors`.
- O construtor recebe o que a regra lê; a regra nunca consulta nada e nunca muda estado.
- `internal sealed`: só os agregados do mesmo assembly a criam.

## O método do agregado

```csharp
/// <summary>
/// Tira o mantenedor do projeto; falha sem mudar nada se ele não for mantenedor, se quem pede não gerencia projetos ou se ele for o último.
/// </summary>
public Result RemoveMaintainer(Guid userId, bool callerManagesProjects)
{
    ProjectMaintainer? maintainer = _maintainers.SingleOrDefault(m => m.UserId == userId);

    if (maintainer is null)
    {
        return Result.Failure(ProjectErrors.MaintainerNotFound(userId));
    }

    Result rules = Result.FirstFailureOrSuccess(
        CheckRule(new OnlyProjectManagerCanRemoveMaintainersRule(callerManagesProjects)),
        CheckRule(new ProjectMustHaveAtLeastOneMaintainerRule(_maintainers.Count - 1)));

    if (rules.IsFailure)
    {
        return rules;
    }

    _maintainers.Remove(maintainer);
    Version++;

    Raise(new ProjectMaintainerRemovedDomainEvent(Id, userId));

    return Result.Success();
}
```

- A conferência simples do próprio estado (`MaintainerNotFound`) continua um `if`; as duas regras com nome são classes.
- Toda regra é conferida antes da primeira mudança: em falha, os mantenedores, a versão e os eventos ficam como estavam.
- O `Version++` faz o save atualizar a raiz, e o token de concorrência dela é conferido ([concurrency.md](concurrency.md)).

O handler carrega o agregado com os filhos, pergunta ao provedor de permissões e repassa a resposta:

```csharp
Project? project = await context.Projects
    .Include(p => p.Maintainers)
    .SingleOrDefaultAsync(p => p.Id == command.ProjectId, cancellationToken);

if (project is null)
{
    return Result.Failure(ProjectErrors.NotFound(command.ProjectId));
}

bool callerManagesProjects = await permissionProvider.HasPermissionAsync(
    userContext.UserId, Permissions.ProjectsManage, cancellationToken);

Result result = project.RemoveMaintainer(command.UserId, callerManagesProjects);

if (result.IsFailure)
{
    return result;
}

await context.SaveChangesAsync(cancellationToken);
```

## Teste pelo método do agregado

```csharp
[Fact]
public void RemoveMaintainer_Should_ReturnFailureAndKeepState_WhenUserIsTheOnlyMaintainer()
{
    // Arrange
    Guid maintainerId = Guid.NewGuid();
    Project project = CreateProjectWithMaintainer(maintainerId);
    int eventsBefore = project.DomainEvents.Count;

    // Act
    Result result = project.RemoveMaintainer(maintainerId, callerManagesProjects: true);

    // Assert
    result.Error.ShouldBe(ProjectErrors.LastMaintainer);
    project.Maintainers.ShouldHaveSingleItem().UserId.ShouldBe(maintainerId);
    project.DomainEvents.Count.ShouldBe(eventsBefore);
}
```

- O teste quebra uma regra de cada vez pelo método e confere o `Error` exato, os filhos intactos e o mesmo número de eventos.
- O `CreateProjectWithMaintainer` monta o projeto pelo `Create` e pelo `AddMaintainer`, nunca atribuindo propriedades.
- A classe de regra não tem teste próprio: o comportamento dela é o do método.
