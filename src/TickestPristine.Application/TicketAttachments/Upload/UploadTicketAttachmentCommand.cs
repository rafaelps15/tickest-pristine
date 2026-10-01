using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.TicketAttachments.Upload;

public sealed class UploadTicketAttachmentCommand : ICommand<Guid>
{
    public Guid TicketId { get; set; }
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public Stream Content { get; set; }
}
