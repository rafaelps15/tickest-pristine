using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed record TicketAttachmentDeletedDomainEvent(Guid AttachmentId, Guid TicketId) : IDomainEvent;
