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
/// mesmo SaveChanges (e, portanto, na mesma transação) da mudança.
/// </summary>
internal sealed class AuditLoggingInterceptor(IUserContext currentUser, IDateTimeProvider dateTimeProvider)
    : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            AuditChanges(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AuditChanges(DbContext context)
    {
        context.ChangeTracker.DetectChanges();

        AuditLogEntry[] auditLogEntries = [.. context.ChangeTracker
            .Entries<IAuditable>()
            .Where(IsChanged)
            .Select(ToAuditLogEntry)];

        context.Set<AuditLogEntry>().AddRange(auditLogEntries);
    }

    private AuditLogEntry ToAuditLogEntry(EntityEntry<IAuditable> entry) => new()
    {
        Id = Guid.NewGuid(),
        EntityName = entry.Entity.GetType().Name,
        EntityId = PrimaryKeyOf(entry),
        Action = entry.State.ToString(),
        UserId = currentUser.UserIdOrDefault,
        Timestamp = dateTimeProvider.UtcNow,
        OldValues = OldValuesOf(entry),
        NewValues = NewValuesOf(entry),
        AffectedColumns = AffectedColumnsOf(entry)
    };

    private static bool IsChanged(EntityEntry entry) =>
        entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;

    private static string? OldValuesOf(EntityEntry entry) => entry.State switch
    {
        EntityState.Modified => ToJson(ModifiedProperties(entry), property => property.OriginalValue),
        EntityState.Deleted => ToJson(entry.Properties, property => property.CurrentValue),
        _ => null
    };

    private static string? NewValuesOf(EntityEntry entry) => entry.State switch
    {
        EntityState.Added => ToJson(entry.Properties, property => property.CurrentValue),
        EntityState.Modified => ToJson(ModifiedProperties(entry), property => property.CurrentValue),
        _ => null
    };

    // O nome da coluna alterada entra mesmo quando o valor dela fica fora do log.
    private static string? AffectedColumnsOf(EntityEntry entry) =>
        entry.State is EntityState.Modified
            ? string.Join(",", ModifiedProperties(entry).Select(property => property.Metadata.Name))
            : null;

    private static IEnumerable<PropertyEntry> ModifiedProperties(EntityEntry entry) =>
        entry.Properties.Where(property => property.IsModified);

    // Chaves compostas viram as partes separadas por vírgula.
    private static string PrimaryKeyOf(EntityEntry entry) =>
        string.Join(",", entry.Properties
            .Where(property => property.Metadata.IsPrimaryKey())
            .Select(property => property.CurrentValue?.ToString() ?? "null"));

    private static string ToJson(IEnumerable<PropertyEntry> properties, Func<PropertyEntry, object?> value) =>
        JsonSerializer.Serialize(properties
            .Where(property => property.Metadata.IsAudited())
            .ToDictionary(property => property.Metadata.Name, value));
}
