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
    // Valores que nunca vão para o log de auditoria; o nome da coluna alterada continua em AffectedColumns.
    private static readonly HashSet<string> ExcludedProperties = [nameof(Domain.Users.UserCredential.PasswordHash)];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddAuditEntries(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // O seed roda pelo caminho síncrono (UseSeeding), então ele também é auditado.
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddAuditEntries(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    private void AddAuditEntries(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();

        var auditEntries = context.ChangeTracker.Entries<IAuditable>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(CreateAuditEntry)
            .ToList();

        context.Set<AuditLogEntry>().AddRange(auditEntries);
    }

    private AuditLogEntry CreateAuditEntry(EntityEntry<IAuditable> entry)
    {
        var auditEntry = new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            EntityName = entry.Entity.GetType().Name,
            EntityId = GetPrimaryKey(entry),
            UserId = currentUser.UserIdOrDefault,
            Timestamp = dateTimeProvider.UtcNow,
            Action = entry.State.ToString()
        };

        switch (entry.State)
        {
            case EntityState.Added:
                auditEntry.NewValues = Serialize(entry.Properties, p => p.CurrentValue);
                break;

            case EntityState.Modified:
                PropertyEntry[] modified = [.. entry.Properties.Where(p => p.IsModified)];
                auditEntry.OldValues = Serialize(modified, p => p.OriginalValue);
                auditEntry.NewValues = Serialize(modified, p => p.CurrentValue);
                auditEntry.AffectedColumns = string.Join(",", modified.Select(p => p.Metadata.Name));
                break;

            case EntityState.Deleted:
                auditEntry.OldValues = Serialize(entry.Properties, p => p.CurrentValue);
                break;
        }

        return auditEntry;
    }

    // Chaves compostas viram as partes separadas por vírgula.
    private static string GetPrimaryKey(EntityEntry entry) =>
        string.Join(",", entry.Properties
            .Where(p => p.Metadata.IsPrimaryKey())
            .Select(p => p.CurrentValue?.ToString() ?? "null"));

    private static string Serialize(IEnumerable<PropertyEntry> properties, Func<PropertyEntry, object?> value) =>
        JsonSerializer.Serialize(properties
            .Where(p => !ExcludedProperties.Contains(p.Metadata.Name))
            .ToDictionary(p => p.Metadata.Name, value));
}
