using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed class TicketAttachment : Entity
{
    private TicketAttachment(
        Guid id,
        Guid ticketId,
        Guid uploadedByUserId,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string storageKey,
        DateTime uploadedAtUtc)
    {
        Id = id;
        TicketId = ticketId;
        UploadedByUserId = uploadedByUserId;
        FileName = fileName;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        StorageKey = storageKey;
        UploadedAtUtc = uploadedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public string FileName { get; private set; }
    public string ContentType { get; private set; }
    public long FileSizeBytes { get; private set; }
    public string StorageKey { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public static TicketAttachment Create(
        Guid ticketId,
        Guid uploadedByUserId,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string storageKey,
        DateTime uploadedAtUtc)
    {
        var attachment = new TicketAttachment(
            Guid.NewGuid(),
            ticketId,
            uploadedByUserId,
            fileName,
            contentType,
            fileSizeBytes,
            storageKey,
            uploadedAtUtc);

        attachment.Raise(new TicketAttachmentUploadedDomainEvent(attachment.Id, ticketId));

        return attachment;
    }

    public void Delete(DateTime deletedAtUtc)
    {
        DeletedAtUtc = deletedAtUtc;

        Raise(new TicketAttachmentDeletedDomainEvent(Id, TicketId));
    }
}
