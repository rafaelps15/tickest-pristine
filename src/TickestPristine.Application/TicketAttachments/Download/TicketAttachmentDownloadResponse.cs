namespace TickestPristine.Application.TicketAttachments.Download;

public sealed class TicketAttachmentDownloadResponse
{
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public Stream Content { get; set; }
}
