using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed class TicketAttachment : Entity
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public string StorageKey { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}
