using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.TicketAttachments;

public sealed record TicketAttachmentUploadedDomainEvent(
    Guid AttachmentId,
    Guid TicketId,
    Guid UploadedByUserId,
    string FileName) : IDomainEvent;
