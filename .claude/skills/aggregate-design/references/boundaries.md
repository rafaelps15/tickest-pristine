# Modelos de fronteira do agregado

A raiz e os filhos internos vão em `src/Domain/{Feature}/`; a configuração, em `src/Infrastructure/{Feature}/{Entity}Configuration.cs`.

## Onde cada dado fica

| O dado | Onde fica |
|---|---|
| Só existe com a raiz, e as invariantes o leem | Filho interno: coleção num campo privado, criada pela raiz e carregada com ela |
| Nasce com uma mudança, mas as invariantes nunca o leem (histórico, auditoria, uma lista sem limite) | Registro filho criado pela raiz e inserido pelo handler (`domain-modeling`, padrão 7) |
| Tem vida própria, ou é compartilhado | Outro agregado, referenciado por id |
| Só serve para responder uma regra (as permissões de quem chama, uma contagem sobre outros agregados) | Lido pelo handler e passado ao método |

- A fronteira segue a invariante: o que uma regra precisa ver de uma vez é carregado junto e gravado num único `SaveChangesAsync`.
- O agregado pequeno carrega rápido e entra pouco em conflito; um filho só vai para dentro quando uma invariante o lê.

## Raiz com coleção interna

```csharp
public sealed class Project : Entity
{
    private readonly List<ProjectMaintainer> _maintainers = [];

    private Project()
    {
    }

    public Guid Id { get; private set; }

    public Guid OwnerId { get; private set; }

    public int Version { get; private set; }

    public IReadOnlyCollection<ProjectMaintainer> Maintainers => _maintainers;
}
```

- O `OwnerId` referencia o agregado `User` pelo id; não há navegação para o `User` na raiz.
- `Maintainers` expõe a lista privada só para leitura, como na `add-entity` (rich-entity.md): só os métodos da raiz incluem ou removem itens.
- `Version` é o token de concorrência que todo método que muda o agregado incrementa ([concurrency.md](concurrency.md)).

## Configuração

```csharp
builder.HasMany(p => p.Maintainers)
    .WithOne()
    .HasForeignKey(m => m.ProjectId);

builder.Navigation(p => p.Maintainers)
    .HasField("_maintainers")
    .UsePropertyAccessMode(PropertyAccessMode.Field);

builder.Property(p => p.Version).IsConcurrencyToken();
```

- O EF Core lê e grava o campo privado, sem setter público (`add-entity`, persistence.md).

## Carga no command

```csharp
Project? project = await context.Projects
    .Include(p => p.Maintainers)
    .SingleOrDefaultAsync(p => p.Id == command.ProjectId, cancellationToken);
```

- O command carrega a raiz com os filhos que as invariantes leem; as queries leem com o próprio SQL e nunca carregam o agregado.
