---
name: add-entity
description: Adiciona uma nova entidade de domínio que tem as próprias regras — classe rica da entidade, catálogo de erros, eventos de domínio, configuração do EF Core, ligação no DbContext e migration. Use quando o usuário pedir para adicionar uma entidade, agregado, modelo de domínio ou tabela.
argument-hint: '<descrição da entidade, ex.: "Project with a name, owner, and list of todos">'
---

# Adicionar uma entidade de domínio

> **Contexto do projeto primeiro.** Antes de agir, leia a seção "Contexto do projeto" do `CLAUDE.md` do
> repositório. Os caminhos, namespaces, nomes e exemplos desta skill vêm do modelo de referência (Todos) e
> são ilustrativos. Quando o contexto do projeto declarar caminhos, referências vivas ou desvios, eles
> prevalecem. Se essa seção não existir, inspecione o repositório, siga o slice real mais parecido e
> pergunte antes de assumir uma convenção.

> **Portão de especificação.** Se o `CLAUDE.md` do repositório adotar um fluxo de especificação antes do
> código (por exemplo, Spec-Driven Development), esta skill só escreve código de produção ou testes dentro
> desse fluxo: chamada pela skill de implementação para uma tarefa de spec aprovada, ou numa mudança
> trivial autorizada explicitamente pelo responsável no chat (quando o `CLAUDE.md` declarar um marcador de
> autorização, como `[sdd:autorizo]`, ele precisa estar na mensagem do responsável). Num pedido direto fora desse fluxo, não
> escreva código: procure em `specs/` uma spec que cubra o pedido e indique o comando de implementação
> dela; se não houver, use a skill de especificação.

Crie uma entidade que guarda o próprio estado e as próprias regras, e ligue-a em todas as camadas. Modelos completos: [references/rich-entity.md](references/rich-entity.md) (entidade, filho, mapeamento do objeto de valor) e [references/persistence.md](references/persistence.md) (coleções, datas UTC, auditoria). Os padrões de domínio estão na `domain-modeling`.

## Arquivos a criar/alterar

1. **Entidade** — `src/Domain/{Feature}/{Entity}.cs`

```csharp
public sealed class Project : Entity
{
    // Exigido pelo EF Core.
    private Project() { }

    public Guid Id { get; private set; }

    public ProjectName Name { get; private set; }

    public static Project Create(Guid ownerId, ProjectName name) { /* ... */ }
}
```

- `sealed`, herda `Entity` (que dá `DomainEvents` + `Raise(...)`), `Guid Id`. Um construtor privado protege os argumentos (`Ensure`); outro, privado e sem parâmetros, existe só para o EF Core.
- `private set` em toda propriedade. Uma coleção fica num campo privado e é exposta como `IReadOnlyCollection<T>`.
- Um `Create` simples quando a criação não pode falhar; `Result<T> Create` quando uma regra de negócio pode recusá-la. Os dois disparam o evento `Created`.
- Os métodos de comportamento mudam o estado, disparam o próprio evento e devolvem `Result` (pode falhar) ou `bool` (pode não mudar nada). A hora atual é parâmetro. Um `<summary>` em português só onde o comportamento não é óbvio.

2. **Catálogo de erros** — `src/Domain/{Feature}/{Entity}Errors.cs`

```csharp
public static class ProjectErrors
{
    public static Error NotFound(Guid projectId) => Error.NotFound(
        "Projects.NotFound",
        $"O projeto com o Id = '{projectId}' não foi encontrado.");
}
```

Os códigos seguem `"{FeaturePlural}.{Reason}"`. Escolha a fábrica pelo significado: `Error.NotFound` (404), `Error.Conflict` (409), `Error.Problem` (400), `Error.Failure` (500).

3. **Eventos de domínio** — um record por arquivo, `src/Domain/{Feature}/{Entity}{PastTenseVerb}DomainEvent.cs`

```csharp
public sealed record ProjectCreatedDomainEvent(Guid ProjectId) : IDomainEvent;
```

Crie pelo menos o evento `Created`; os métodos da entidade disparam os outros. Os eventos levam ids, nunca entidades; um evento de alteração leva também o valor anterior. Sem `OccurredOnUtc`.

4. **Configuração do EF** — `src/Infrastructure/{Feature}/{Entity}Configuration.cs`

```csharp
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

Os relacionamentos são configurados no estilo shadow (`HasOne<User>().WithMany()`): as entidades guardam ids de chave estrangeira, não propriedades de navegação. Objetos de valor usam `OwnsOne` com o `MaxLength` deles; uma coleção é mapeada pelo campo (`references/persistence.md`). As configurações são encontradas pelo `ApplyConfigurationsFromAssembly`.

5. **Ligação no DbContext** — adicione `DbSet<{Entity}> {Plural}` **nos dois**:
   - `src/Application/Abstractions/Data/IApplicationDbContext.cs`
   - `src/Infrastructure/Database/ApplicationDbContext.cs`
   Adicione também o `DbSet` em `tests/Application.UnitTests/Abstractions/TestDbContext.cs` para que os handlers continuem testáveis.

6. **Migration** — na raiz do repositório:

```
dotnet ef migrations add Add_{Plural} --project src/Infrastructure --startup-project src/Web.Api
```

Os nomes de migration seguem `PascalCase_With_Underscores` (veja `Add_RefreshTokens`).

## Regras

- O projeto Domain referencia só o `SharedKernel` — sem EF, sem tipos da Application. Os detalhes de persistência ficam na configuração da Infrastructure.
- Nenhum handler ou classe estática atribui as propriedades da entidade nem dispara os eventos dela; eles chamam os métodos da entidade.
- Rode `dotnet build` e `dotnet test` ao terminar — os `ArchitectureTests` garantem as regras de camada. Escreva os testes da entidade com a `add-tests`; siga com a `add-feature` para os casos de uso dela.
