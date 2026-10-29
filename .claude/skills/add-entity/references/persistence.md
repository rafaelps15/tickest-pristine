# Modelos de persistência

Os arquivos vão em `src/Infrastructure/Database/` e `src/SharedKernel/`. Aplique-os na primeira vez que uma entidade precisar deles; depois, cada entidade só implementa as interfaces.

## Coleção num campo privado

A entidade guarda a coleção num campo privado e a expõe como `IReadOnlyCollection<T>` (`rich-entity.md`). A configuração mapeia o campo, e o EF Core o lê e grava sem setter público:

```csharp
builder.PrimitiveCollection(t => t.Labels)
    .HasField("_labels")
    .UsePropertyAccessMode(PropertyAccessMode.Field);
```

- Só os métodos da entidade incluem ou removem itens; nenhum handler alcança a lista.

## Conversor UTC

`src/Infrastructure/Database/ModelBuilderExtensions.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Database;

internal static class ModelBuilderExtensions
{
    private static readonly ValueConverter<DateTime, DateTime> UtcValueConverter =
        new(outside => outside, inside => DateTime.SpecifyKind(inside, DateTimeKind.Utc));

    /// <summary>Marca como UTC, na leitura, toda propriedade <c>DateTime</c> terminada em <c>Utc</c>.</summary>
    internal static void ApplyUtcDateTimeConverter(this ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType mutableEntityType in modelBuilder.Model.GetEntityTypes())
        {
            IEnumerable<IMutableProperty> dateTimeUtcProperties = mutableEntityType.GetProperties()
                .Where(p => p.ClrType == typeof(DateTime) && p.Name.EndsWith("Utc", StringComparison.Ordinal));

            foreach (IMutableProperty mutableProperty in dateTimeUtcProperties)
            {
                mutableProperty.SetValueConverter(UtcValueConverter);
            }
        }
    }
}
```

- Só propriedades `DateTime` com nome terminado em `Utc`. O nome é o contrato: uma data sem o sufixo não é convertida.
- Uma coluna `DateTime?` é convertida na própria configuração: `HasConversion(d => d != null ? DateTime.SpecifyKind(d.Value, DateTimeKind.Utc) : d, v => v)` (como no `TodoItemConfiguration`).
- Chamado uma vez em `ApplicationDbContext.OnModelCreating`, depois do `ApplyConfigurationsFromAssembly`.

## Auditoria e exclusão lógica

`src/SharedKernel/IAuditableEntity.cs` e `src/SharedKernel/ISoftDeletableEntity.cs`:

```csharp
namespace SharedKernel;

/// <summary>Entidade com datas de criação e alteração preenchidas pelo contexto.</summary>
public interface IAuditableEntity
{
    DateTime CreatedOnUtc { get; }

    DateTime? ModifiedOnUtc { get; }
}

/// <summary>Entidade excluída logicamente pelo contexto.</summary>
public interface ISoftDeletableEntity
{
    DateTime? DeletedOnUtc { get; }

    bool Deleted { get; }
}
```

A entidade expõe as datas como propriedades só de leitura:

```csharp
public sealed class Project : Entity, IAuditableEntity, ISoftDeletableEntity
{
    // ...
    public DateTime CreatedOnUtc { get; }

    public DateTime? ModifiedOnUtc { get; }

    public DateTime? DeletedOnUtc { get; }

    public bool Deleted { get; }
}
```

O `ApplicationDbContext.SaveChangesAsync` as preenche antes de salvar e continua despachando os eventos de domínio depois:

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    DateTime utcNow = dateTimeProvider.UtcNow;

    UpdateAuditableEntities(utcNow);
    UpdateSoftDeletableEntities(utcNow);

    List<IDomainEvent> domainEvents = ExtractDomainEvents();
    int result = await base.SaveChangesAsync(cancellationToken);

    await PublishDomainEventsAsync(domainEvents);

    return result;
}

private void UpdateAuditableEntities(DateTime utcNow)
{
    foreach (EntityEntry<IAuditableEntity> entityEntry in ChangeTracker.Entries<IAuditableEntity>())
    {
        if (entityEntry.State == EntityState.Added)
        {
            entityEntry.Property(nameof(IAuditableEntity.CreatedOnUtc)).CurrentValue = utcNow;
        }

        if (entityEntry.State == EntityState.Modified)
        {
            entityEntry.Property(nameof(IAuditableEntity.ModifiedOnUtc)).CurrentValue = utcNow;
        }
    }
}

private void UpdateSoftDeletableEntities(DateTime utcNow)
{
    foreach (EntityEntry<ISoftDeletableEntity> entityEntry in ChangeTracker.Entries<ISoftDeletableEntity>())
    {
        if (entityEntry.State != EntityState.Deleted)
        {
            continue;
        }

        entityEntry.Property(nameof(ISoftDeletableEntity.DeletedOnUtc)).CurrentValue = utcNow;
        entityEntry.Property(nameof(ISoftDeletableEntity.Deleted)).CurrentValue = true;
        entityEntry.State = EntityState.Modified;

        UpdateDeletedEntityEntryReferencesToUnchanged(entityEntry);
    }
}

private static void UpdateDeletedEntityEntryReferencesToUnchanged(EntityEntry entityEntry)
{
    if (!entityEntry.References.Any())
    {
        return;
    }

    foreach (ReferenceEntry referenceEntry in entityEntry.References.Where(r => r.TargetEntry.State == EntityState.Deleted))
    {
        referenceEntry.TargetEntry.State = EntityState.Unchanged;

        UpdateDeletedEntityEntryReferencesToUnchanged(referenceEntry.TargetEntry);
    }
}
```

- O `IDateTimeProvider` entra no construtor primário do contexto (`ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IDomainEventsDispatcher domainEventsDispatcher, IDateTimeProvider dateTimeProvider)`).
- O handler exclui com `context.Projects.Remove(project)`; o contexto transforma isso em exclusão lógica. Nenhum handler atribui `CreatedOnUtc`, `ModifiedOnUtc` ou `DeletedOnUtc`.

## Configuração

Cada entidade com exclusão lógica filtra as linhas excluídas:

```csharp
builder.Property(p => p.CreatedOnUtc).IsRequired();
builder.Property(p => p.ModifiedOnUtc);
builder.Property(p => p.DeletedOnUtc);
builder.Property(p => p.Deleted).HasDefaultValue(false);

builder.HasQueryFilter(p => !p.Deleted);
```

- Um projeto que já tem essas colunas com outros nomes mantém os nomes dele nas interfaces, para que nenhuma coluna seja renomeada pela refatoração.
