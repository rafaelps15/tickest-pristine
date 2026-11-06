using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.TicketAttachments;

public sealed record TicketAttachmentDeletedDomainEvent(Guid AttachmentId, Guid TicketId) : IDomainEvent;
