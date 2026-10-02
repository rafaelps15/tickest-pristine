using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed record TicketAttachmentUploadedDomainEvent(Guid AttachmentId, Guid TicketId) : IDomainEvent;
