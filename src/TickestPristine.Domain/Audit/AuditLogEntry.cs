namespace TickestPristine.Domain.Audit;

public sealed class AuditLogEntry
{
    public Guid Id { get; set; }
    public string EntityName { get; set; }
    public string EntityId { get; set; }
    public string Action { get; set; } // Added, Modified ou Deleted (o EntityState do EF).
    public Guid? UserId { get; set; }
    public DateTime Timestamp { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? AffectedColumns { get; set; }
}
