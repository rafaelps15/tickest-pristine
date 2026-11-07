using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Domain.Audit;
using TickestPristine.SharedKernel;

namespace TickestPristine.Infrastructure.Audit;

/// <summary>
/// Grava um registro de auditoria para cada entidade <see cref="IAuditable"/> incluída, alterada ou excluída, no
/// mesmo SaveChanges (e, portanto, na mesma transação) da mudança, com um só instante por save. Os registros de um save
/// que falhou ficam pendentes no contexto; a nova tentativa os descarta e grava os dela.
/// </summary>
internal sealed class AuditLoggingInterceptor(IUserContext userContext, IDateTimeProvider dateTimeProvider)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddAuditEntries(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddAuditEntries(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditEntries(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DetachPendingAuditEntries(context);

        DateTime timestamp = dateTimeProvider.UtcNow;

        AuditLogEntry[] auditEntries = [.. context.ChangeTracker
            .Entries<IAuditable>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                EntityName = entry.Entity.GetType().Name,
                EntityId = entry.Property("Id").CurrentValue!.ToString()!,
                Action = entry.State.ToString(),
                UserId = userContext.UserIdOrDefault,
                Timestamp = timestamp,
                OldValues = SerializeOldValues(entry),
                NewValues = SerializeNewValues(entry),
                AffectedColumns = GetModifiedColumns(entry)
            })];

        context.Set<AuditLogEntry>().AddRange(auditEntries);
    }

    // Só este interceptor inclui registros de auditoria: os que ainda estão como incluídos são de um save que falhou.
    private static void DetachPendingAuditEntries(DbContext context)
    {
        foreach (EntityEntry<AuditLogEntry> pending in context.ChangeTracker
            .Entries<AuditLogEntry>()
            .Where(entry => entry.State == EntityState.Added)
            .ToList())
        {
            pending.State = EntityState.Detached;
        }
    }

    private static string? SerializeOldValues(EntityEntry entry) => entry.State switch
    {
        EntityState.Modified => Serialize(ModifiedProperties(entry), property => property.OriginalValue),
        EntityState.Deleted => Serialize(entry.Properties, property => property.CurrentValue),
        _ => null
    };

    private static string? SerializeNewValues(EntityEntry entry) => entry.State switch
    {
        EntityState.Added => Serialize(entry.Properties, property => property.CurrentValue),
        EntityState.Modified => Serialize(ModifiedProperties(entry), property => property.CurrentValue),
        _ => null
    };

    // O nome da coluna alterada entra mesmo quando o valor dela fica fora do log.
    private static string? GetModifiedColumns(EntityEntry entry) =>
        entry.State == EntityState.Modified
            ? string.Join(",", ModifiedProperties(entry).Select(property => property.Metadata.Name))
            : null;

    private static IEnumerable<PropertyEntry> ModifiedProperties(EntityEntry entry) =>
        entry.Properties.Where(property => property.IsModified);

    private static string Serialize(IEnumerable<PropertyEntry> properties, Func<PropertyEntry, object?> value) =>
        JsonSerializer.Serialize(properties
            .Where(property => property.Metadata.IsAudited())
            .ToDictionary(property => property.Metadata.Name, value));
}
