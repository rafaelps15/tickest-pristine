namespace TickestPristine.Application.AuditLogs.GetHistory;

public sealed class AuditLogResponse
{
    public Guid Id { get; set; }
    public string EntityName { get; set; }
    public string EntityId { get; set; }
    public string Action { get; set; }
    public Guid? UserId { get; set; }
    public DateTime Timestamp { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? AffectedColumns { get; set; }
}
