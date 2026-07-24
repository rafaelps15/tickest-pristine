namespace TickestPristine.Application.TicketAttachments.GetByTicket;

public sealed class TicketAttachmentResponse
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAtUtc { get; set; }
}
