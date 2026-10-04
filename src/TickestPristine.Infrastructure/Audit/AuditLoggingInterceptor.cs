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
internal sealed class AuditLoggingInterceptor : SaveChangesInterceptor
{
    // Valores que nunca vão para o log de auditoria; o nome da coluna alterada continua em AffectedColumns.
    private static readonly HashSet<string> ExcludedProperties =
        [nameof(Domain.Users.UserCredential.PasswordHash), nameof(Domain.Users.RefreshToken.TokenHash)];

    private readonly IUserContext _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AuditLoggingInterceptor(IUserContext currentUser, IDateTimeProvider dateTimeProvider)
    {
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        DbContext? context = eventData.Context;

        if (context is null)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        List<AuditLogEntry> auditEntries = CreateAuditEntries(context);

        context.Set<AuditLogEntry>().AddRange(auditEntries);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // O seed roda pelo caminho síncrono (UseSeeding), então ele também é auditado.
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        DbContext? context = eventData.Context;

        if (context is null)
        {
            return base.SavingChanges(eventData, result);
        }

        List<AuditLogEntry> auditEntries = CreateAuditEntries(context);

        context.Set<AuditLogEntry>().AddRange(auditEntries);

        return base.SavingChanges(eventData, result);
    }

    private List<AuditLogEntry> CreateAuditEntries(DbContext context)
    {
        var entries = new List<AuditLogEntry>();

        context.ChangeTracker.DetectChanges();

        foreach (EntityEntry<IAuditable> entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }

            var auditEntry = new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                EntityName = entry.Entity.GetType().Name,
                EntityId = GetPrimaryKey(entry),
                UserId = _currentUser.UserIdOrDefault,
                Timestamp = _dateTimeProvider.UtcNow,
                Action = entry.State.ToString()
            };

            switch (entry.State)
            {
                case EntityState.Added:
                    auditEntry.NewValues = SerializeProperties(
                        entry.Properties);
                    break;

                case EntityState.Modified:
                    auditEntry.OldValues = SerializeOldValues(entry);
                    auditEntry.NewValues = SerializeNewValues(entry);
                    auditEntry.AffectedColumns = GetModifiedColumns(entry);
                    break;

                case EntityState.Deleted:
                    auditEntry.OldValues = SerializeProperties(
                        entry.Properties);
                    break;
            }

            entries.Add(auditEntry);
        }

        return entries;
    }

    private static string GetPrimaryKey(EntityEntry entry)
    {
        // Handles composite keys by joining all key parts
        IEnumerable<string> keyParts = entry.Properties
            .Where(p => p.Metadata.IsPrimaryKey())
            .Select(p => p.CurrentValue?.ToString() ?? "null");

        return string.Join(",", keyParts);
    }

    private static string SerializeProperties(
        IEnumerable<PropertyEntry> properties)
    {
        var dict = properties
            .Where(p => !ExcludedProperties.Contains(p.Metadata.Name))
            .ToDictionary(
                p => p.Metadata.Name,
                p => p.CurrentValue);

        return JsonSerializer.Serialize(dict);
    }

    private static string SerializeOldValues(EntityEntry entry)
    {
        var dict = entry.Properties
            .Where(p => p.IsModified && !ExcludedProperties.Contains(p.Metadata.Name))
            .ToDictionary(
                p => p.Metadata.Name,
                p => p.OriginalValue);

        return JsonSerializer.Serialize(dict);
    }

    private static string SerializeNewValues(EntityEntry entry)
    {
        var dict = entry.Properties
            .Where(p => p.IsModified && !ExcludedProperties.Contains(p.Metadata.Name))
            .ToDictionary(
                p => p.Metadata.Name,
                p => p.CurrentValue);

        return JsonSerializer.Serialize(dict);
    }

    private static string GetModifiedColumns(EntityEntry entry)
    {
        IEnumerable<string> columns = entry.Properties
            .Where(p => p.IsModified)
            .Select(p => p.Metadata.Name);

        return string.Join(",", columns);
    }
}
