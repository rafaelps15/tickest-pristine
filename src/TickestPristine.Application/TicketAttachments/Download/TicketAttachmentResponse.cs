namespace TickestPristine.Application.TicketAttachments.Download;

public sealed class TicketAttachmentResponse
{
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public string StorageKey { get; set; }
    public Stream Content { get; set; }
}
