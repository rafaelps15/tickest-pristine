namespace TickestPristine.Application.TicketAttachments.GetPolicy;

public sealed class TicketAttachmentPolicyResponse
{
    public long MaxFileSizeBytes { get; set; }
    public int MaxAttachmentsPerTicket { get; set; }
    public long MaxTotalBytesPerTicket { get; set; }
    public List<AllowedAttachmentTypeResponse> AllowedTypes { get; set; } = [];
}

public sealed class AllowedAttachmentTypeResponse
{
    public string ContentType { get; set; }
    public List<string> Extensions { get; set; } = [];
}
